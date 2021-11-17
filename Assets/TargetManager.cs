using UnityEngine;

// Updating the mouse pos here so we only do it once. 

public class TargetManager : MonoBehaviour
{
    #region Singleton
        
    private static TargetManager _instance;
    public static TargetManager Get => _instance;

    public static bool IsValid => _instance != null;

    private bool CreateStaticInstance()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return false;
        }

        _instance = this;
        return true;
    }
        
    #endregion
    
    public Transform _mouseObject;
    public Camera SceneCamera;
    
    private Vector3 _mousePosWS;
    public Vector3 MousePosWS;

    private void Awake()
    {
        if (!CreateStaticInstance())
            return;
        
        
    }

    private void Update()
    {
        var mousePos = Input.mousePosition;
        var ray = SceneCamera.ScreenPointToRay(mousePos);
        
        // Ignore the player layer
        var playerLayer = ~(1 << LayerMask.NameToLayer("Player"));
        if (Physics.Raycast(ray, out RaycastHit hitInfo, 1000.0f, playerLayer ))
        {
            MousePosWS = hitInfo.point;
            if (_mouseObject != null)
            {
                _mouseObject.transform.SetPositionAndRotation(MousePosWS, Quaternion.identity);
            }
        }
    }
}
