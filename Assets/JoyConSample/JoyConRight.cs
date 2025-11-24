using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityJoyCon;

namespace JoyConSample
{
    public class JoyConRight : MonoBehaviour
    {
        [SerializeField] private Renderer buttonEast;
        [SerializeField] private Renderer buttonSouth;
        [SerializeField] private Renderer buttonNorth;
        [SerializeField] private Renderer buttonWest;
        [SerializeField] private Renderer buttonR;
        [SerializeField] private Renderer buttonZR;
        [SerializeField] private Renderer buttonSL;
        [SerializeField] private Renderer buttonSR;
        [SerializeField] private Renderer buttonPlus;
        [SerializeField] private Renderer buttonHome;
        [SerializeField] private Renderer buttonStick;
        [SerializeField] private Transform stickAxis;

        [SerializeField] private TMP_Text accelXText;
        [SerializeField] private Slider accelXSlider;
        [SerializeField] private TMP_Text accelYText;
        [SerializeField] private Slider accelYSlider;
        [SerializeField] private TMP_Text accelZText;
        [SerializeField] private Slider accelZSlider;

        [SerializeField] private TMP_Text gyroXText;
        [SerializeField] private Slider gyroXSlider;
        [SerializeField] private TMP_Text gyroYText;
        [SerializeField] private Slider gyroYSlider;
        [SerializeField] private TMP_Text gyroZText;
        [SerializeField] private Slider gyroZSlider;

        private SwitchJoyConRightHID _joyConRight;

        private void Awake()
        {
            _joyConRight = SwitchJoyConRightHID.all.FirstOrDefault();
        }

        private void Update()
        {
            if (_joyConRight == null) return;

            buttonEast.material.color = _joyConRight.buttonEast.IsPressed() ? Color.green : Color.black;
            buttonSouth.material.color = _joyConRight.buttonSouth.IsPressed() ? Color.green : Color.black;
            buttonNorth.material.color = _joyConRight.buttonNorth.IsPressed() ? Color.green : Color.black;
            buttonWest.material.color = _joyConRight.buttonWest.IsPressed() ? Color.green : Color.black;
            buttonR.material.color = _joyConRight.rightShoulder.IsPressed() ? Color.green : Color.black;
            buttonZR.material.color = _joyConRight.rightTrigger.IsPressed() ? Color.green : Color.black;
            buttonSL.material.color = _joyConRight.smallLeftShoulder.IsPressed() ? Color.green : Color.black;
            buttonSR.material.color = _joyConRight.smallRightShoulder.IsPressed() ? Color.green : Color.black;
            buttonPlus.material.color = _joyConRight.startButton.IsPressed() ? Color.green : Color.black;
            buttonHome.material.color = _joyConRight.homeButton.IsPressed() ? Color.green : Color.black;
            buttonStick.material.color = _joyConRight.rightStickButton.IsPressed() ? Color.green : Color.black;

            var rightStick = _joyConRight.rightStick.ReadValue();
            stickAxis.localPosition = new Vector3(rightStick.x * 0.5f, stickAxis.localPosition.y, rightStick.y * 0.5f);

            var accel = _joyConRight.accelerometer.ReadValue();
            var gyro = _joyConRight.gyroscope.ReadValue();

            accelXText.SetText($"{accel.x: 0.00;-0.00}");
            accelYText.SetText($"{accel.y: 0.00;-0.00}");
            accelZText.SetText($"{accel.z: 0.00;-0.00}");
            gyroXText.SetText($"{gyro.x: 000.00;-000.00}");
            gyroYText.SetText($"{gyro.y: 000.00;-000.00}");
            gyroZText.SetText($"{gyro.z: 000.00;-000.00}");
            accelXSlider.value = accel.x;
            accelYSlider.value = accel.y;
            accelZSlider.value = accel.z;
            gyroXSlider.value = gyro.x;
            gyroYSlider.value = gyro.y;
            gyroZSlider.value = gyro.z;

            if (_joyConRight.rightTrigger.wasPressedThisFrame) _joyConRight.SetMotorSpeeds(0.5f, 0.0f);
            if (_joyConRight.rightTrigger.wasReleasedThisFrame) _joyConRight.ResetHaptics();
        }
    }
}
