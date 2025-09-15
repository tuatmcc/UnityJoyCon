using TMPro;
using UnityEngine;
using UnityJoycon;

public class SampleJoyCon : MonoBehaviour
{
    [SerializeField] private Renderer upButton;
    [SerializeField] private Renderer downButton;
    [SerializeField] private Renderer leftButton;
    [SerializeField] private Renderer rightButton;
    [SerializeField] private Renderer slButton;
    [SerializeField] private Renderer srButton;
    [SerializeField] private Renderer rOrLButton;
    [SerializeField] private Renderer zrOrZlButton;
    [SerializeField] private Renderer plusOrMinusButton;
    [SerializeField] private Renderer stickButton;
    [SerializeField] private Renderer homeOrCaptureButton;
    [SerializeField] private Transform stickAxis;
    [SerializeField] private TMP_Text accText;
    [SerializeField] private TMP_Text gyroText;

    private HidDevice _device;
    private Hidapi _hidapi;
    private JoyCon _joycon;

    private void Awake()
    {
        _hidapi = new Hidapi();
        var deviceInfos = _hidapi.GetDevices(0x057e);
        if (deviceInfos.Count == 0)
        {
            Debug.LogError("No Hidapi devices found");
            return;
        }

        var deviceInfo = deviceInfos[0];
        _device = _hidapi.OpenDevice(deviceInfo);
        Debug.Log($"Opened device: {deviceInfo.ProductString} ({deviceInfo.VendorId:X4}:{deviceInfo.ProductId:X4})");

        _joycon = new JoyCon(_device);
    }

    private void Update()
    {
        if (_joycon == null) return;
        _joycon.Update();
        if (_joycon.State == null) return;

        upButton.material.color = _joycon.State.GetButtonRaw(ButtonRaw.X) || _joycon.State.GetButtonRaw(ButtonRaw.Up)
            ? Color.green
            : Color.black;
        downButton.material.color =
            _joycon.State.GetButtonRaw(ButtonRaw.B) || _joycon.State.GetButtonRaw(ButtonRaw.Down)
                ? Color.green
                : Color.black;
        leftButton.material.color =
            _joycon.State.GetButtonRaw(ButtonRaw.Y) || _joycon.State.GetButtonRaw(ButtonRaw.Left)
                ? Color.green
                : Color.black;
        rightButton.material.color =
            _joycon.State.GetButtonRaw(ButtonRaw.A) || _joycon.State.GetButtonRaw(ButtonRaw.Right)
                ? Color.green
                : Color.black;
        slButton.material.color = _joycon.State.GetButtonRaw(ButtonRaw.LSL) || _joycon.State.GetButtonRaw(ButtonRaw.RSL)
            ? Color.green
            : Color.black;
        srButton.material.color = _joycon.State.GetButtonRaw(ButtonRaw.LSR) || _joycon.State.GetButtonRaw(ButtonRaw.RSR)
            ? Color.green
            : Color.black;
        rOrLButton.material.color = _joycon.State.GetButtonRaw(ButtonRaw.R) || _joycon.State.GetButtonRaw(ButtonRaw.L)
            ? Color.green
            : Color.black;
        zrOrZlButton.material.color =
            _joycon.State.GetButtonRaw(ButtonRaw.ZL) || _joycon.State.GetButtonRaw(ButtonRaw.ZR)
                ? Color.green
                : Color.black;
        plusOrMinusButton.material.color =
            _joycon.State.GetButtonRaw(ButtonRaw.Minus) || _joycon.State.GetButtonRaw(ButtonRaw.Plus)
                ? Color.green
                : Color.black;
        stickButton.material.color =
            _joycon.State.GetButtonRaw(ButtonRaw.LStick) || _joycon.State.GetButtonRaw(ButtonRaw.RStick)
                ? Color.green
                : Color.black;
        homeOrCaptureButton.material.color = _joycon.State.GetButtonRaw(ButtonRaw.Capture) ||
                                             _joycon.State.GetButtonRaw(ButtonRaw.Home)
            ? Color.green
            : Color.black;

        stickAxis.localPosition = new Vector3(_joycon.State.Stick.X * 0.5f, stickAxis.localPosition.y,
            _joycon.State.Stick.Y * -0.5f);

        var imuSample = _joycon.State.ImuSamples[0];
        accText.SetText($"Acc: {imuSample.Acc.X:F2}, {imuSample.Acc.Y:F2}, {imuSample.Acc.Z:F2}");
        gyroText.SetText($"Gyro: {imuSample.Gyro.X:F2}, {imuSample.Gyro.Y:F2}, {imuSample.Gyro.Z:F2}");
    }

    private void OnDestroy()
    {
        _joycon?.Dispose();
        _device?.Dispose();
        _hidapi.Dispose();
    }
}