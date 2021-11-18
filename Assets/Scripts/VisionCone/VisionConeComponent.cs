#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityEngine;

namespace VisionConeDemo
{
    public class VisionConeComponent : MonoBehaviour
    {
        public float Radius { get; set; } = 5;
        public float FovDegrees { get; set; } = 45;
        public Transform viewpointTransform;
        public Color currentColor = Color.white;
        
        public Vector3 ViewPosition => viewpointTransform.position;

        public Vector3 ViewDirection => viewpointTransform.forward;

        void RegisterVisionConeComponent()
        {
            VisionConeManager.Get.RegisterVisionCone(this);
        }
    
        void DeRegisterVisionConeComponent()
        {
            VisionConeManager.Get.DeregisterVisionCone(this);
        }

        public VisionConeData GetData()
        {
            var t = viewpointTransform ? viewpointTransform : transform;
            var forward2D = t.forward;
            forward2D.y = 0;
            forward2D.Normalize();
            
            return new VisionConeData(true, Radius, FovDegrees, t.position, forward2D, currentColor);
        }

        private void Start()
        {
            Debug.Assert(viewpointTransform != null, $"{name} does not have it's viewportTransform set.");
            RegisterVisionConeComponent();
        }

        private void OnDestroy()
        {
            DeRegisterVisionConeComponent();
        }

        private void OnDisable()
        {
            DeRegisterVisionConeComponent();
        }
        
#if UNITY_EDITOR

        private void OnDrawGizmosSelected()
        {
            // This draws the arc from the feet, which is the intended rendering of the 2.5d vision cones.

            var t = viewpointTransform != null ? viewpointTransform : transform;
            var pos = t.position;
            pos.y = transform.position.y; // Floor it
            Handles.Label(pos, $"Debug Vision Cone: Rad {Radius} FOV {FovDegrees}");

            Handles.color = new Color(1f, 0, 0, 0.1f);
            var centeredForward = Quaternion.AngleAxis(-FovDegrees * 0.5f, Vector3.up) * t.forward;
            Handles.DrawSolidArc(pos, t.up, centeredForward, FovDegrees, Radius);
        }
#endif
    }
}
