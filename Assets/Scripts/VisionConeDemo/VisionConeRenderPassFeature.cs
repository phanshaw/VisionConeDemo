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
            RenderTexture m_visionConeDepthArray;
            RenderTargetHandle m_visionConeOccluderAtlasHandle;

            private Matrix4x4[] worldToVisionConeMatrices;
            private Vector4[] packedDataEnabledRadArc;
            private Vector4[] conePosArray;
            private Vector4[] coneDirArray;
            private Vector4[] coneColorArray;
            
            private Camera _proxyCamera = null;
            private readonly int _layerFlags;

            public VisionConeOccluderPass(VisionConeRenderSettings settings, Material depthPassMaterial, Material renderPassMaterial)
            {
                _depthPassMaterial = depthPassMaterial;
                _renderPassMaterial = renderPassMaterial;
                
                _layerFlags = settings.visionConeOccluderLayers;
                m_visionConeOccluderAtlasHandle = new RenderTargetHandle();
                m_visionConeOccluderAtlasHandle.Init("_VisionConeDepthTexture");

                m_depthTextureSize = settings.quality switch
                {
                    VisionConeQuality.low => 256,
                    VisionConeQuality.medium => 512,
                    VisionConeQuality.high => 1024,
                    _ => throw new ArgumentOutOfRangeException()
                };

                m_visionConeData = new VisionConeData[VisionConeShaderConstants.MAX_VISION_CONES];
                worldToVisionConeMatrices = new Matrix4x4[VisionConeShaderConstants.MAX_VISION_CONES];

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
                m_visionConeDepthArray = RenderTexture.GetTemporary(m_depthTextureSize, m_depthTextureSize, 16, RenderTextureFormat.Depth);
                m_visionConeDepthArray.dimension = TextureDimension.Tex2DArray;
                m_visionConeDepthArray.volumeDepth = VisionConeShaderConstants.MAX_VISION_CONES;
                
                m_visionConeDepthArray.filterMode = FilterMode.Point;
                m_visionConeDepthArray.wrapMode = TextureWrapMode.Clamp;

                ConfigureTarget(m_visionConeDepthArray);
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
                        if (_proxyCamera == null)
                            continue;

                        var data = m_visionConeData[i];
                        if (data.Enabled == 0)
                        {
                            worldToVisionConeMatrices[i] = Matrix4x4.zero;
                            continue;
                        }
                        
                        packedDataEnabledRadArc[i] = new Vector3(data.Enabled, data.Radius, data.FOVDegrees);
                        conePosArray[i] = data.PositionWS;
                        coneDirArray[i] = data.Direction;
                        coneColorArray[i] = data.ConeColor;

                        var t = _proxyCamera.transform;
                        t.position = data.PositionWS;
                        t.rotation = Quaternion.Euler(data.Direction);
      
                        var tan = Vector3.Cross(data.Direction, Vector3.up);
                        var up = Vector3.Cross(tan, data.Direction);
                        
                        var lookMatrix = Matrix4x4.LookAt(data.PositionWS, data.PositionWS + data.Direction, up);
                        var scaleMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(1, 1, -1));
                        var viewMatrix = scaleMatrix * lookMatrix.inverse;
                        
                        var projectionMatrix = Matrix4x4.Perspective(data.FOVDegrees, 1, 0.01f, data.Radius);
                        if (SystemInfo.usesReversedZBuffer) 
                        {
                            projectionMatrix.m20 = -projectionMatrix.m20;
                            projectionMatrix.m21 = -projectionMatrix.m21;
                            projectionMatrix.m22 = -projectionMatrix.m22;
                            projectionMatrix.m23 = -projectionMatrix.m23;
                        }
                        
                        var scaleOffset = Matrix4x4.identity;
                        scaleOffset.m00 = scaleOffset.m11 = scaleOffset.m22 = 0.5f;
                        scaleOffset.m03 = scaleOffset.m13 = scaleOffset.m23 = 0.5f;
                        
                        var worldToShadowMatrix = scaleOffset * (projectionMatrix * viewMatrix);

                        cmd.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
                        context.ExecuteCommandBuffer(cmd);
                        cmd.Clear();

                        worldToVisionConeMatrices[i] = worldToShadowMatrix;

                        var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
                        var drawSettings = CreateDrawingSettings(m_ShaderTagIdList, ref renderingData, sortFlags);
                        drawSettings.overrideMaterial = _depthPassMaterial;

                        cmd.SetRenderTarget(m_visionConeDepthArray, 0, CubemapFace.Unknown, i);
                        
                        // We use the proxy camera for culling to get a list of renderers appropriate to this vision cone.
                        _proxyCamera.fieldOfView = data.FOVDegrees;
                        _proxyCamera.nearClipPlane = 0.01f;
                        _proxyCamera.farClipPlane = data.Radius;
                        _proxyCamera.aspect = 1;
                        var success = _proxyCamera.TryGetCullingParameters(false, out var cp);
                        if (!success)
                            continue;
                        var proxyCullResults = context.Cull(ref cp);
                        context.DrawRenderers(proxyCullResults, ref drawSettings, ref m_FilteringSettings,
                            ref m_RenderStateBlock);
                        
                        context.ExecuteCommandBuffer(cmd);
                        cmd.Clear();
                    }
                }

                // Set the arrays so they can be read by the vision cone material. 
                _renderPassMaterial.SetMatrixArray(VisionConeShaderConstants.worldToVisionConeMatricesId, worldToVisionConeMatrices);
                _renderPassMaterial.SetVectorArray(VisionConeShaderConstants.packedDataEnabledRadArcID, packedDataEnabledRadArc);
                _renderPassMaterial.SetVectorArray(VisionConeShaderConstants.conePosArrayID, conePosArray);
                _renderPassMaterial.SetVectorArray(VisionConeShaderConstants.coneDirArrayID, coneDirArray);
                _renderPassMaterial.SetVectorArray(VisionConeShaderConstants.coneColorArrayID, coneColorArray);
                _renderPassMaterial.SetTexture(m_visionConeOccluderAtlasHandle.id, m_visionConeDepthArray);

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
                
                if (m_visionConeDepthArray)
                {
                    RenderTexture.ReleaseTemporary(m_visionConeDepthArray);
                    m_visionConeDepthArray = null;
                }
            }
        }

        private class VisionConeRenderPass : ScriptableRenderPass
        {
            private string m_ProfilerTag = "RenderVisionCones";
            private Material m_overrideMaterial;

            public VisionConeRenderPass(VisionConeRenderSettings settings, Material overrideMaterial)
            {
                Debug.Assert(settings.visionConePassShader != null, $"Vision Cone Settings had null shader reference. This will result in rendering errors.");
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
            _visionConeRenderPass = new VisionConeRenderPass(settings, m_visionConeRenderMaterial);

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


