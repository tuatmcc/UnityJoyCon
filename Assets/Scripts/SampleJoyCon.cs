using TMPro;
using UnityEngine;
using UnityJoycon;
using UnityJoycon.Hidapi;

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

    private async void Awake()
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

        _joycon = await JoyCon.CreateAsync(_device);
    }

    private void Update()
    {
        if (_joycon == null || !_joycon.TryGetState(out var state)) return;

        upButton.material.color = state.IsButtonPressed(Button.X) || state.IsButtonPressed(Button.Up)
            ? Color.green
            : Color.black;
        downButton.material.color =
            state.IsButtonPressed(Button.B) || state.IsButtonPressed(Button.Down)
                ? Color.green
                : Color.black;
        leftButton.material.color =
            state.IsButtonPressed(Button.Y) || state.IsButtonPressed(Button.Left)
                ? Color.green
                : Color.black;
        rightButton.material.color =
            state.IsButtonPressed(Button.A) || state.IsButtonPressed(Button.Right)
                ? Color.green
                : Color.black;
        slButton.material.color = state.IsButtonPressed(Button.LSL) || state.IsButtonPressed(Button.RSL)
            ? Color.green
            : Color.black;
        srButton.material.color = state.IsButtonPressed(Button.LSR) || state.IsButtonPressed(Button.RSR)
            ? Color.green
            : Color.black;
        rOrLButton.material.color = state.IsButtonPressed(Button.R) || state.IsButtonPressed(Button.L)
            ? Color.green
            : Color.black;
        zrOrZlButton.material.color =
            state.IsButtonPressed(Button.ZL) || state.IsButtonPressed(Button.ZR)
                ? Color.green
                : Color.black;
        plusOrMinusButton.material.color =
            state.IsButtonPressed(Button.Minus) || state.IsButtonPressed(Button.Plus)
                ? Color.green
                : Color.black;
        stickButton.material.color =
            state.IsButtonPressed(Button.LStick) || state.IsButtonPressed(Button.RStick)
                ? Color.green
                : Color.black;
        homeOrCaptureButton.material.color = state.IsButtonPressed(Button.Capture) ||
                                             state.IsButtonPressed(Button.Home)
            ? Color.green
            : Color.black;

        stickAxis.localPosition = new Vector3(state.Stick.X * 0.5f, stickAxis.localPosition.y,
            state.Stick.Y * -0.5f);

        var imuSample = state.ImuSamples[0];
        accText.SetText(
            $"Acc: {imuSample.Acceleration.X:F2}, {imuSample.Acceleration.Y:F2}, {imuSample.Acceleration.Z:F2}");
        gyroText.SetText($"Gyro: {imuSample.Gyroscope.X:F2}, {imuSample.Gyroscope.Y:F2}, {imuSample.Gyroscope.Z:F2}");
    }

    private async void OnDestroy()
    {
        if (_joycon != null) await _joycon.DisposeAsync();
        _hidapi.Dispose();
    }
}