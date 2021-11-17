using System;
using SO;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using VisionConeDemo;
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

        _currentColor = settings.colorSeeking;
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

    bool InsideBoundsSphere(Vector3 position, float radius)
    {
        return Vector3.Dot(position, position) <= radius * radius;
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
        
        _currentColor = Color.Lerp(settings.colorSeeking, settings.colorAlert, curveValue);
        _currentRadius = Mathf.Lerp(settings.radiusSeeking, settings.radiusAlert, curveValue);
        _currentFOV = Mathf.Lerp(settings.fovDegreeSeeking, settings.fovDegreeAlert, curveValue);
    }

    void HandleTransitionToSeek()
    {
        _transitionTimer += Time.deltaTime;
        var t = _transitionTimer / settings.durationSeekToAlert;

        if (t >= 1)
        {
            ChangeState(SecurityCameraState.Seeking);
            return;
        }

        var curveValue = settings.alertToSeekAnimationCurve.Evaluate(t);
        
        _currentColor = Color.Lerp(settings.colorAlert, settings.colorSeeking, curveValue);
        _currentRadius = Mathf.Lerp(settings.radiusAlert, settings.radiusSeeking, curveValue);
        _currentFOV = Mathf.Lerp(settings.fovDegreeAlert, settings.fovDegreeSeeking, curveValue);
    }

    void HandleSeek()
    {
        if (CheckForTarget())
        {
            ChangeState(SecurityCameraState.TransitionSeekToAlert);
        }
    }
    
    void HandleAlert()
    {
        if (!CheckForTarget())
        {
            ChangeState(SecurityCameraState.TransitionAlertToSeek);
        }
    }

    void ChangeState(SecurityCameraState newState)
    {
        _transitionTimer = 0;
        _state = newState;
    }

    bool CheckForTarget()
    {
        var target = TargetManager.Get.MousePosWS;
        if (!InsideBoundsSphere(target, _visionConeComponent.Radius)) 
            return false;
        
        // Rebuild the arc
        // cosine between those two angles
        var viewPos = _visionConeComponent.ViewPosition;
        var viewDir = _visionConeComponent.ViewDirection;
        var relPos = target - viewPos;
        var nrmRelPos = Vector3.Normalize(relPos);
        var dp = Vector3.Dot( viewDir, Vector3.Normalize(relPos));

        // get angle from the cosine from 0 (180) to 1 (90)
        var dp_angle = ((Mathf.Acos(dp) / Mathf.PI) * 180);
        if (dp_angle <= _currentFOV * 0.5f)
        {
            // Now do a raycast to see if we can really see! 
            if (Physics.Raycast(viewPos, nrmRelPos, out RaycastHit hitInfo))
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
