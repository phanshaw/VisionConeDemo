using System;
using System.Collections;
using System.Collections.Generic;
using SO;
using UnityEngine;
using VisionConeDemo;

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

    private void Start()
    {
        if(settings == null)
            return;
        
        _visionConeComponent = GetComponent<VisionConeComponent>();
        _state = SecurityCameraState.Seeking;
        
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
        
        SetVisionConeValues();
    }

    void HandleTransition()
    {
        // Transition from one state to another. 
    }

    void HandleSeek()
    {
        float yawTarget = Mathf.Sin(Time.timeSinceLevelLoad * settings.rotationSpeed) * settings.rotationDegrees;
        var localRotation = transform.localRotation.eulerAngles;
        localRotation.y = yawTarget;
        
        pivotYaw.localRotation = Quaternion.Euler(localRotation);
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
                _visionConeComponent.Radius = settings.radiusSeeking;
                _visionConeComponent.FovDegrees = settings.fovDegreeSeeking;
                _visionConeComponent.currentColor = settings.colorSeeking;
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
