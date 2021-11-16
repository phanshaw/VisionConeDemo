using UnityEngine;

namespace VisionConeDemo
{
    public struct VisionConeBuffers
    {
        private static readonly string s_visionConePositionArrayName = "_visionConePositionArray";
        private static readonly string s_visionConeColorArrayName = "_visionConeColorArray";
        private static readonly string s_visionConeRadiusArrayName = "_visionConeRadiusArray";
        private static readonly string s_visionConeArcAngleArrayName = "_visionConeArcAngleArray";
        private static readonly string s_visionConeDirectionArrayName = "_visionConeDirectionArray";

        // W component is active flag
        private static readonly int SVisionConePositionArrayId = Shader.PropertyToID(s_visionConePositionArrayName);
        private static readonly int SVisionConeColorArrayId = Shader.PropertyToID(s_visionConeColorArrayName);
        private static readonly int SVisionConeRadiusArrayId = Shader.PropertyToID(s_visionConeRadiusArrayName);
        private static readonly int SVisionConeArcAngleArrayId = Shader.PropertyToID(s_visionConeArcAngleArrayName);
        private static readonly int SVisionConeDirectionArrayId = Shader.PropertyToID(s_visionConeDirectionArrayName);
        
        public float[] VisionConeArcAngleArray { get; set; }
        public float[] VisionConeRadiusArray { get; set; }
        public Vector4[] VisionConeDirectionArray { get; set; }
        public Vector4[] VisionConeColorArray { get; set; }
        public Vector4[] VisionConePositionArray { get; set; }
        public Vector4[] VisionConeCasterPositionArray { get; set; }
        public Quaternion[] VisionConeCasterRotationArray { get; set; }

        public VisionConeBuffers(int maxVisionCones)
        {
            VisionConeCasterPositionArray = new Vector4[maxVisionCones];
            VisionConeCasterRotationArray = new Quaternion[maxVisionCones];
            VisionConePositionArray = new Vector4[maxVisionCones];
            VisionConeColorArray = new Vector4[maxVisionCones];
            VisionConeDirectionArray = new Vector4[maxVisionCones];
            VisionConeRadiusArray = new float[maxVisionCones];
            VisionConeArcAngleArray = new float[maxVisionCones];
        }
        
        public void Set()
        {
            if(VisionConePositionArray != null)
                Shader.SetGlobalVectorArray(SVisionConePositionArrayId, VisionConePositionArray);

            if(VisionConeColorArray != null)
                Shader.SetGlobalVectorArray(SVisionConeColorArrayId, VisionConeColorArray);
            
            if(VisionConeDirectionArray != null)
                Shader.SetGlobalVectorArray(SVisionConeDirectionArrayId, VisionConeDirectionArray);
            
            if(VisionConeRadiusArray != null)
                Shader.SetGlobalFloatArray(SVisionConeRadiusArrayId, VisionConeRadiusArray);
            
            if(VisionConeArcAngleArray != null)
                Shader.SetGlobalFloatArray(SVisionConeArcAngleArrayId, VisionConeArcAngleArray);
        }
    }
}