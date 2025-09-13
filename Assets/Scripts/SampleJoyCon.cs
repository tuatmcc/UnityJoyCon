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

        upButton.material.color = _joycon.Button.DpadUp ? Color.green : Color.black;
        downButton.material.color = _joycon.Button.DpadDown ? Color.green : Color.black;
        leftButton.material.color = _joycon.Button.DpadLeft ? Color.green : Color.black;
        rightButton.material.color = _joycon.Button.DpadRight ? Color.green : Color.black;
        slButton.material.color = _joycon.Button.SL ? Color.green : Color.black;
        srButton.material.color = _joycon.Button.SR ? Color.green : Color.black;
        if (_joycon.Type == Type.Right)
        {
            rOrLButton.material.color = _joycon.Button.R ? Color.green : Color.black;
            zrOrZlButton.material.color = _joycon.Button.ZR ? Color.green : Color.black;
            plusOrMinusButton.material.color = _joycon.Button.Plus ? Color.green : Color.black;
            stickButton.material.color = _joycon.Button.StickR ? Color.green : Color.black;
            homeOrCaptureButton.material.color = _joycon.Button.Home ? Color.green : Color.black;
        }
        else
        {
            rOrLButton.material.color = _joycon.Button.L ? Color.green : Color.black;
            zrOrZlButton.material.color = _joycon.Button.ZL ? Color.green : Color.black;
            plusOrMinusButton.material.color = _joycon.Button.Minus ? Color.green : Color.black;
            stickButton.material.color = _joycon.Button.StickL ? Color.green : Color.black;
            homeOrCaptureButton.material.color = _joycon.Button.Capture ? Color.green : Color.black;
        }

        stickAxis.localPosition = new Vector3(_joycon.Stick.X * 0.5f, stickAxis.localPosition.y, _joycon.Stick.Y * -0.5f);
    }

    private void OnDestroy()
    {
        _joycon?.Dispose();
        _device?.Dispose();
        _hidapi.Dispose();
    }
}