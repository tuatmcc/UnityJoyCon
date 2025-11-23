using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityJoycon;

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

    private SwitchJoyConHID _joyCon;

    private void Awake()
    {
        _joyCon = SwitchJoyConHID.all.First(joyCon => joyCon.Side == Side.Left);
    }

    private void Update()
    {
        if (_joyCon == null) return;

        buttonRight.material.color = _joyCon.dpad.right.IsPressed() ? Color.green : Color.black;
        buttonDown.material.color = _joyCon.dpad.down.IsPressed() ? Color.green : Color.black;
        buttonUp.material.color = _joyCon.dpad.up.IsPressed() ? Color.green : Color.black;
        buttonLeft.material.color = _joyCon.dpad.left.IsPressed() ? Color.green : Color.black;
        buttonL.material.color = _joyCon.leftShoulder.IsPressed() ? Color.green : Color.black;
        buttonZL.material.color = _joyCon.leftTrigger.IsPressed() ? Color.green : Color.black;
        buttonSL.material.color = _joyCon.leftSmallLeftShoulder.IsPressed() ? Color.green : Color.black;
        buttonSR.material.color = _joyCon.leftSmallRightShoulder.IsPressed() ? Color.green : Color.black;
        buttonMinus.material.color = _joyCon.selectButton.IsPressed() ? Color.green : Color.black;
        buttonCapture.material.color = _joyCon.captureButton.IsPressed() ? Color.green : Color.black;
        buttonStick.material.color = _joyCon.leftStickButton.IsPressed() ? Color.green : Color.black;

        var leftStick = _joyCon.leftStick.ReadValue();
        stickAxis.localPosition = new Vector3(leftStick.x * 0.5f, stickAxis.localPosition.y, leftStick.y * 0.5f);

        var accel = _joyCon.accelerometer.ReadValue();
        var gyro = _joyCon.gyroscope.ReadValue();

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