# UnityJoyCon

Read this page in Japanese: [README.ja.md](./README.ja.md).

UnityJoyCon is a library that lets you use Nintendo Switch Joy-Con controllers with the Unity Input System. It automatically recognizes Joy-Con as HID devices and exposes buttons, sticks, accelerometer, and gyroscope data via InputDevice. HID rumble is also supported.

![Demo of UnityJoyCon. Pressing buttons on the left and right Joy-Con lights up the corresponding buttons in a Unity scene.](https://github.com/user-attachments/assets/12084255-9505-4443-9137-1a14359b77fa)

## Features

- Automatically registers layouts in the Unity Input System and recognizes left/right Joy-Con as `SwitchJoyConLeftHID` / `SwitchJoyConRightHID`
- Exposes buttons / sticks / accelerometer / gyroscope as standard InputControls
- Supports attitude estimation with a complementary filter that fuses accelerometer and gyroscope data
- Supports HID rumble in addition to the Unity Input System's default `IDualMotorRumble`
- No native plugins; works cross-platform (Windows / macOS / Linux)

## Installation

Add the package as a Git dependency in Package Manager:
`https://github.com/tuatmcc/UnityJoyCon.git?path=Packages/com.tuatmcc.unityjoycon#v0.2.1`  
To update, replace `#v0.2.1` with the latest tag.

### Edit manifest.json directly

Open `Packages/manifest.json` and add this under `dependencies`:

```diff
 {
   "dependencies": {
+    "com.tuatmcc.unityjoycon": "https://github.com/tuatmcc/UnityJoyCon.git?path=Packages/com.tuatmcc.unityjoycon#v0.2.1"
   }
 }
```

### Add via GUI

1. Open `Window > Package Manager`.
2. Click `+` and select `Add package from git URL...`.
3. Enter `https://github.com/tuatmcc/UnityJoyCon.git?path=Packages/com.tuatmcc.unityjoycon#v0.2.1` and click `Add`.

## Usage

After installation, layouts and initialization are automatic. Pair your Joy-Con in the OS and they will appear in the Unity Input System—no manual setup needed.

The left Joy-Con is recognized as `SwitchJoyConLeftHID` and the right as `SwitchJoyConRightHID`. Use them like a regular gamepad in an Input Actions asset. From scripts, you can fetch devices with `InputSystem.GetDevice<SwitchJoyConLeftHID>()`, etc. See the code samples in [Assets/JoyConSample/JoyConLeft.cs](./Assets/JoyConSample/JoyConLeft.cs) and [Assets/JoyConSample/JoyConRight.cs](./Assets/JoyConSample/JoyConRight.cs).

## Sample Scene

`Assets/JoyConSample/JoyConSample.unity` includes a demo that shows buttons, sticks, and IMU data in the UI. Pressing the ZR button rumbles the right Joy-Con. Pair a Joy-Con and play the scene to confirm behavior.

## License

This package is released under the MIT License. See [LICENSE](./LICENSE).
