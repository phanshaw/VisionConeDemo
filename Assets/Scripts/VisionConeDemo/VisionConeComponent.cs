#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityEngine;

namespace VisionConeDemo
{
    public class VisionConeComponent : MonoBehaviour
    {
        [SerializeField] 
        [Range(0.1f, 15f)]
        private float radius;

        [SerializeField] 
        [Range(5f, 180f)]
        private float fovDegrees;

        [SerializeField] 
        private Transform viewpointTransform;

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
            return new VisionConeData(true, radius, fovDegrees, viewpointTransform.position, viewpointTransform.forward);
        }

        private void Start()
        {
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
            var t = viewpointTransform;
            var pos = t.position;
            pos.y = transform.position.y; // Floor it
            Handles.Label(pos, $"Debug Vision Cone: Rad {radius} FOV {fovDegrees}");

            Handles.color = new Color(1f, 0, 0, 0.1f);
            var centeredForward = Quaternion.AngleAxis(-fovDegrees * 0.5f, Vector3.up) * t.forward;
            Handles.DrawSolidArc(pos, t.up, centeredForward, fovDegrees, radius);
        }
#endif
    }
}
