using UnityEngine;

namespace VisionConeDemo
{
    public static class VisionConeShaderConstants
    {
        public static int MAX_VISION_CONES = 16;
        
        public static int packedDataEnabledRadArcID = Shader.PropertyToID("_packedDataEnabledRadArc");
        public static int conePosArrayID = Shader.PropertyToID("_conePosArray");
        public static int coneDirArrayID = Shader.PropertyToID("_coneDirArray");
        public static int coneColorArrayID = Shader.PropertyToID("_coneColorArray");
        public static int visionConeZBufferParamsID = Shader.PropertyToID("_visionConeZBufferParams");
        public static int worldToVisionConeMatricesID = Shader.PropertyToID("_WorldToVisionConeMatrices");
    }
}
