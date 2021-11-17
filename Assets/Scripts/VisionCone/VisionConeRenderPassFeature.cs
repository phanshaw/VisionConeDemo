using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// (Pete) Note this is added to the ForwardRenderer asset under RendererFeatures.

namespace VisionConeDemo
{
    [Serializable]
    public enum VisionConeQuality
    {
        low,
        medium,
        high
    }

    [Serializable]
    public class VisionConeRenderSettings
    {
        [Header("Properties")]

        [Tooltip("This controls the resolution of the vision cone occlusion pass. Lower values may result in visual artifacts.")]
        public VisionConeQuality quality = VisionConeQuality.high;

        [SerializeField]
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;

        [SerializeField] 
        public Shader visionConeDepthShader;

        [SerializeField] 
        public Shader visionConePassShader;

        [SerializeField] 
        public LayerMask visionConeOccluderLayers;
    }

    public class VisionConeRenderPassFeature : ScriptableRendererFeature
    {
        public class VisionConeOccluderPass : ScriptableRenderPass
        {
            FilteringSettings m_FilteringSettings;
            RenderStateBlock m_RenderStateBlock;
            List<ShaderTagId> m_ShaderTagIdList = new List<ShaderTagId>();
            string m_ProfilerTag;
            ProfilingSampler m_ProfilingSampler;
            VisionConeData[] m_visionConeData;

            int m_depthTextureSize;

            private Material _depthPassMaterial;
            private Material _renderPassMaterial;
            RenderTexture m_visionConeDepthTexture;
            RenderTargetHandle m_visionConeDepthTextureID;

            private Matrix4x4[] worldToVisionConeMatrices;
            private Vector4[] packedDataEnabledRadArc;
            private Vector4[] conePosArray;
            private Vector4[] coneDirArray;
            private Vector4[] coneColorArray;
            private Vector4[] visionConeZBufferParamsArray;
            
            private Camera _proxyCamera = null;
            private readonly int _layerFlags;

            public VisionConeOccluderPass(VisionConeRenderSettings settings, Material depthPassMaterial, Material renderPassMaterial)
            {
                _depthPassMaterial = depthPassMaterial;
                _renderPassMaterial = renderPassMaterial;
                
                _layerFlags = settings.visionConeOccluderLayers;
                m_visionConeDepthTextureID = new RenderTargetHandle();
                m_visionConeDepthTextureID.Init("_VisionConeDepthTexture");

                m_depthTextureSize = settings.quality switch
                {
                    VisionConeQuality.low => 512,
                    VisionConeQuality.medium => 1024,
                    VisionConeQuality.high => 2048,
                    _ => throw new ArgumentOutOfRangeException()
                };

                m_visionConeData = new VisionConeData[VisionConeShaderConstants.MAX_VISION_CONES];
                worldToVisionConeMatrices = new Matrix4x4[VisionConeShaderConstants.MAX_VISION_CONES];
                visionConeZBufferParamsArray = new Vector4[VisionConeShaderConstants.MAX_VISION_CONES];

                packedDataEnabledRadArc = new Vector4[VisionConeShaderConstants.MAX_VISION_CONES];
                conePosArray = new Vector4[VisionConeShaderConstants.MAX_VISION_CONES];
                coneDirArray = new Vector4[VisionConeShaderConstants.MAX_VISION_CONES];
                coneColorArray = new Vector4[VisionConeShaderConstants.MAX_VISION_CONES];

                m_ProfilerTag = "VisionConeOccluderPass";

                m_ProfilingSampler = new ProfilingSampler(m_ProfilerTag);
                m_ShaderTagIdList.Add(new ShaderTagId("ShadowCaster"));
                renderPassEvent = settings.renderPassEvent;

                m_FilteringSettings = new FilteringSettings(RenderQueueRange.opaque, _layerFlags);
                m_RenderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);

                Debug.Assert(settings.visionConeDepthShader != null, $"Vision Cone Settings had null shader reference. This will result in rendering errors.");
            }

            private void CopyVisionConeDataFromManager()
            {
                m_visionConeData = new VisionConeData[VisionConeShaderConstants.MAX_VISION_CONES];
                if (VisionConeManager.Get == null)
                    return;
                
                var counter = 0;
                foreach (var data in VisionConeManager.Get.GetVisionConeData())
                {
                    m_visionConeData[counter] = data;
                    counter++;
                    
                    // Drop any that are over our count
                    if(counter >= VisionConeShaderConstants.MAX_VISION_CONES)
                        break;
                }

                _proxyCamera = VisionConeManager.Get.GetVisionConeProxyCamera();
            }

            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                m_visionConeDepthTexture = RenderTexture.GetTemporary(m_depthTextureSize, m_depthTextureSize, 16, RenderTextureFormat.Depth);
                m_visionConeDepthTexture.filterMode = FilterMode.Bilinear;
                m_visionConeDepthTexture.wrapMode = TextureWrapMode.Clamp;

                ConfigureTarget(m_visionConeDepthTexture);
                ConfigureClear(ClearFlag.All, Color.black);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var cmd = CommandBufferPool.Get(m_ProfilerTag);
                using (new ProfilingScope(cmd, m_ProfilingSampler))
                {
                    CopyVisionConeDataFromManager();
                    
                    for (var i = 0; i < VisionConeShaderConstants.MAX_VISION_CONES; i++)
                    {
                        if(_proxyCamera == null)
                            continue;

                        var data = m_visionConeData[i];
                        if (data.Enabled == 0)
                        {
                            worldToVisionConeMatrices[i] = Matrix4x4.zero;
                            continue;
                        }

                        var pos = data.PositionWS;
                        var rot = Quaternion.LookRotation(data.Direction, Vector3.up);
                        var fov = data.FOVDegrees;
                        var range = data.Radius;

                        var t = _proxyCamera.transform;
                        t.position = pos;
                        t.rotation = rot;
                        _proxyCamera.fieldOfView = fov;
                        _proxyCamera.nearClipPlane = 0.1f;
                        _proxyCamera.farClipPlane = range;
                        
                        var success = _proxyCamera.TryGetCullingParameters(false, out var cp);
                        if(!success)
                            continue;
                        
                        var proxyCullResults = context.Cull(ref cp);

                        // Create an atlas slice - this is based off of Jasper Flick's catlike coding overview of spotlight shadows
                        // and uses a similar approach as the unity URP additional lights shadows. 
                        // https://catlikecoding.com/unity/tutorials/scriptable-render-pipeline/spotlight-shadows/
                        var lookMatrix = Matrix4x4.LookAt(pos, pos + t.forward, t.up);
                        var scaleMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(1, 1, -1));
                        var viewMatrix = scaleMatrix * lookMatrix.inverse;

                        var tileRowColumCount = 4;
                        float tileSize = m_depthTextureSize / tileRowColumCount;
                        float tileOffsetX = i % tileRowColumCount;
                        float tileOffsetY = i / tileRowColumCount;
                        var tileViewport = new Rect(tileOffsetX * tileSize, tileOffsetY * tileSize, tileSize, tileSize);
                        var scissorRect = new Rect(
                            tileViewport.x + 4f, tileViewport.y + 4f,
                            tileSize - 8f, tileSize - 8f
                        );
                        
                        var scaleOffset = Matrix4x4.identity;
                        scaleOffset.m00 = scaleOffset.m11 = scaleOffset.m22 = 0.5f;
                        scaleOffset.m03 = scaleOffset.m13 = scaleOffset.m23 = 0.5f;
                        
                        var projectionMatrix = _proxyCamera.projectionMatrix;
                        if (SystemInfo.usesReversedZBuffer) 
                        {
                            projectionMatrix.m20 = -projectionMatrix.m20;
                            projectionMatrix.m21 = -projectionMatrix.m21;
                            projectionMatrix.m22 = -projectionMatrix.m22;
                            projectionMatrix.m23 = -projectionMatrix.m23;
                        }
                        
                        // Used later on to transform our wsPos into the vision cone depth map space. 
                        var tileMatrix = Matrix4x4.identity;
                        tileMatrix.m00 = tileMatrix.m11 = 0.25f;
                        tileMatrix.m03 = tileOffsetX * 0.25f;
                        tileMatrix.m13 = tileOffsetY * 0.25f;
                        worldToVisionConeMatrices[i] = tileMatrix * (scaleOffset * (projectionMatrix * viewMatrix));
                        
                        // See Common.hlsl
                        // zBufferParam = { (f-n)/n, 1, (f-n)/n*f, 1/f }
                        var far = _proxyCamera.farClipPlane;
                        var near = _proxyCamera.nearClipPlane;
                        var x = (far-near) / near;
                        var y = 1;
                        var z = (far-near) / near * far;
                        var w = 1 / far;
                        visionConeZBufferParamsArray[i] = new Vector4(x, y, z, w);
                        
                        // Pack the vision cone data
                        packedDataEnabledRadArc[i] = new Vector3(data.Enabled, data.Radius, data.FOVDegrees);
                        conePosArray[i] = pos;
                        coneDirArray[i] = data.Direction;
                        coneColorArray[i] = data.ConeColor;

                        cmd.SetViewport(tileViewport);
                        cmd.EnableScissorRect(scissorRect);
                        
                        cmd.SetViewProjectionMatrices(viewMatrix, _proxyCamera.projectionMatrix);
                        context.ExecuteCommandBuffer(cmd);
                        cmd.Clear();

                        var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
                        var drawSettings = CreateDrawingSettings(m_ShaderTagIdList, ref renderingData, sortFlags);
                        drawSettings.overrideMaterial = _depthPassMaterial;
                        
                        context.DrawRenderers(proxyCullResults, ref drawSettings, ref m_FilteringSettings, ref m_RenderStateBlock);
                        
                        cmd.DisableScissorRect();
                    }
                }

                // Set the arrays so they can be read by the vision cone material. 
                _renderPassMaterial.SetMatrixArray(VisionConeShaderConstants.worldToVisionConeMatricesID, worldToVisionConeMatrices);
                _renderPassMaterial.SetVectorArray(VisionConeShaderConstants.packedDataEnabledRadArcID, packedDataEnabledRadArc);
                _renderPassMaterial.SetVectorArray(VisionConeShaderConstants.conePosArrayID, conePosArray);
                _renderPassMaterial.SetVectorArray(VisionConeShaderConstants.coneDirArrayID, coneDirArray);
                _renderPassMaterial.SetVectorArray(VisionConeShaderConstants.coneColorArrayID, coneColorArray);
                _renderPassMaterial.SetVectorArray(VisionConeShaderConstants.visionConeZBufferParamsID, visionConeZBufferParamsArray);
                _renderPassMaterial.SetTexture(m_visionConeDepthTextureID.id, m_visionConeDepthTexture);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                // Reset the view projection
                var cam = renderingData.cameraData.camera;
                cmd.SetViewProjectionMatrices(cam.worldToCameraMatrix, cam.projectionMatrix);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                CommandBufferPool.Release(cmd);
            }

            //public override void FrameCleanup(CommandBuffer cmd)
            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                if (cmd == null)
                    throw new ArgumentNullException("cmd");
                
                if (m_visionConeDepthTexture)
                {
                    RenderTexture.ReleaseTemporary(m_visionConeDepthTexture);
                    m_visionConeDepthTexture = null;
                }
            }
        }

        private class VisionConeRenderPass : ScriptableRenderPass
        {
            private string m_ProfilerTag = "RenderVisionCones";
            private Material m_overrideMaterial;

            public VisionConeRenderPass(Material overrideMaterial)
            {
                m_overrideMaterial = overrideMaterial;                
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var cmd = CommandBufferPool.Get(m_ProfilerTag);
                if (Application.isPlaying)
                {
                    // Frustrum corners for fast world space reconstruction
                    var cam = renderingData.cameraData.camera;
                    var t = cam.transform;
                    
                    var frustumCorners = new Vector3[4];
                    cam.CalculateFrustumCorners(new Rect(0, 0, 1, 1), cam.farClipPlane, Camera.MonoOrStereoscopicEye.Mono, frustumCorners);
                    
                    var bottomLeft = t.TransformVector(frustumCorners[0]);
                    var topLeft = t.TransformVector(frustumCorners[1]);
                    var topRight = t.TransformVector(frustumCorners[2]);
                    var bottomRight = t.TransformVector(frustumCorners[3]);

                    var frustumCornersArray = Matrix4x4.identity;
                    frustumCornersArray.SetRow(0, bottomLeft);
                    frustumCornersArray.SetRow(1, bottomRight);
                    frustumCornersArray.SetRow(2, topLeft);
                    frustumCornersArray.SetRow(3, topRight);
                    
                    Shader.SetGlobalMatrix("_FrustumCornersWS", frustumCornersArray);
                    Shader.SetGlobalVector("_CameraWS", t.position);
                    
                    RenderTargetIdentifier src = BuiltinRenderTextureType.CameraTarget;
                    RenderTargetIdentifier dst = BuiltinRenderTextureType.CurrentActive;

                    cmd.Blit(src, dst, m_overrideMaterial);
                    cmd.SetRenderTarget(dst);
                }
                // execution
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                if (cmd == null)
                    throw new ArgumentNullException("cmd");
            }
        }

        public VisionConeRenderSettings settings = new VisionConeRenderSettings();

        private VisionConeOccluderPass _visionConeOccluderPass;
        private VisionConeRenderPass _visionConeRenderPass;

        private Material m_visionConeDepthMaterial;
        private Material m_visionConeRenderMaterial;
        

        /// <inheritdoc/>
        public override void Create()
        {
            m_visionConeDepthMaterial = new Material(settings.visionConeDepthShader);
            m_visionConeRenderMaterial = new Material(settings.visionConePassShader);
            
            _visionConeOccluderPass = new VisionConeOccluderPass(settings, m_visionConeDepthMaterial, m_visionConeRenderMaterial);
            _visionConeRenderPass = new VisionConeRenderPass(m_visionConeRenderMaterial);

            // Configures where the render pass should be injected.
            _visionConeOccluderPass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            _visionConeRenderPass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        }

        // Here you can inject one or multiple render passes in the renderer.
        // This method is called when setting up the renderer once per-camera.
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(_visionConeOccluderPass);
            renderer.EnqueuePass(_visionConeRenderPass);
        }
    }
}


