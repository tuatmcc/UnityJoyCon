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

        upButton.material.color = _joycon.GetButton(JoyConButton.DpadUp) ? Color.green : Color.black;
        downButton.material.color = _joycon.GetButton(JoyConButton.DpadDown) ? Color.green : Color.black;
        leftButton.material.color = _joycon.GetButton(JoyConButton.DpadLeft) ? Color.green : Color.black;
        rightButton.material.color = _joycon.GetButton(JoyConButton.DpadRight) ? Color.green : Color.black;
        slButton.material.color = _joycon.GetButton(JoyConButton.SL) ? Color.green : Color.black;
        srButton.material.color = _joycon.GetButton(JoyConButton.SR) ? Color.green : Color.black;
        if (_joycon.Type == JoyConType.Right)
        {
            rOrLButton.material.color = _joycon.GetButton(JoyConButton.R) ? Color.green : Color.black;
            zrOrZlButton.material.color = _joycon.GetButton(JoyConButton.ZR) ? Color.green : Color.black;
            plusOrMinusButton.material.color = _joycon.GetButton(JoyConButton.Plus) ? Color.green : Color.black;
            stickButton.material.color = _joycon.GetButton(JoyConButton.StickRight) ? Color.green : Color.black;
            homeOrCaptureButton.material.color = _joycon.GetButton(JoyConButton.Home) ? Color.green : Color.black;
        }
        else
        {
            rOrLButton.material.color = _joycon.GetButton(JoyConButton.L) ? Color.green : Color.black;
            zrOrZlButton.material.color = _joycon.GetButton(JoyConButton.ZL) ? Color.green : Color.black;
            plusOrMinusButton.material.color = _joycon.GetButton(JoyConButton.Minus) ? Color.green : Color.black;
            stickButton.material.color = _joycon.GetButton(JoyConButton.StickLeft) ? Color.green : Color.black;
            homeOrCaptureButton.material.color = _joycon.GetButton(JoyConButton.Capture) ? Color.green : Color.black;
        }
    }

    private void OnDestroy()
    {
        _joycon?.Dispose();
        _device?.Dispose();
        _hidapi.Dispose();
    }
}