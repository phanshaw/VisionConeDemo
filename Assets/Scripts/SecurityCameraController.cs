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
    private bool _canSeeTarget;
    private VisionConeComponent _visionConeComponent;

    private SecurityCameraState _state;

    private float _counter = 0;
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
        
        SetVisionConeValues();
    }

    private void Update()
    {
        switch (_state)
        {
            case SecurityCameraState.Seeking:
                HandleSeek();
                break;
            case SecurityCameraState.TransitionSeekToAlert:
                HandleTransition();
                break;
            case SecurityCameraState.Alert:
                HandleAlert();
                break;
            case SecurityCameraState.TransitionAlertToSeek:
                HandleTransition();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        // TODO: Manage cooldowns and transitions
        if (CheckForTarget())
        {
            _state = SecurityCameraState.Alert;
        }
        else
        {
            _state = SecurityCameraState.Seeking;
        }

        SetVisionConeValues();
    }

    bool InsideBoundsSphere(Vector3 position, float radius)
    {
        return Vector3.Dot(position, position) <= radius * radius;
    }

    void HandleTransition()
    {
        // Transition from one state to another. 
    }

    void HandleSeek()
    {
        _counter += Time.deltaTime;
        var yawTarget = Mathf.Sin((_counter + _randomOffset) * settings.rotationSpeed) * settings.rotationDegrees;
        var localRotation = transform.localRotation.eulerAngles;
        localRotation.y = yawTarget;
        
        pivotYaw.localRotation = Quaternion.Euler(localRotation);
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
                    return true;
                }
            }
        }

        return false;

    }

    void HandleAlert()
    {
        // Stare at the target fiercely. 
    }

    private void SetVisionConeValues()
    {
        switch (_state)
        {
            case SecurityCameraState.Seeking:
                _visionConeComponent.Radius = _currentRadius;
                _visionConeComponent.FovDegrees = _currentFOV;
                _visionConeComponent.currentColor = _currentColor;
                break;
            case SecurityCameraState.TransitionSeekToAlert:
                break;
            case SecurityCameraState.Alert:
                _visionConeComponent.Radius = settings.radiusAlert;
                _visionConeComponent.FovDegrees = settings.fovDegreeAlert;
                _visionConeComponent.currentColor = settings.colorAlert;
                break;
            case SecurityCameraState.TransitionAlertToSeek:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}
