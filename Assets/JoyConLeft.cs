using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityJoyCon;

public class JoyConLeft : MonoBehaviour
{
    [SerializeField] private Renderer buttonRight;
    [SerializeField] private Renderer buttonDown;
    [SerializeField] private Renderer buttonUp;
    [SerializeField] private Renderer buttonLeft;
    [SerializeField] private Renderer buttonL;
    [SerializeField] private Renderer buttonZL;
    [SerializeField] private Renderer buttonSL;
    [SerializeField] private Renderer buttonSR;
    [SerializeField] private Renderer buttonMinus;
    [SerializeField] private Renderer buttonCapture;
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

    private SwitchJoyConLeftHID _joyConLeft;

    private void Awake()
    {
        _joyConLeft = SwitchJoyConLeftHID.all.FirstOrDefault();
    }

    private void Update()
    {
        if (_joyConLeft == null) return;

        buttonRight.material.color = _joyConLeft.dpad.right.IsPressed() ? Color.green : Color.black;
        buttonDown.material.color = _joyConLeft.dpad.down.IsPressed() ? Color.green : Color.black;
        buttonUp.material.color = _joyConLeft.dpad.up.IsPressed() ? Color.green : Color.black;
        buttonLeft.material.color = _joyConLeft.dpad.left.IsPressed() ? Color.green : Color.black;
        buttonL.material.color = _joyConLeft.leftShoulder.IsPressed() ? Color.green : Color.black;
        buttonZL.material.color = _joyConLeft.leftTrigger.IsPressed() ? Color.green : Color.black;
        buttonSL.material.color = _joyConLeft.smallLeftShoulder.IsPressed() ? Color.green : Color.black;
        buttonSR.material.color = _joyConLeft.smallRightShoulder.IsPressed() ? Color.green : Color.black;
        buttonMinus.material.color = _joyConLeft.selectButton.IsPressed() ? Color.green : Color.black;
        buttonCapture.material.color = _joyConLeft.captureButton.IsPressed() ? Color.green : Color.black;
        buttonStick.material.color = _joyConLeft.leftStickButton.IsPressed() ? Color.green : Color.black;

        var leftStick = _joyConLeft.leftStick.ReadValue();
        stickAxis.localPosition = new Vector3(leftStick.x * 0.5f, stickAxis.localPosition.y, leftStick.y * 0.5f);

        var accel = _joyConLeft.accelerometer.ReadValue();
        var gyro = _joyConLeft.gyroscope.ReadValue();

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
    }
}
