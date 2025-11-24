using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityJoyCon
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct SwitchJoyConLeftHIDInputState : IInputStateTypeInfo
    {
        public static FourCC Type => new('S', 'J', 'L', 'S'); // Switch Joy-Con Left State
        public FourCC format => Type;

        [InputControl(name = "dpad", layout = "Dpad", format = "BIT", bit = (int)Button.DpadDown,
            sizeInBits = 4)]
        [InputControl(name = "dpad/down", bit = (int)Button.DpadDown)]
        [InputControl(name = "dpad/up", bit = (int)Button.DpadUp)]
        [InputControl(name = "dpad/right", bit = (int)Button.DpadRight)]
        [InputControl(name = "dpad/left", bit = (int)Button.DpadLeft)]
        [InputControl(name = "smallRightShoulder", displayName = "Left SR", layout = "Button",
            bit = (int)Button.LeftSR)]
        [InputControl(name = "smallLeftShoulder", displayName = "Left SL", layout = "Button",
            bit = (int)Button.LeftSL)]
        [InputControl(name = "leftShoulder", displayName = "L", layout = "Button", bit = (int)Button.L)]
        [InputControl(name = "leftTrigger", displayName = "ZL", layout = "Button", format = "BIT",
            bit = (int)Button.ZL)]
        [InputControl(name = "select", displayName = "Minus", layout = "Button", bit = (int)Button.Minus)]
        [InputControl(name = "capture", displayName = "Capture", layout = "Button", bit = (int)Button.Capture)]
        [InputControl(name = "leftStickPress", displayName = "Left Stick", layout = "Button",
            bit = (int)Button.LeftStick)]
        public uint buttons;

        [InputControl(name = "leftStick", layout = "Stick", format = "VEC2", displayName = "Left Stick")]
        public Vector2 leftStick;

        [InputControl(name = "accelerometer", layout = "Vector3", format = "VEC3", displayName = "Accelerometer")]
        public Vector3 accelerometer;

        [InputControl(name = "gyroscope", layout = "Vector3", format = "VEC3", displayName = "Gyroscope")]
        public Vector3 gyroscope;
    }

#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    [InputControlLayout(displayName = "Switch Joy-Con (L)", stateType = typeof(SwitchJoyConLeftHIDInputState))]
    public class SwitchJoyConLeftHID : SwitchJoyConHID
    {
        public override Side Side => Side.Left;

        // ReSharper disable InconsistentNaming
        public DpadControl dpad { get; private set; }
        public ButtonControl smallLeftShoulder { get; private set; }
        public ButtonControl smallRightShoulder { get; private set; }
        public ButtonControl leftShoulder { get; private set; }
        public ButtonControl leftTrigger { get; private set; }
        public ButtonControl selectButton { get; private set; }
        public ButtonControl captureButton { get; private set; }
        public ButtonControl leftStickButton { get; private set; }
        public StickControl leftStick { get; private set; }

        public new static SwitchJoyConLeftHID current { get; private set; }

        public new static IReadOnlyList<SwitchJoyConLeftHID> all => AllDevices;
        // ReSharper restore InconsistentNaming

        private static readonly List<SwitchJoyConLeftHID> AllDevices = new();

        static SwitchJoyConLeftHID()
        {
            InputSystem.RegisterLayout<SwitchJoyConLeftHID>(matches: new InputDeviceMatcher().WithInterface("HID")
                .WithCapability("vendorId", VendorId).WithCapability("productId", ProductIdLeft));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
        }

        protected override void OnAdded()
        {
            base.OnAdded();
            AllDevices.Add(this);
        }

        protected override void OnRemoved()
        {
            base.OnRemoved();
            AllDevices.Remove(this);
            if (current == this)
                current = null;
        }

        public override void MakeCurrent()
        {
            base.MakeCurrent();
            current = this;
        }

        protected override void FinishSetup()
        {
            base.FinishSetup();
            dpad = GetChildControl<DpadControl>("dpad");
            smallLeftShoulder = GetChildControl<ButtonControl>("smallLeftShoulder");
            smallRightShoulder = GetChildControl<ButtonControl>("smallRightShoulder");
            leftShoulder = GetChildControl<ButtonControl>("leftShoulder");
            leftTrigger = GetChildControl<ButtonControl>("leftTrigger");
            selectButton = GetChildControl<ButtonControl>("select");
            captureButton = GetChildControl<ButtonControl>("capture");
            leftStickButton = GetChildControl<ButtonControl>("leftStickPress");
            leftStick = GetChildControl<StickControl>("leftStick");
        }
    }
}
