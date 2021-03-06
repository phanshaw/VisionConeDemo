using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// (Pete) Note this is added to the ForwardRenderer asset under RendererFeatures.
namespace VisionCone
{
    [Serializable]
    public enum VisionConeQuality
    {
        Low,
        Medium,
        High
    }

    [Serializable]
    public class VisionConeRenderSettings
    {
        [Header("Properties")]

        [Tooltip("This controls the resolution of the vision cone occlusion pass. Lower values may result in visual artifacts.")]
        public VisionConeQuality quality = VisionConeQuality.High;

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
        private class VisionConeOccluderPass : ScriptableRenderPass
        {
            private FilteringSettings _filteringSettings;
            private RenderStateBlock _renderStateBlock;
            private readonly List<ShaderTagId> _shaderTagIdList = new List<ShaderTagId>();
            private readonly string _profilerTag;
            private readonly ProfilingSampler _profilingSampler;
            private VisionConeData[] _visionConeData;

            private readonly int _depthTextureSize;

            private readonly Material _depthPassMaterial;
            private readonly Material _renderPassMaterial;
            private RenderTexture _visionConeDepthTexture;
            private readonly RenderTargetHandle _visionConeDepthTextureID;

            private readonly Matrix4x4[] _worldToVisionConeMatrices;
            private readonly Vector4[] _packedDataEnabledRadArc;
            private readonly Vector4[] _conePosArray;
            private readonly Vector4[] _coneDirArray;
            private readonly Vector4[] _coneColorArray;
            private readonly Vector4[] _visionConeZBufferParamsArray;

            private int TileRowColumnCount { get; } = 4;
            private Camera _proxyCamera;

            public VisionConeOccluderPass(VisionConeRenderSettings settings, Material depthPassMaterial, Material renderPassMaterial)
            {
                _depthPassMaterial = depthPassMaterial;
                _renderPassMaterial = renderPassMaterial;
                
                _visionConeDepthTextureID = new RenderTargetHandle();
                _visionConeDepthTextureID.Init("_VisionConeDepthTexture");

                _depthTextureSize = settings.quality switch
                {
                    VisionConeQuality.Low => 512,
                    VisionConeQuality.Medium => 1024,
                    VisionConeQuality.High => 2048,
                    _ => throw new ArgumentOutOfRangeException()
                };

                _visionConeData = new VisionConeData[VisionConeShaderConstants.MAX_VISION_CONES];
                _worldToVisionConeMatrices = new Matrix4x4[VisionConeShaderConstants.MAX_VISION_CONES];
                _visionConeZBufferParamsArray = new Vector4[VisionConeShaderConstants.MAX_VISION_CONES];

                _packedDataEnabledRadArc = new Vector4[VisionConeShaderConstants.MAX_VISION_CONES];
                _conePosArray = new Vector4[VisionConeShaderConstants.MAX_VISION_CONES];
                _coneDirArray = new Vector4[VisionConeShaderConstants.MAX_VISION_CONES];
                _coneColorArray = new Vector4[VisionConeShaderConstants.MAX_VISION_CONES];

                _profilerTag = "VisionConeOccluderPass";

                _profilingSampler = new ProfilingSampler(_profilerTag);
                _shaderTagIdList.Add(new ShaderTagId("ShadowCaster"));
                renderPassEvent = settings.renderPassEvent;

                int layerFlags = settings.visionConeOccluderLayers;
                _filteringSettings = new FilteringSettings(RenderQueueRange.opaque, layerFlags);
                _renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);

                Debug.Assert(settings.visionConeDepthShader != null, $"Vision Cone Settings had null shader reference. This will result in rendering errors.");
            }

            private void CopyVisionConeDataFromManager()
            {
                _visionConeData = new VisionConeData[VisionConeShaderConstants.MAX_VISION_CONES];
                if (VisionConeManager.Get == null)
                    return;
                
                var counter = 0;
                foreach (var data in VisionConeManager.Get.GetVisionConeData())
                {
                    _visionConeData[counter] = data;
                    counter++;
                    
                    // Drop any that are over our count
                    if(counter >= VisionConeShaderConstants.MAX_VISION_CONES)
                        break;
                }

                _proxyCamera = VisionConeManager.Get.GetVisionConeProxyCamera();
            }

            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                _visionConeDepthTexture = RenderTexture.GetTemporary(_depthTextureSize, _depthTextureSize, 16, RenderTextureFormat.Depth);
                _visionConeDepthTexture.filterMode = FilterMode.Bilinear;
                _visionConeDepthTexture.wrapMode = TextureWrapMode.Clamp;

                ConfigureTarget(_visionConeDepthTexture);
                ConfigureClear(ClearFlag.All, Color.black);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var cmd = CommandBufferPool.Get(_profilerTag);
                using (new ProfilingScope(cmd, _profilingSampler))
                {
                    CopyVisionConeDataFromManager();

                    for (var i = 0; i < VisionConeShaderConstants.MAX_VISION_CONES; i++)
                    {
                        if(_proxyCamera == null)
                            continue;

                        var data = _visionConeData[i];
                        if (data.Enabled == 0)
                        {
                            _worldToVisionConeMatrices[i] = Matrix4x4.zero;
                            continue;
                        }

                        var pos = data.PositionWS;
                        var rot = Quaternion.LookRotation(data.Direction, Vector3.up);
                        var fov = data.FOVDegrees;
                        var range = data.Radius * 2;

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

                        float tileSize = _depthTextureSize / TileRowColumnCount;
                        float tileOffsetX = i % TileRowColumnCount;
                        float tileOffsetY = i / TileRowColumnCount;
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
                        _worldToVisionConeMatrices[i] = tileMatrix * (scaleOffset * (projectionMatrix * viewMatrix));
                        
                        // See Common.hlsl
                        // zBufferParam = { (f-n)/n, 1, (f-n)/n*f, 1/f }
                        var far = _proxyCamera.farClipPlane;
                        var near = _proxyCamera.nearClipPlane;
                        var x = (far-near) / near;
                        var y = 1;
                        var z = (far-near) / near * far;
                        var w = 1 / far;
                        _visionConeZBufferParamsArray[i] = new Vector4(x, y, z, w);
                        
                        // Pack the vision cone data
                        _packedDataEnabledRadArc[i] = new Vector3(data.Enabled, data.Radius, data.FOVDegrees);
                        _conePosArray[i] = pos;
                        _coneDirArray[i] = data.Direction;
                        _coneColorArray[i] = data.ConeColor;

                        cmd.SetViewport(tileViewport);
                        cmd.EnableScissorRect(scissorRect);
                        
                        cmd.SetViewProjectionMatrices(viewMatrix, _proxyCamera.projectionMatrix);
                        context.ExecuteCommandBuffer(cmd);
                        cmd.Clear();

                        var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
                        var drawSettings = CreateDrawingSettings(_shaderTagIdList, ref renderingData, sortFlags);
                        drawSettings.overrideMaterial = _depthPassMaterial;
                        
                        context.DrawRenderers(proxyCullResults, ref drawSettings, ref _filteringSettings, ref _renderStateBlock);
                        
                        cmd.DisableScissorRect();
                    }
                }

                // Set the arrays so they can be read by the vision cone material. 
                _renderPassMaterial.SetMatrixArray(VisionConeShaderConstants.WorldToVisionConeMatricesID, _worldToVisionConeMatrices);
                _renderPassMaterial.SetVectorArray(VisionConeShaderConstants.PackedDataEnabledRadArcID, _packedDataEnabledRadArc);
                _renderPassMaterial.SetVectorArray(VisionConeShaderConstants.ConePosArrayID, _conePosArray);
                _renderPassMaterial.SetVectorArray(VisionConeShaderConstants.ConeDirArrayID, _coneDirArray);
                _renderPassMaterial.SetVectorArray(VisionConeShaderConstants.ConeColorArrayID, _coneColorArray);
                _renderPassMaterial.SetVectorArray(VisionConeShaderConstants.VisionConeZBufferParamsID, _visionConeZBufferParamsArray);
                _renderPassMaterial.SetTexture(_visionConeDepthTextureID.id, _visionConeDepthTexture);

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
                    throw new ArgumentNullException(nameof(cmd));
                
                if (_visionConeDepthTexture)
                {
                    RenderTexture.ReleaseTemporary(_visionConeDepthTexture);
                    _visionConeDepthTexture = null;
                }
            }
        }

        private class VisionConeRenderPass : ScriptableRenderPass
        {
            private string _profilerTag = "RenderVisionCones";
            private readonly Material _overrideMaterial;

            public VisionConeRenderPass(Material overrideMaterial)
            {
                _overrideMaterial = overrideMaterial;                
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var cmd = CommandBufferPool.Get(_profilerTag);
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
                    
                    _overrideMaterial.SetMatrix(VisionConeShaderConstants.FrustrumCornersWSID, frustumCornersArray);
                    _overrideMaterial.SetVector(VisionConeShaderConstants.CameraWSID, t.position);
                    
                    RenderTargetIdentifier src = BuiltinRenderTextureType.CameraTarget;
                    RenderTargetIdentifier dst = BuiltinRenderTextureType.CurrentActive;

                    cmd.Blit(src, dst, _overrideMaterial);
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

        private Material _visionConeDepthMaterial;
        private Material _visionConeRenderMaterial;

        public override void Create()
        {
            _visionConeDepthMaterial = new Material(settings.visionConeDepthShader);
            _visionConeRenderMaterial = new Material(settings.visionConePassShader);
            
            _visionConeOccluderPass = new VisionConeOccluderPass(settings, _visionConeDepthMaterial, _visionConeRenderMaterial);
            _visionConeRenderPass = new VisionConeRenderPass(_visionConeRenderMaterial);

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


