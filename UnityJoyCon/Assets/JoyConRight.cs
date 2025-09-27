using TMPro;
using UnityEngine;
using UnityJoycon;
using UnityJoycon.Hidapi;

public class JoyConRight : MonoBehaviour
{
    [SerializeField] private Renderer upButton;
    [SerializeField] private Renderer downButton;
    [SerializeField] private Renderer leftButton;
    [SerializeField] private Renderer rightButton;
    [SerializeField] private Renderer slButton;
    [SerializeField] private Renderer srButton;
    [SerializeField] private Renderer rButton;
    [SerializeField] private Renderer zrButton;
    [SerializeField] private Renderer plusButton;
    [SerializeField] private Renderer stickButton;
    [SerializeField] private Renderer homeButton;
    [SerializeField] private Transform stickAxis;
    [SerializeField] private TMP_Text accText;
    [SerializeField] private TMP_Text gyroText;

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
        var device = _hidapi.OpenDevice(deviceInfo);
        Debug.Log($"Opened device: {deviceInfo.ProductString} ({deviceInfo.VendorId:X4}:{deviceInfo.ProductId:X4})");

        _joycon = await JoyCon.CreateAsync(device);
    }

    private void Update()
    {
        if (_joycon == null || !_joycon.TryGetState(out var state)) return;

        upButton.material.color = state.IsButtonPressed(Button.X)
            ? Color.green
            : Color.black;
        downButton.material.color =
            state.IsButtonPressed(Button.B)
                ? Color.green
                : Color.black;
        leftButton.material.color =
            state.IsButtonPressed(Button.Y)
                ? Color.green
                : Color.black;
        rightButton.material.color =
            state.IsButtonPressed(Button.A)
                ? Color.green
                : Color.black;
        slButton.material.color =
            state.IsButtonPressed(Button.RSL)
                ? Color.green
                : Color.black;
        srButton.material.color =
            state.IsButtonPressed(Button.RSR)
                ? Color.green
                : Color.black;
        rButton.material.color = state.IsButtonPressed(Button.R) ? Color.green : Color.black;
        zrButton.material.color = state.IsButtonPressed(Button.ZR) ? Color.green : Color.black;
        plusButton.material.color = state.IsButtonPressed(Button.Plus) ? Color.green : Color.black;
        stickButton.material.color = state.IsButtonPressed(Button.RStick) ? Color.green : Color.black;
        homeButton.material.color = state.IsButtonPressed(Button.Home) ? Color.green : Color.black;

        stickAxis.localPosition = new Vector3(state.Stick.X * 0.5f, stickAxis.localPosition.y, state.Stick.Y * -0.5f);

        var lastSample = state.ImuSamples[^1];
        accText.SetText(
            $"Acc: {lastSample.Acceleration.X:F2}, {lastSample.Acceleration.Y:F2}, {lastSample.Acceleration.Z:F2}");
        gyroText.SetText(
            $"Gyro: {lastSample.Gyroscope.X:F2}, {lastSample.Gyroscope.Y:F2}, {lastSample.Gyroscope.Z:F2}");
    }

    private async void OnDestroy()
    {
        if (_joycon != null)
        {
            await _joycon.DisposeAsync();
            _joycon = null;
        }

        if (_hidapi != null)
        {
            _hidapi.Dispose();
            _hidapi = null;
        }
    }
}