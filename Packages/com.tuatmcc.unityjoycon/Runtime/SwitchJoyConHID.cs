using System;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.HID;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace UnityJoycon
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct SwitchJoyConHIDInputState : IInputStateTypeInfo
    {
        public static FourCC Type => new('S', 'J', 'V', 'S'); // Switch Joy-Con Virtual State
        public FourCC format => Type;

        [InputControl(name = "buttonWest", displayName = "Y", layout = "Button", bit = (int)Button.Y)]
        [InputControl(name = "buttonNorth", displayName = "X", layout = "Button", bit = (int)Button.X)]
        [InputControl(name = "buttonSouth", displayName = "B", layout = "Button", bit = (int)Button.B)]
        [InputControl(name = "buttonEast", displayName = "A", layout = "Button", bit = (int)Button.A)]
        [InputControl(name = "rightSmallRightShoulder", displayName = "Right SR", layout = "Button",
            bit = (int)Button.RightSR)]
        [InputControl(name = "rightSmallLeftShoulder", displayName = "Right SL", layout = "Button",
            bit = (int)Button.RightSL)]
        [InputControl(name = "rightShoulder", displayName = "R", layout = "Button", bit = (int)Button.R)]
        [InputControl(name = "rightTrigger", displayName = "ZR", layout = "Button", format = "BIT",
            bit = (int)Button.ZR)]
        [InputControl(name = "start", displayName = "Plus", layout = "Button", bit = (int)Button.Plus)]
        [InputControl(name = "select", displayName = "Minus", layout = "Button", bit = (int)Button.Minus)]
        [InputControl(name = "rightStickPress", displayName = "Right Stick", layout = "Button",
            bit = (int)Button.RightStick)]
        [InputControl(name = "leftStickPress", displayName = "Left Stick", layout = "Button",
            bit = (int)Button.LeftStick)]
        [InputControl(name = "home", displayName = "Home", layout = "Button", bit = (int)Button.Home)]
        [InputControl(name = "capture", displayName = "Capture", layout = "Button", bit = (int)Button.Capture)]
        [InputControl(name = "dpad", format = "BIT", bit = (int)Button.DpadDown, sizeInBits = 4)]
        [InputControl(name = "dpad/down", bit = (int)Button.DpadDown)]
        [InputControl(name = "dpad/up", bit = (int)Button.DpadUp)]
        [InputControl(name = "dpad/right", bit = (int)Button.DpadRight)]
        [InputControl(name = "dpad/left", bit = (int)Button.DpadLeft)]
        [InputControl(name = "leftSmallLeftShoulder", displayName = "Left SL", layout = "Button",
            bit = (int)Button.LeftSL)]
        [InputControl(name = "leftSmallRightShoulder", displayName = "Left SR", layout = "Button",
            bit = (int)Button.LeftSR)]
        [InputControl(name = "leftShoulder", displayName = "L", layout = "Button", bit = (int)Button.L)]
        [InputControl(name = "leftTrigger", displayName = "ZL", layout = "Button", format = "BIT",
            bit = (int)Button.ZL)]
        public uint buttons;

        [InputControl(name = "rightStick", layout = "Stick", format = "VEC2", displayName = "Right Stick")]
        public Vector2 rightStick;

        public enum Button
        {
            Y = 0,
            X = 1,
            B = 2,
            A = 3,
            RightSR = 4,
            RightSL = 5,
            R = 6,
            ZR = 7,

            Minus = 8,
            Plus = 9,
            RightStick = 10,
            LeftStick = 11,
            Home = 12,
            Capture = 13,
            ChargeGrip = 15,

            DpadDown = 16,
            DpadUp = 17,
            DpadRight = 18,
            DpadLeft = 19,
            LeftSL = 20,
            LeftSR = 21,
            L = 22,
            ZL = 23
        }

        public void Set(Button button, bool state)
        {
            if (state)
                buttons |= (uint)(1 << (int)button);
            else
                buttons &= ~(uint)(1 << (int)button);
        }
    }

#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    [InputControlLayout(displayName = "Switch Joy-Con", stateType = typeof(SwitchJoyConHIDInputState))]
    public class SwitchJoyConHID : Gamepad, IInputStateCallbackReceiver
    {
        private const int VendorId = 0x057e;
        private const int ProductIdLeft = 0x2006;
        private const int ProductIdRight = 0x2007;

        static SwitchJoyConHID()
        {
            InputSystem.RegisterLayout<SwitchJoyConHID>(matches: new InputDeviceMatcher().WithInterface("HID")
                .WithCapability("vendorId", VendorId).WithCapability("productId", ProductIdLeft));
            InputSystem.RegisterLayout<SwitchJoyConHID>(matches: new InputDeviceMatcher().WithInterface("HID")
                .WithCapability("vendorId", VendorId).WithCapability("productId", ProductIdRight));
        }

        // ランタイムでスタティックコンストラクタを実行するためのダミーメソッド
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
        }

        private Side _side;

        // ReSharper disable once InconsistentNaming
        [InputControl(name = "capture", displayName = "Capture")]
        public ButtonControl captureButton { get; protected set; }

        // ReSharper disable once InconsistentNaming
        [InputControl(name = "home", displayName = "Home")]
        public ButtonControl homeButton { get; protected set; }

        private ushort _stickDeadZone;
        private ushort _stickCenterX;
        private ushort _stickMinX;
        private ushort _stickMaxX;
        private ushort _stickCenterY;
        private ushort _stickMinY;
        private ushort _stickMaxY;

        private bool _stickCalibrationDataLoaded;
        private bool _stickUserCalibrationDataLoaded;
        private bool _stickParametersLoaded;

        private double _lastCommandSentTime;

        unsafe void IInputStateCallbackReceiver.OnStateEvent(InputEventPtr eventPtr)
        {
            // Delta eventは無視する
            if (eventPtr.type != StateEvent.Type) return;

            var stateEvent = StateEvent.From(eventPtr);
            // HID以外のイベントは無視する
            if (stateEvent->stateFormat != SwitchHIDGenericInputReport.Format) return;

            // 汎用レポートに変換
            var genericReport = (SwitchHIDGenericInputReport*)stateEvent->state;

            // 標準レポート(IMU)の場合
            if (genericReport->reportId == 0x30)
            {
                // スティックのキャリブレーションデータとパラメータが読み込まれていない場合は無視する
                if (!_stickCalibrationDataLoaded || !_stickParametersLoaded) return;
                var data = ((SwitchStandardInputReport*)stateEvent->state)->ToHIDInputReport(
                    _stickDeadZone,
                    _stickCenterX,
                    _stickMinX, _stickMaxX,
                    _stickCenterY,
                    _stickMinY, _stickMaxY);
                InputState.Change(this, data, eventPtr: eventPtr);
            }

            // 標準レポート(サブコマンド応答)の場合
            if (genericReport->reportId == 0x21)
            {
                var data = (SwitchStandardInputReport*)stateEvent->state;
                // 否定応答の場合はエラーログ
                if ((data->subCommandReply.ack & 0x80) == 0)
                {
                    Debug.LogError($"Joy-Con sub command 0x{data->subCommandReply.subCommandId:X2} NAK received.");
                    return;
                }

                // SPIフラッシュ読み出し応答の場合
                if (data->subCommandReply.subCommandId == 0x10)
                {
                    var address = data->subCommandReply.data[0] |
                                  ((uint)data->subCommandReply.data[1] << 8) |
                                  ((uint)data->subCommandReply.data[2] << 16) |
                                  ((uint)data->subCommandReply.data[3] << 24);
                    var length = data->subCommandReply.data[4];
                    var payload = data->subCommandReply.data + 5;

                    // スティックパラメータの場合
                    if (address == (uint)GetStickParametersAddress() && length == 18)
                    {
                        _stickDeadZone = (ushort)(((payload[4] << 8) & 0xf00) | payload[3]);
                        _stickParametersLoaded = true;
                    }

                    // スティックキャリブレーションデータの場合
                    // ユーザーデータ優先で読み込む
                    if (address == (uint)GetStickUserCalibrationAddress() && length == 9)
                    {
                        _stickUserCalibrationDataLoaded = true;
                        // 全て0xFFの場合はユーザーデータが未設定とみなす
                        var hasUserData = payload[0] != 0xFF || payload[1] != 0xFF || payload[2] != 0xFF ||
                                          payload[3] != 0xFF || payload[4] != 0xFF || payload[5] != 0xFF ||
                                          payload[6] != 0xFF || payload[7] != 0xFF || payload[8] != 0xFF;
                        if (!hasUserData) return;

                        // TODO: 左スティックの場合はパラメータの順番が異なるため修正が必要
                        _stickCenterX = (ushort)(((payload[1] << 8) & 0xf00) | payload[0]);
                        _stickCenterY = (ushort)((payload[2] << 4) | (payload[1] >> 4));
                        _stickMinX = (ushort)(((payload[4] << 8) & 0xf00) | payload[3]);
                        _stickMinY = (ushort)((payload[5] << 4) | (payload[4] >> 4));
                        _stickMaxX = (ushort)(((payload[7] << 8) & 0xf00) | payload[6]);
                        _stickMaxY = (ushort)((payload[8] << 4) | (payload[7] >> 4));
                        _stickCalibrationDataLoaded = true;
                    }

                    // 工場出荷時データの場合
                    if (address == (uint)GetStickFactoryCalibrationAddress() && length == 9)
                    {
                        // TODO: 左スティックの場合はパラメータの順番が異なるため修正が必要
                        _stickCenterX = (ushort)(((payload[1] << 8) & 0xf00) | payload[0]);
                        _stickCenterY = (ushort)((payload[2] << 4) | (payload[1] >> 4));
                        _stickMinX = (ushort)(((payload[4] << 8) & 0xf00) | payload[3]);
                        _stickMinY = (ushort)((payload[5] << 4) | (payload[4] >> 4));
                        _stickMaxX = (ushort)(((payload[7] << 8) & 0xf00) | payload[6]);
                        _stickMaxY = (ushort)((payload[8] << 4) | (payload[7] >> 4));
                        _stickCalibrationDataLoaded = true;
                    }
                }
            }
        }

        void IInputStateCallbackReceiver.OnNextUpdate()
        {
            // コマンド送信間隔(s)
            const double commandInterval = 0.1;

            // 直前でコマンドを送信していた場合はスキップする
            if (lastUpdateTime < _lastCommandSentTime + commandInterval) return;

            // スティックパラメータが読み込まれていない場合
            if (!_stickParametersLoaded)
            {
                Debug.Log("Requesting stick parameters...");
                var stickParametersCommand =
                    SwitchReadSPIFlashOutput.Create(0x01, GetStickParametersAddress(), 18);
                ExecuteCommand(ref stickParametersCommand);
                _lastCommandSentTime = lastUpdateTime;
                return;
            }

            // スティックキャリブレーションデータが読み込まれていない場合
            if (!_stickCalibrationDataLoaded)
            {
                // ユーザーデータを優先的に読み込む
                if (!_stickUserCalibrationDataLoaded)
                {
                    Debug.Log("Requesting stick user calibration data...");
                    var stickUserCalibrationCommand =
                        SwitchReadSPIFlashOutput.Create(0x00, GetStickUserCalibrationAddress(), 9);
                    ExecuteCommand(ref stickUserCalibrationCommand);
                }
                else
                {
                    Debug.Log("Requesting stick factory calibration data...");
                    var stickCalibrationCommand =
                        SwitchReadSPIFlashOutput.Create(0x00, GetStickFactoryCalibrationAddress(), 9);
                    ExecuteCommand(ref stickCalibrationCommand);
                }

                _lastCommandSentTime = lastUpdateTime;
            }

            // TODO: IMUキャリブレーションデータの読み出し
        }

        bool IInputStateCallbackReceiver.GetStateOffsetForEvent(InputControl control, InputEventPtr eventPtr,
            ref uint offset)
        {
            return false;
        }

        protected override void OnAdded()
        {
            base.OnAdded();

            var descriptor = HID.HIDDeviceDescriptor.FromJson(description.capabilities);
            _side = descriptor.productId switch
            {
                ProductIdLeft => Side.Left,
                ProductIdRight => Side.Right,
                _ => throw new InvalidOperationException("Invalid product ID for Switch Joy-Con.")
            };

            // 出力モードを標準モードに設定
            var configureOutputModeCommand = SwitchConfigureReportModeOutput.Create(0x02, 0x30);
            ExecuteCommand(ref configureOutputModeCommand);
        }

        protected override void FinishSetup()
        {
            base.FinishSetup();

            captureButton = GetChildControl<ButtonControl>("capture");
            homeButton = GetChildControl<ButtonControl>("home");
        }

        private SwitchReadSPIFlashOutput.Address GetStickUserCalibrationAddress()
        {
            return _side switch
            {
                Side.Left => SwitchReadSPIFlashOutput.Address.LeftStickUserCalibration,
                Side.Right => SwitchReadSPIFlashOutput.Address.RightStickUserCalibration,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private SwitchReadSPIFlashOutput.Address GetStickFactoryCalibrationAddress()
        {
            return _side switch
            {
                Side.Left => SwitchReadSPIFlashOutput.Address.LeftStickFactoryCalibration,
                Side.Right => SwitchReadSPIFlashOutput.Address.RightStickFactoryCalibration,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private SwitchReadSPIFlashOutput.Address GetStickParametersAddress()
        {
            return _side switch
            {
                Side.Left => SwitchReadSPIFlashOutput.Address.LeftStickParameters,
                Side.Right => SwitchReadSPIFlashOutput.Address.RightStickParameters,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public enum Side
        {
            Left,
            Right
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct SwitchHIDGenericInputReport
    {
        public static FourCC Format => new('H', 'I', 'D');

        [FieldOffset(0)] public byte reportId;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct SwitchStandardInputReport
    {
        public const int Size = 0x49;

        [FieldOffset(0)] public byte reportId;
        [FieldOffset(1)] public byte timer;
        [FieldOffset(2)] public byte batteryAndConnectionInfo;
        [FieldOffset(3)] public byte buttons0;
        [FieldOffset(4)] public byte buttons1;
        [FieldOffset(5)] public byte buttons2;
        [FieldOffset(6)] public byte left0;
        [FieldOffset(7)] public byte left1;
        [FieldOffset(8)] public byte left2;
        [FieldOffset(9)] public byte right0;
        [FieldOffset(10)] public byte right1;
        [FieldOffset(11)] public byte right2;
        [FieldOffset(12)] public byte vibrationReport;

        // IMU data
        [FieldOffset(13)] public IMUData imu0;
        [FieldOffset(25)] public IMUData imu1;
        [FieldOffset(37)] public IMUData imu2;

        // Sub command reply data
        [FieldOffset(13)] public SubCommandReplyData subCommandReply;

        public SwitchJoyConHIDInputState ToHIDInputReport(ushort stickDeadZone, ushort stickCenterX, ushort stickMinX,
            ushort stickMaxX, ushort stickCenterY, ushort stickMinY, ushort stickMaxY)
        {
            // TODO: 左スティックの場合は左スティックの値を使用する
            var rawX = (ushort)(right0 | ((right1 & 0x0f) << 8));
            var rawY = (ushort)(((right1 & 0xf0) >> 4) | (right2 << 4));

            var diffX = rawX - stickCenterX;
            var diffY = rawY - stickCenterY;

            if (Math.Abs(diffX) < stickDeadZone) diffX = 0;
            if (Math.Abs(diffY) < stickDeadZone) diffY = 0;

            var normX = diffX > 0
                ? (float)diffX / stickMaxX
                : (float)diffX / stickMinX;
            var normY = diffY > 0
                ? (float)diffY / stickMaxY
                : (float)diffY / stickMinY;
            var stick = new Vector2(normX, normY);

            var state = new SwitchJoyConHIDInputState
            {
                buttons = ((uint)buttons2 << 16) | ((uint)buttons1 << 8) | buttons0,
                rightStick = stick
            };

            return state;
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct IMUData
    {
        [FieldOffset(0)] public byte accelX0;
        [FieldOffset(1)] public byte accelX1;
        [FieldOffset(2)] public byte accelY0;
        [FieldOffset(3)] public byte accelY1;
        [FieldOffset(4)] public byte accelZ0;
        [FieldOffset(5)] public byte accelZ1;
        [FieldOffset(6)] public byte gyroX0;
        [FieldOffset(7)] public byte gyroX1;
        [FieldOffset(8)] public byte gyroY0;
        [FieldOffset(9)] public byte gyroY1;
        [FieldOffset(10)] public byte gyroZ0;
        [FieldOffset(11)] public byte gyroZ1;

        public (short accelX, short accelY, short accelZ) GetAcceleration()
        {
            var accelX = (short)(accelX0 | (accelX1 << 8));
            var accelY = (short)(accelY0 | (accelY1 << 8));
            var accelZ = (short)(accelZ0 | (accelZ1 << 8));
            return (accelX, accelY, accelZ);
        }

        public (short gyroX, short gyroY, short gyroZ) GetGyroscope()
        {
            var gyroX = (short)(gyroX0 | (gyroX1 << 8));
            var gyroY = (short)(gyroY0 | (gyroY1 << 8));
            var gyroZ = (short)(gyroZ0 | (gyroZ1 << 8));
            return (gyroX, gyroY, gyroZ);
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct SubCommandReplyData
    {
        [FieldOffset(0)] public byte ack;
        [FieldOffset(1)] public byte subCommandId;

        // ReSharper disable once InconsistentNaming
        public unsafe byte* data
        {
            get
            {
                fixed (SubCommandReplyData* ptr = &this)
                {
                    return (byte*)ptr + 2;
                }
            }
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = Size)]
    internal struct SwitchConfigureReportModeOutput : IInputDeviceCommandInfo
    {
        public static FourCC Type => new('H', 'I', 'D', 'O');
        public FourCC typeStatic => Type;

        public const int Size = InputDeviceCommand.BaseCommandSize + 12;

        [FieldOffset(0)] public InputDeviceCommand baseCommand;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 0)]
        public byte reportId;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 1)]
        public byte packetNumber;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 2)]
        public byte rumbleData0;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 3)]
        public byte rumbleData1;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 4)]
        public byte rumbleData2;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 5)]
        public byte rumbleData3;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 6)]
        public byte rumbleData4;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 7)]
        public byte rumbleData5;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 8)]
        public byte rumbleData6;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 9)]
        public byte rumbleData7;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 10)]
        public byte subCommandConfigureReportMode;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 11)]
        public byte mode;

        public static SwitchConfigureReportModeOutput Create(byte packetNumber, byte mode)
        {
            return new SwitchConfigureReportModeOutput
            {
                baseCommand = new InputDeviceCommand(Type, Size),
                reportId = 0x01,
                packetNumber = packetNumber,
                rumbleData0 = 0x00,
                rumbleData1 = 0x01,
                rumbleData2 = 0x40,
                rumbleData3 = 0x40,
                rumbleData4 = 0x00,
                rumbleData5 = 0x01,
                rumbleData6 = 0x40,
                rumbleData7 = 0x40,
                subCommandConfigureReportMode = 0x03,
                mode = mode
            };
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = Size)]
    internal struct SwitchReadSPIFlashOutput : IInputDeviceCommandInfo
    {
        public static FourCC Type => new('H', 'I', 'D', 'O');
        public FourCC typeStatic => Type;

        public const int Size = InputDeviceCommand.BaseCommandSize + 0x16;

        [FieldOffset(0)] public InputDeviceCommand baseCommand;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 0)]
        public byte reportId;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 1)]
        public byte packetNumber;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 2)]
        public byte rumbleData0;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 3)]
        public byte rumbleData1;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 4)]
        public byte rumbleData2;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 5)]
        public byte rumbleData3;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 6)]
        public byte rumbleData4;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 7)]
        public byte rumbleData5;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 8)]
        public byte rumbleData6;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 9)]
        public byte rumbleData7;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 10)]
        public byte subCommandReadSPIFlash;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 11)]
        public byte address0;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 12)]
        public byte address1;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 13)]
        public byte address2;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 14)]
        public byte address3;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 15)]
        public byte length;

        public static SwitchReadSPIFlashOutput Create(byte packetNumber, Address address, byte length)
        {
            var addr = (uint)address;

            return new SwitchReadSPIFlashOutput
            {
                baseCommand = new InputDeviceCommand(Type, Size),
                reportId = 0x01,
                packetNumber = packetNumber,
                rumbleData0 = 0x00,
                rumbleData1 = 0x01,
                rumbleData2 = 0x40,
                rumbleData3 = 0x40,
                rumbleData4 = 0x00,
                rumbleData5 = 0x01,
                rumbleData6 = 0x40,
                rumbleData7 = 0x40,
                subCommandReadSPIFlash = 0x10,
                address0 = (byte)(addr & 0xFF),
                address1 = (byte)((addr >> 8) & 0xFF),
                address2 = (byte)((addr >> 16) & 0xFF),
                address3 = (byte)((addr >> 24) & 0xFF),
                length = length
            };
        }

        public enum Address : uint
        {
            LeftStickUserCalibration = 0x8012,
            RightStickUserCalibration = 0x801d,
            LeftStickFactoryCalibration = 0x603d,
            RightStickFactoryCalibration = 0x6046,
            LeftStickParameters = 0x6086,
            RightStickParameters = 0x6098,
            ImuFactoryCalibration = 0x6020,
            ImuUserCalibration = 0x8028,
            ImuParameters = 0x6080
        }
    }
}