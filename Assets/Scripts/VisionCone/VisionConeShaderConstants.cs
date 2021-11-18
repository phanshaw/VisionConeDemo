using UnityEngine;

namespace VisionCone
{
    public static class VisionConeShaderConstants
    {
        public static readonly int MAX_VISION_CONES = 16;
        
        public static readonly int FrustrumCornersWSID = Shader.PropertyToID("_FrustumCornersWS");
        public static readonly int CameraWSID = Shader.PropertyToID("_CameraWS");
        public static readonly int PackedDataEnabledRadArcID = Shader.PropertyToID("_packedDataEnabledRadArc");
        public static readonly int ConePosArrayID = Shader.PropertyToID("_conePosArray");
        public static readonly int ConeDirArrayID = Shader.PropertyToID("_coneDirArray");
        public static readonly int ConeColorArrayID = Shader.PropertyToID("_coneColorArray");
        public static readonly int VisionConeZBufferParamsID = Shader.PropertyToID("_visionConeZBufferParams");
        public static readonly int WorldToVisionConeMatricesID = Shader.PropertyToID("_WorldToVisionConeMatrices");
    }
}
