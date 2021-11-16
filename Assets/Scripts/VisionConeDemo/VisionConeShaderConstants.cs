using UnityEngine;

namespace VisionConeDemo
{
    public static class VisionConeShaderConstants
    {
        public static int MAX_VISION_CONES = 1;
        
        public static int packedDataEnabledRadArcID = Shader.PropertyToID("_packedDataEnabledRadArc");
        public static int conePosArrayID = Shader.PropertyToID("_conePosArray");
        public static int coneDirArrayID = Shader.PropertyToID("_coneDirArray");
        public static int coneColorArrayID = Shader.PropertyToID("_coneColorArray");
        public static int worldToVisionConeMatricesId = Shader.PropertyToID("_WorldToVisionConeMatrices");
    }
}
