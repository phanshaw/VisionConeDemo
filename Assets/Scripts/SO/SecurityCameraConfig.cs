using UnityEngine;

namespace SO
{
    [CreateAssetMenu(menuName = "VisionConeDemo/SecurityCameraConfig", fileName = "NewSecurityCameraConfig")]
    public class SecurityCameraConfig : ScriptableObject
    {
        [SerializeField]
        public float durationSeekToAlert = 1f;
        
        [SerializeField]
        public float durationAlertToSeek = 2f;

        [SerializeField] 
        public AnimationCurve seekToAlertAnimationCurve;
        
        [SerializeField] 
        public AnimationCurve alertToSeekAnimationCurve;
        
        [SerializeField]
        public float rotationSpeed = 1f;
        
        [SerializeField]
        public float rotationDegrees = 45;
        
        [SerializeField] 
        [Range(0.1f, 1f)]
        public float minRadius = 1;
        
        [SerializeField] 
        [Range(0.1f, 15f)]
        public float radiusSeeking = 5;
        
        [SerializeField] 
        [Range(45f, 120f)]
        public float fovDegreeSeeking = 90;
        
        [SerializeField] 
        [Range(0.1f, 15f)]
        public float radiusAlert = 10;
        
        [SerializeField] 
        [Range(45f, 120f)]
        public float fovDegreeAlert = 35;
        
        [SerializeField]
        public Color visionConeColorSeeking = Color.green;

        [SerializeField] 
        public Color visionConeColorAlert = Color.red;
        
        [SerializeField]
        public Color cameraPropColorSeeking = Color.green;
        
        [SerializeField]
        public Color cameraPropColorConfused = new Color(1, 0.5f, 0, 1);

        [SerializeField] 
        public Color cameraPropColorAlert = Color.red;
    }
}
