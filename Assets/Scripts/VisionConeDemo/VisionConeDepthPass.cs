using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VisionConeDemo
{
    public class VisionConeDepthPass : ScriptableRenderPass
    {
        private static int MAX_VISION_CONES = 16;
        FilteringSettings m_FilteringSettings;
        RenderStateBlock m_RenderStateBlock;
        List<ShaderTagId> m_ShaderTagIdList = new List<ShaderTagId>();
        string m_ProfilerTag;
        ProfilingSampler m_ProfilingSampler;
        VisionConeBuffers m_visionConeBuffers;
        Camera m_visionConeCameraProxy;
        
        int m_AtlasSquareSize;

        private Material m_overrideMaterial;
        RenderTexture m_AtlasTexture;
        RenderTargetHandle m_AtlasTextureHandle;
        
        static int worldToVisionConeMatricesId = Shader.PropertyToID("_WorldToVisionConeMatrices");
        private Matrix4x4[] worldToVisionConeMatrices;

        private int LayerFlags = 1 << LayerMask.NameToLayer("Occluder");
        
        private void InitializeCameraProxies()
        {
            // We use proxy camera to obtain accurate culling results. 
            var root = new GameObject("VisionConeCameraProxy");
            m_visionConeCameraProxy = root.AddComponent<Camera>();
            m_visionConeCameraProxy.enabled = false;
            m_visionConeCameraProxy.cullingMask = LayerFlags;
        }

        public VisionConeDepthPass(string profilerTag, RenderPassEvent evt, RenderQueueRange renderQueueRange)
        {
            m_AtlasTextureHandle = new RenderTargetHandle();
            m_AtlasTextureHandle.Init("_VisionConeDepthTexture");

            m_AtlasSquareSize = 2048;

            m_visionConeBuffers = new VisionConeBuffers(MAX_VISION_CONES);
            worldToVisionConeMatrices = new Matrix4x4[MAX_VISION_CONES];
            
            m_ProfilerTag = profilerTag;
            
            m_ProfilingSampler = new ProfilingSampler(profilerTag);
            m_ShaderTagIdList.Add(new ShaderTagId("Lit"));
            renderPassEvent = evt;

            m_FilteringSettings = new FilteringSettings(renderQueueRange, LayerFlags);
            m_RenderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
        }

        public void SetVisionConeCasterData(VisionConeBuffers data)
        {
            m_visionConeBuffers = data;
        }

        public void Setup(Material overrideMaterial)
        {
            m_overrideMaterial = overrideMaterial;
            if(m_visionConeCameraProxy == null)
                InitializeCameraProxies();
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            m_AtlasTexture = ShadowUtils.GetTemporaryShadowTexture(m_AtlasSquareSize, m_AtlasSquareSize, 16);
            ConfigureTarget(m_AtlasTexture);
            ConfigureClear(ClearFlag.All, Color.black);
        }

        /// <inheritdoc/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if(m_visionConeCameraProxy == null)
                return;

            CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                for (var i = 0; i < MAX_VISION_CONES; i++)
                {
                    var proxyCamera = m_visionConeCameraProxy;
                    if(proxyCamera == null)
                        continue;
   
                    var active = m_visionConeBuffers.VisionConePositionArray[i].w > 0;
                    if (active == false)
                    {
                        worldToVisionConeMatrices[i] = Matrix4x4.zero;
                        continue;
                    }

                    var pos = m_visionConeBuffers.VisionConeCasterPositionArray[i];
                    var rot = m_visionConeBuffers.VisionConeCasterRotationArray[i];
                    var fov = m_visionConeBuffers.VisionConeArcAngleArray[i];
                    var range = m_visionConeBuffers.VisionConeRadiusArray[i];
                    
                    var t = proxyCamera.transform;
                    t.position = pos;
                    t.rotation = rot;
                    proxyCamera.fieldOfView = fov;
                    proxyCamera.nearClipPlane = 0.1f;
                    proxyCamera.farClipPlane = range;
                    
                    var success = proxyCamera.TryGetCullingParameters(false, out var cp);
                    if(!success)
                        continue;
                    
                    var proxyCullResults = context.Cull(ref cp);

                    // Override the view projection - see https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.DrawRenderers.html
                    // for why we need to execute and clear after setting the matrices. 
                    var tr = proxyCamera.transform;
                    var camera = proxyCamera;
                    
                    // Matrix that looks from camera's position, along the forward axis.
                    var lookMatrix = Matrix4x4.LookAt(tr.position, tr.position + tr.forward, tr.up);
                    // Matrix that mirrors along Z axis, to match the camera space convention.
                    var scaleMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(1, 1, -1));
                    // Final view matrix is inverse of the LookAt matrix, and then mirrored along Z.
                    var viewMatrix = scaleMatrix * lookMatrix.inverse;

                    var tileRowColumCount = 4;

                    float tileSize = m_AtlasSquareSize / tileRowColumCount;
                    float tileOffsetX = i % tileRowColumCount;
                    float tileOffsetY = i / tileRowColumCount;
                    var tileViewport = new Rect(tileOffsetX * tileSize, tileOffsetY * tileSize, tileSize, tileSize);
                    var scissorRect = new Rect(
                        tileViewport.x + 4f, tileViewport.y + 4f,
                        tileSize - 8f, tileSize - 8f
                    );
                    
                    // https://catlikecoding.com/unity/tutorials/scriptable-render-pipeline/spotlight-shadows/
                    var scaleOffset = Matrix4x4.identity;
                    scaleOffset.m00 = scaleOffset.m11 = scaleOffset.m22 = 0.5f;
                    scaleOffset.m03 = scaleOffset.m13 = scaleOffset.m23 = 0.5f;
                    
                    var projectionMatrix = camera.projectionMatrix;
                    if (SystemInfo.usesReversedZBuffer) 
                    {
                        projectionMatrix.m20 = -projectionMatrix.m20;
                        projectionMatrix.m21 = -projectionMatrix.m21;
                        projectionMatrix.m22 = -projectionMatrix.m22;
                        projectionMatrix.m23 = -projectionMatrix.m23;
                    }
                    
                    worldToVisionConeMatrices[i] =
                        scaleOffset * (projectionMatrix * viewMatrix);
                    
                    var tileMatrix = Matrix4x4.identity;
                    tileMatrix.m00 = tileMatrix.m11 = 0.25f;
                    tileMatrix.m03 = tileOffsetX * 0.25f;
                    tileMatrix.m13 = tileOffsetY * 0.25f;
                    worldToVisionConeMatrices[i] = tileMatrix * worldToVisionConeMatrices[i];
                    
                    cmd.SetViewport(tileViewport);
                    cmd.EnableScissorRect(scissorRect);
                    
                    cmd.SetViewProjectionMatrices(viewMatrix, camera.projectionMatrix);
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

                    var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
                    var drawSettings = CreateDrawingSettings(m_ShaderTagIdList, ref renderingData, sortFlags);
                    drawSettings.overrideMaterial = m_overrideMaterial;
                    
                    context.DrawRenderers(proxyCullResults, ref drawSettings, ref m_FilteringSettings, ref m_RenderStateBlock);
                    
                    cmd.DisableScissorRect();
                }
            }

            // Set the arrays so they can be read by the vision cone shader. 
            cmd.SetGlobalMatrixArray(
                worldToVisionConeMatricesId, worldToVisionConeMatrices
            );
            cmd.SetGlobalTexture(m_AtlasTextureHandle.id, m_AtlasTexture);
            
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            
            // Reset the view projection
            var cam = renderingData.cameraData.camera;
            cmd.SetViewProjectionMatrices(cam.worldToCameraMatrix, cam.projectionMatrix);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            
            CommandBufferPool.Release(cmd);
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            if (cmd == null)
                throw new ArgumentNullException("cmd");

            if (m_AtlasTexture)
            {
                RenderTexture.ReleaseTemporary(m_AtlasTexture);
                m_AtlasTexture = null;
            }
        }

        public void Cleanup()
        {
            if (m_visionConeCameraProxy == null) 
                return;
            
            if (Application.isEditor && !Application.isPlaying)
            {
                UnityEngine.Object.DestroyImmediate(m_visionConeCameraProxy.gameObject);
                return;
            }

            UnityEngine.Object.Destroy(m_visionConeCameraProxy.gameObject);
        }
    }
}
