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
    internal struct SwitchJoyConRightHIDInputState : IInputStateTypeInfo
    {
        public static FourCC Type => new('S', 'J', 'R', 'S'); // Switch Joy-Con Right State
        public FourCC format => Type;

        [InputControl(name = "buttonWest", displayName = "Y", layout = "Button", bit = (int)Button.Y)]
        [InputControl(name = "buttonNorth", displayName = "X", layout = "Button", bit = (int)Button.X)]
        [InputControl(name = "buttonSouth", displayName = "B", layout = "Button", bit = (int)Button.B)]
        [InputControl(name = "buttonEast", displayName = "A", layout = "Button", bit = (int)Button.A)]
        [InputControl(name = "smallRightShoulder", displayName = "Right SR", layout = "Button",
            bit = (int)Button.RightSR)]
        [InputControl(name = "smallLeftShoulder", displayName = "Right SL", layout = "Button",
            bit = (int)Button.RightSL)]
        [InputControl(name = "rightShoulder", displayName = "R", layout = "Button", bit = (int)Button.R)]
        [InputControl(name = "rightTrigger", displayName = "ZR", layout = "Button", format = "BIT",
            bit = (int)Button.ZR)]
        [InputControl(name = "start", displayName = "Plus", layout = "Button", bit = (int)Button.Plus)]
        [InputControl(name = "home", displayName = "Home", layout = "Button", bit = (int)Button.Home)]
        [InputControl(name = "rightStickPress", displayName = "Right Stick", layout = "Button",
            bit = (int)Button.RightStick)]
        public uint buttons;

        [InputControl(name = "rightStick", layout = "Stick", format = "VEC2", displayName = "Right Stick")]
        public Vector2 rightStick;

        [InputControl(name = "accelerometer", layout = "Vector3", format = "VEC3", displayName = "Accelerometer")]
        public Vector3 accelerometer;

        [InputControl(name = "gyroscope", layout = "Vector3", format = "VEC3", displayName = "Gyroscope")]
        public Vector3 gyroscope;
    }

#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    [InputControlLayout(displayName = "Switch Joy-Con (R)", stateType = typeof(SwitchJoyConRightHIDInputState))]
    public class SwitchJoyConRightHID : SwitchJoyConHID
    {
        public override Side Side => Side.Right;

        // ReSharper disable InconsistentNaming
        public ButtonControl buttonWest { get; private set; }
        public ButtonControl buttonNorth { get; private set; }
        public ButtonControl buttonSouth { get; private set; }
        public ButtonControl buttonEast { get; private set; }
        public ButtonControl smallLeftShoulder { get; private set; }
        public ButtonControl smallRightShoulder { get; private set; }
        public ButtonControl rightShoulder { get; private set; }
        public ButtonControl rightTrigger { get; private set; }
        public ButtonControl startButton { get; private set; }
        public ButtonControl homeButton { get; private set; }
        public ButtonControl rightStickButton { get; private set; }
        public StickControl rightStick { get; private set; }

        public new static SwitchJoyConRightHID current { get; private set; }

        public new static IReadOnlyList<SwitchJoyConRightHID> all => AllDevices;
        // ReSharper restore InconsistentNaming

        private static readonly List<SwitchJoyConRightHID> AllDevices = new();

        static SwitchJoyConRightHID()
        {
            InputSystem.RegisterLayout<SwitchJoyConRightHID>(matches: new InputDeviceMatcher().WithInterface("HID")
                .WithCapability("vendorId", VendorId).WithCapability("productId", ProductIdRight));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
        }

        protected override void FinishSetup()
        {
            base.FinishSetup();
            buttonWest = GetChildControl<ButtonControl>("buttonWest");
            buttonNorth = GetChildControl<ButtonControl>("buttonNorth");
            buttonSouth = GetChildControl<ButtonControl>("buttonSouth");
            buttonEast = GetChildControl<ButtonControl>("buttonEast");
            smallLeftShoulder = GetChildControl<ButtonControl>("smallLeftShoulder");
            smallRightShoulder = GetChildControl<ButtonControl>("smallRightShoulder");
            rightShoulder = GetChildControl<ButtonControl>("rightShoulder");
            rightTrigger = GetChildControl<ButtonControl>("rightTrigger");
            startButton = GetChildControl<ButtonControl>("start");
            homeButton = GetChildControl<ButtonControl>("home");
            rightStickButton = GetChildControl<ButtonControl>("rightStickPress");
            rightStick = GetChildControl<StickControl>("rightStick");
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
    }
}
