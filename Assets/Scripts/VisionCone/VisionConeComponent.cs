using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VisionCone
{
    public class VisionConeComponent : MonoBehaviour
    {
        public float Radius { get; set; } = 5;
        public float FovDegrees { get; set; } = 45;
        public Transform viewpointTransform;
        public Color currentColor = Color.white;
        
        public Vector3 ViewPosition => viewpointTransform.position;

        public Vector3 ViewDirection => viewpointTransform.forward;

        private void RegisterVisionConeComponent()
        {
            VisionConeManager.Get.RegisterVisionCone(this);
        }

        private void DeRegisterVisionConeComponent()
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
            var viewportTransform = viewpointTransform != null ? viewpointTransform : transform;
            var pos = viewportTransform.position;
            Handles.Label(pos, $"Vision Cone: Rad {Radius} FOV {FovDegrees}");
        }
#endif
    }
}
