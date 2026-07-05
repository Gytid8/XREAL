using UnityEngine;
using UnityEngine.XR;

namespace Unity.XR.XREAL.DrawingViewer
{
    /// <summary>
    /// Controller button identifiers mapped to XREAL controller layout.
    /// </summary>
    public enum ControllerButton
    {
        APP,
        HOME,
        TRIGGER,
        GRIP,
        MENU,
        PRIMARY,
        SECONDARY
    }

    /// <summary>
    /// Convenience wrapper around the XREAL controller input.
    /// Provides simplified API for button state, touchpad, pose, and scrolling.
    /// </summary>
    public class XREALInput : SingletonMonoBehaviour<XREALInput>
    {
        private InputDevice _controller;
        private bool _controllerFound;

        private bool _isTouching;
        private bool _wasTouching;
        private Vector3 _devicePosition;
        private Quaternion _deviceRotation = Quaternion.identity;
        private bool _hasPose;
        private Vector2 _touchPosition;
        private Vector2 _previousTouchPosition;
        private Vector2 _deltaTouch;
        private bool _isTouchScrollStart;

        private readonly bool[] _buttonStates = new bool[16];
        private readonly bool[] _buttonDownStates = new bool[16];
        private readonly bool[] _buttonUpStates = new bool[16];

        public static new XREALInput CreateSingleton()
        {
            if (Singleton != null) return Singleton;
            var go = new GameObject("XREALInput");
            return go.AddComponent<XREALInput>();
        }

        private void Start()
        {
            FindController();
        }

        private void FindController()
        {
            _controllerFound = false;
            var devices = new System.Collections.Generic.List<InputDevice>();
            InputDevices.GetDevices(devices);

            foreach (var device in devices)
            {
                string deviceName = device.name ?? string.Empty;
                if (device.characteristics.HasFlag(InputDeviceCharacteristics.Controller) &&
                    (deviceName.Contains("XREAL") || deviceName.Contains("Controller")))
                {
                    _controller = device;
                    _controllerFound = true;
                    Debug.Log($"[XREALInput] Found controller: {deviceName}");
                    return;
                }
            }

            foreach (var device in devices)
            {
                if (device.characteristics.HasFlag(InputDeviceCharacteristics.Controller))
                {
                    _controller = device;
                    _controllerFound = true;
                    Debug.Log($"[XREALInput] Using fallback controller: {_controller.name}");
                    return;
                }
            }
        }

        private void Update()
        {
            if (!_controllerFound || !_controller.isValid)
            {
                FindController();
                _isTouching = false;
                _hasPose = false;
                return;
            }

            _wasTouching = _isTouching;
            _previousTouchPosition = _touchPosition;

            for (int i = 0; i < _buttonDownStates.Length; i++)
                _buttonDownStates[i] = false;
            for (int i = 0; i < _buttonUpStates.Length; i++)
                _buttonUpStates[i] = false;

            if (_controller.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis))
            {
                _touchPosition = axis;
                _isTouching = axis.sqrMagnitude > 0.0001f;
            }
            else
            {
                _isTouching = false;
                _touchPosition = Vector2.zero;
            }

            _deltaTouch = _touchPosition - _previousTouchPosition;
            _isTouchScrollStart = _isTouching && !_wasTouching;

            _hasPose = _controller.TryGetFeatureValue(CommonUsages.devicePosition, out _devicePosition)
                       && _controller.TryGetFeatureValue(CommonUsages.deviceRotation, out _deviceRotation);

            ReadButtonState(0, CommonUsages.primaryButton);
            ReadButtonState(1, CommonUsages.secondaryButton);
            ReadButtonState(2, CommonUsages.triggerButton);
            ReadButtonState(3, CommonUsages.gripButton);
            ReadButtonState(4, CommonUsages.menuButton);
        }

        private void ReadButtonState(int index, InputFeatureUsage<bool> usage)
        {
            if (_controller.TryGetFeatureValue(usage, out bool pressed))
            {
                _buttonDownStates[index] = pressed && !_buttonStates[index];
                _buttonUpStates[index] = !pressed && _buttonStates[index];
                _buttonStates[index] = pressed;
            }
        }

        public bool IsTouching() => _isTouching;
        public bool IsTouchScrolling() => _isTouching && _wasTouching;
        public bool IsTouchScrollStart() => _isTouchScrollStart;
        public Vector2 GetDeltaTouch() => _deltaTouch;
        public Vector2 GetTouchPosition() => _touchPosition;

        public bool TryGetPose(out Vector3 position, out Quaternion rotation)
        {
            position = _devicePosition;
            rotation = _deviceRotation;
            return _hasPose;
        }

        public Vector3 GetPosition() => _devicePosition;
        public Quaternion GetRotation() => _deviceRotation;

        public bool GetButtonDown(ControllerButton button)
        {
            int idx = ButtonToIndex(button);
            return idx >= 0 && _buttonDownStates[idx];
        }

        public bool GetButtonUp(ControllerButton button)
        {
            int idx = ButtonToIndex(button);
            return idx >= 0 && _buttonUpStates[idx];
        }

        public bool GetButton(ControllerButton button)
        {
            int idx = ButtonToIndex(button);
            return idx >= 0 && _buttonStates[idx];
        }

        private static int ButtonToIndex(ControllerButton button)
        {
            switch (button)
            {
                case ControllerButton.PRIMARY: return 0;
                case ControllerButton.SECONDARY: return 1;
                case ControllerButton.TRIGGER: return 2;
                case ControllerButton.GRIP: return 3;
                case ControllerButton.APP:
                case ControllerButton.MENU: return 4;
                case ControllerButton.HOME: return 1;
                default: return -1;
            }
        }
    }
}
