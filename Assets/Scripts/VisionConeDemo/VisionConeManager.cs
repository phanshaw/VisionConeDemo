using System;
using System.Collections.Generic;
using UnityEngine;

namespace VisionConeDemo
{
    public class VisionConeManager : MonoBehaviour
    {
        #region Singleton
        
        private static VisionConeManager _instance;
        public static VisionConeManager Get => _instance;

        public static bool IsValid => _instance != null;

        private Camera _visionConeCameraProxy;

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

        private Dictionary<int, VisionConeComponent> _visionConeComponents;
        private VisionConeData[] _visionConeData;
        
        public void Awake()
        {
            if (!CreateStaticInstance())
                return;

            _visionConeData = new VisionConeData[0];
            _visionConeComponents = new Dictionary<int, VisionConeComponent>();
            
            InitializeCameraProxy();
        }

        public void RegisterVisionCone(VisionConeComponent component)
        {
            var id = component.GetInstanceID();
            if (!_visionConeComponents.ContainsKey(id))
            {
                _visionConeComponents.Add(component.GetInstanceID(), component);
            }
        }

        public void DeregisterVisionCone(VisionConeComponent component)
        {
            var id = component.GetInstanceID();
            if (_visionConeComponents.ContainsKey(id))
            {
                _visionConeComponents.Remove(id);
            }
        }

        public VisionConeData[] GetVisionConeData()
        {
            var count = _visionConeComponents.Count;
            _visionConeData = new VisionConeData[count];
            if (count == 0)
                return _visionConeData;

            var counter = 0;
            foreach (var visionConeComponent in _visionConeComponents)
            {
                _visionConeData[counter] = visionConeComponent.Value.GetData();
                counter++;
            }
            return _visionConeData;
        }
        
        private void InitializeCameraProxy()
        {
            var root = new GameObject("VisionConeCameraProxy");
            root.transform.parent = transform;
            _visionConeCameraProxy = root.AddComponent<Camera>();
            _visionConeCameraProxy.enabled = false;
        }

        public Camera GetVisionConeProxyCamera()
        {
            return _visionConeCameraProxy;
        }

        private void OnDestroy()
        {
            if (_visionConeCameraProxy)
            {
                Destroy(_visionConeCameraProxy.gameObject);
            }
        }
    }
}
