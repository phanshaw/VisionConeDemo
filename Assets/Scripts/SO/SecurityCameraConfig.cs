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
        [Range(0.1f, 15f)]
        public float radiusSeeking = 5;
        
        [SerializeField] 
        [Range(5f, 180f)]
        public float fovDegreeSeeking = 90;
        
        [SerializeField] 
        [Range(0.1f, 15f)]
        public float radiusAlert = 10;
        
        [SerializeField] 
        [Range(5f, 180f)]
        public float fovDegreeAlert = 35;
        
        [SerializeField]
        public Color colorSeeking = Color.green;

        [SerializeField] 
        public Color colorAlert = Color.red;
    }
}
