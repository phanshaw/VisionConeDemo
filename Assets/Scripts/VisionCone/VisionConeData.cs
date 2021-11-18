using UnityEngine;

namespace VisionCone
{
    public struct VisionConeData
    {
        public int Enabled;
        public float Radius;
        public float FOVDegrees;
        public Vector3 PositionWS;
        public Vector3 Direction;
        public Vector4 ConeColor;

        public VisionConeData(bool enabled, float radius, float fovDegrees, Vector3 positionWs, Vector3 direction, Color color)
        {
            Enabled = enabled ? 1 : 0;
            Radius = radius;
            FOVDegrees = fovDegrees;
            PositionWS = positionWs;
            Direction = direction;
            ConeColor = color;
        }
    }
}
