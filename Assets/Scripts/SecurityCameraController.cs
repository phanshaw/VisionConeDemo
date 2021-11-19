using System;
using SO;
using UnityEngine;
using VisionCone;
using Random = UnityEngine.Random;

public enum SecurityCameraState
{
    Seeking,
    TransitionSeekToAlert,
    Alert,
    TransitionAlertToSeek
}

[RequireComponent(typeof(VisionConeComponent))]
public class SecurityCameraController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField]
    private SecurityCameraConfig settings;

    [Header("Structure")]
    public Transform pivotYaw;

    public Light cameraLight;
    
    private Transform _seekTarget;
    private VisionConeComponent _visionConeComponent;

    private SecurityCameraState _state;

    private float _transitionTimer = 0;
    private float _randomOffset;
    private float _currentRadius;
    private float _currentFOV;
    private Color _currentColor;

    private void Start()
    {
        if(settings == null)
            return;

        _randomOffset = Random.Range(0, 2);
        _visionConeComponent = GetComponent<VisionConeComponent>();
        _state = SecurityCameraState.Seeking;

        _currentColor = settings.visionConeColorSeeking;
        _currentRadius = settings.radiusSeeking;
        _currentFOV = settings.fovDegreeSeeking;
        
        UpdateVisionConeValues();
    }

    private void Update()
    {
        switch (_state)
        {
            case SecurityCameraState.Seeking:
                HandleSeek();
                break;
            case SecurityCameraState.TransitionSeekToAlert:
                HandleTransitionToAlert();
                break;
            case SecurityCameraState.Alert:
                HandleAlert();
                break;
            case SecurityCameraState.TransitionAlertToSeek:
                HandleTransitionToSeek();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        UpdateVisionConeValues();
    }

    void HandleTransitionToAlert()
    {
        _transitionTimer += Time.deltaTime;
        var t = _transitionTimer / settings.durationSeekToAlert;
        var curveValue = settings.seekToAlertAnimationCurve.Evaluate(t);
        
        if (t >= 1)
        {
            ChangeState(SecurityCameraState.Alert);
            return;
        }
        
        _currentColor = Color.Lerp(settings.visionConeColorSeeking, settings.visionConeColorAlert, curveValue);
        _currentRadius = Mathf.Lerp(settings.radiusSeeking, settings.radiusAlert, curveValue);
        _currentFOV = Mathf.Lerp(settings.fovDegreeSeeking, settings.fovDegreeAlert, curveValue);
        
        LookAtTargetWithinLimit();

        cameraLight.color = settings.cameraPropColorAlert;
    }

    void HandleTransitionToSeek()
    {
        _transitionTimer += Time.deltaTime;
        var t = _transitionTimer / settings.durationAlertToSeek;
        
        // Check if we see the target first! 
        if (CheckForTarget())
        {
            var customTimer = settings.durationSeekToAlert * (1-t);
            ChangeState(SecurityCameraState.TransitionSeekToAlert, customTimer);
            return;
        }

        if (t >= 1)
        {
            ChangeState(SecurityCameraState.Seeking);
            return;
        }

        var curveValue = settings.alertToSeekAnimationCurve.Evaluate(t);
        
        _currentColor = Color.Lerp(settings.visionConeColorAlert, settings.visionConeColorSeeking, curveValue);
        _currentRadius = Mathf.Lerp(settings.radiusAlert, settings.radiusSeeking, curveValue);
        _currentFOV = Mathf.Lerp(settings.fovDegreeAlert, settings.fovDegreeSeeking, curveValue);
        
        // Make the light flash between states to indicate confusion.
        var tCol = (Mathf.Sin(Time.timeSinceLevelLoad * 15) + 1) * 0.5f;
        
        // Make a nicer transition interpolation
        float tColCubed = tCol * tCol * tCol;
        var color = Color.Lerp(settings.cameraPropColorAlert, settings.cameraPropColorConfused, tColCubed);
        cameraLight.color = color;
    }

    void HandleSeek()
    {
        if (CheckForTarget())
        {
            ChangeState(SecurityCameraState.TransitionSeekToAlert);
        }
        
        cameraLight.color = settings.cameraPropColorSeeking;
    }
    
    void HandleAlert()
    {
        if (!CheckForTarget())
        {
            ChangeState(SecurityCameraState.TransitionAlertToSeek);
            return;
        }

        LookAtTargetWithinLimit();
        
        cameraLight.color = settings.cameraPropColorAlert;
    }

    private bool WithinRangeCheck(Vector3 p1, Vector3 p2, float rad)
    {
        return !((p1 - p2).sqrMagnitude > rad *  rad);
    }

    private void LookAtTargetWithinLimit() 
    {
        var targetPos = _seekTarget.position;
        var viewPos3D = _visionConeComponent.ViewPosition;
        var lookDir = targetPos - pivotYaw.position;
        
        var targetRelativeDir = Vector3.Normalize(targetPos - viewPos3D);
        targetRelativeDir.y = 0;
        targetRelativeDir.Normalize();
        
        var viewRelativeAngle = Vector3.Angle(_visionConeComponent.transform.forward, targetRelativeDir);
        if (viewRelativeAngle > settings.rotationDegrees)
            return;
        
        var rotation = Quaternion.LookRotation(lookDir);
        
        // Flatten to match the way we are rendering
        var targetPos2D = targetPos;
        targetPos2D.y = 0;
        var viewPos2D = viewPos3D;
        viewPos2D.y = 0;
        if(WithinRangeCheck(targetPos2D, viewPos2D, settings.minRadius))
            return;
        
        pivotYaw.rotation = rotation;
    }

    void ChangeState(SecurityCameraState newState, float timerOverride = 0)
    {
        _transitionTimer = 0;

        if (timerOverride != 0)
            _transitionTimer = timerOverride;
        
        _state = newState;
    }

    bool CheckForTarget()
    {
        var targetPos = TargetManager.Get.MousePosWS;
        var viewPos3D = _visionConeComponent.ViewPosition;
        var radius = _visionConeComponent.Radius;

        // Flatten to match the way we are rendering
        var targetPos2D = targetPos;
        targetPos2D.y = 0;
        var viewPos2D = viewPos3D;
        viewPos2D.y = 0;

        if ((targetPos2D - viewPos2D).sqrMagnitude > radius * radius)
            return false;
        
        var viewDir = _visionConeComponent.ViewDirection;
        viewDir.y = 0;
        viewDir.Normalize();
        
        var targetRelativeDir = Vector3.Normalize(targetPos - viewPos3D);
        targetRelativeDir.y = 0;
        targetRelativeDir.Normalize();
        
        var viewRelativeAngle = Vector3.Angle(viewDir, targetRelativeDir);
        if (viewRelativeAngle <= _currentFOV * 0.5f)
        {
            var rayDir = Vector3.Normalize(targetPos - viewPos3D);
            
            Debug.DrawRay(viewPos3D, rayDir * 10);
            
            if (Physics.Raycast(viewPos3D, rayDir, out var hitInfo))
            {
                if (hitInfo.collider.gameObject.layer == LayerMask.NameToLayer("Player"))
                {
                    _seekTarget = TargetManager.Get.Player;
                    return true;
                }
            }
        }

        _seekTarget = null;
        return false;

    }

    private void UpdateVisionConeValues()
    {
        _visionConeComponent.Radius = _currentRadius;
        _visionConeComponent.FovDegrees = _currentFOV;
        _visionConeComponent.currentColor = _currentColor;
    }
}
