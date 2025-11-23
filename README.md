# UnityJoyCon

UnityJoyCon は Nintendo Switch の Joy-Con を Unity Input System に直接統合するライブラリです。Joy-Con を HID デバイスとして自動認識し、ボタン入力・スティック座標・加速度 / ジャイロの生データをそのまま InputDevice から取得できます。

## 特徴
- Unity Input System へのレイアウトを自動登録し、左右 Joy-Con を `SwitchJoyConLeftHID` / `SwitchJoyConRightHID` として認識
- ボタン / スティック / 加速度 / ジャイロを標準の InputControl として取得可能
- ネイティブプラグイン不要。OS にペアリングされている Joy-Con をそのまま利用

## 前提条件
- Unity 2022.3.62f3 以降（推奨）
- `com.unity.inputsystem` 1.14.2（本パッケージの依存として自動導入されます）
- Joy-Con を OS の Bluetooth 設定で事前にペアリングしておくこと

## 導入方法

### パッケージを Git から追加する
Package Manager で Git パッケージとして追加します。

- **manifest.json を直接編集する**
  ```json
  "com.tuatmcc.unityjoycon": "https://github.com/tuatmcc/UnityJoyCon.git?path=Packages/com.tuatmcc.unityjoycon"
  ```

- **GUI から追加する**
  1. `Window > Package Manager` を開きます。
  2. 左上の `+` ボタンから `Add package from git URL...` を選択します。
  3. `https://github.com/tuatmcc/UnityJoyCon.git?path=Packages/com.tuatmcc.unityjoycon` を入力して `Add` します。

### Input System を有効化する
パッケージ導入時に「新しい Input System を有効にしますか？」と促される場合があります。`Project Settings > Player > Active Input Handling` を `Input System Package (New)` または `Both` に設定し、Unity を再起動してください。

Unity の Package Manager を再読み込みすると `UnityJoyCon` がインストールされ、`UnityJoyCon` 名前空間の API を参照できるようになります。

### サンプルを試す場合
リポジトリ全体を取得すると、サンプルシーンで動作確認が行えます。

```bash
git clone https://github.com/tuatmcc/UnityJoyCon.git
cd UnityJoyCon
```

`Assets/SampleJoyCon.unity` を開き、Joy-Con をペアリングした状態で Play モードに入ると、ボタン・スティック・IMU の値をリアルタイムで確認できます。

## クイックスタート
最初に見つかった Joy-Con から入力を読み取るシンプルなサンプルです。Input System によって Joy-Con が認識されると、`InputSystem.GetDevice<SwitchJoyConLeftHID>()` / `InputSystem.GetDevice<SwitchJoyConRightHID>()` で取得できます。実運用では InputAction（Input Actions アセットまたは `InputAction` クラス）にバインドして扱うのが基本です。

```csharp
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityJoyCon;

public class JoyConReader : MonoBehaviour
{
    private SwitchJoyConLeftHID _left;
    private SwitchJoyConRightHID _right;

    private void Start()
    {
        _left = InputSystem.GetDevice<SwitchJoyConLeftHID>();
        _right = InputSystem.GetDevice<SwitchJoyConRightHID>();
    }

    private void Update()
    {
        // シーン再生中にペアリングされた場合も追従できるよう、見つからなければ再取得します
        _left ??= InputSystem.GetDevice<SwitchJoyConLeftHID>();
        _right ??= InputSystem.GetDevice<SwitchJoyConRightHID>();

        if (_left != null && _left.leftTrigger.wasPressedThisFrame)
            Debug.Log("ZL が押されました");

        if (_right != null)
        {
            var stick = _right.rightStick.ReadValue(); // -1.0f ~ 1.0f
            var accel = _right.accelerometer.ReadValue();
            var gyro = _right.gyroscope.ReadValue();
            // stick / accel / gyro をゲーム内ロジックへ反映
        }
    }
}
```

### InputAction で使う例（抜粋）
入力アクションアセットで、デバイスを `SwitchJoyConLeftHID` / `SwitchJoyConRightHID` に限定したり、左スティックやボタンをバインドできます。スクリプトで生成する場合の最小例:

```csharp
using UnityEngine;
using UnityEngine.InputSystem;
using UnityJoyCon;

public class JoyConActions : MonoBehaviour
{
    private InputAction _stick;

    private void OnEnable()
    {
        _stick = new InputAction(type: InputActionType.Value, binding: "<SwitchJoyConRightHID>/rightStick");
        _stick.Enable();
        _stick.performed += ctx => Debug.Log($"Right stick: {ctx.ReadValue<Vector2>()}");
    }

    private void OnDisable()
    {
        _stick.Dispose();
    }
}
```

## サンプルシーン
- `Assets/JoyConRight.cs` は Joy-Con のステートを取得し GameObject に反映するスクリプトです。
- `Assets/SampleJoyCon.unity` を実行すると、押したボタンの色が変化し、 IMU のデータが UI で表示されます。

## ライセンス
- MIT License
