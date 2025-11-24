# UnityJoyCon

UnityJoyCon は Nintendo Switch の Joy-Con を Unity Input System で扱えるようにするライブラリです。
Joy-Con を HID デバイスとして自動認識し、ボタン・スティック・加速度・ジャイロなどのデータを InputDevice から取得できます。
また、Joy-Con の HID 振動機能もサポートしています。

## 特徴
- Unity Input System へのレイアウトを自動登録し、左右 Joy-Con を `SwitchJoyConLeftHID` / `SwitchJoyConRightHID` として認識
- ボタン / スティック / 加速度 / ジャイロを標準の InputControl として取得可能
- Unity Input System デフォルトの `IDualMotorRumble` 以外にも、 HID 振動機能をサポート
- ネイティブプラグイン不要でクロスプラットフォーム対応（Windows / macOS / Linux）

## 導入方法

Package Manager で `https://github.com/tuatmcc/UnityJoyCon.git?path=Packages/com.tuatmcc.unityjoycon#v0.2.1` を Git パッケージとして追加します。
アップデートしたい場合は `#v0.2.1` の部分を最新のバージョンに変更してください。

### manifest.json を直接編集する

`Packages/manifest.json` を開き、`dependencies` セクションに以下を追加します。

```diff
 {
   "dependencies": {
+    "com.tuatmcc.unityjoycon": "https://github.com/tuatmcc/UnityJoyCon.git?path=Packages/com.tuatmcc.unityjoycon#v0.2.1"
   }
 }
```

### **GUI から追加する**

1. `Window > Package Manager` を開きます。
2. 左上の `+` ボタンから `Add package from git URL...` を選択します。
3. `https://github.com/tuatmcc/UnityJoyCon.git?path=Packages/com.tuatmcc.unityjoycon#v0.2.1` を入力して `Add` します。

## 使い方
パッケージ導入後はレイアウト登録と初期化が自動で行われ、Joy-Con を OS 側でペアリングするだけで Unity Input System に現れます。手動の初期化や特別なセットアップは不要です。

左 Joy-Con は `SwitchJoyConLeftHID`、右 Joy-Con は `SwitchJoyConRightHID` というデバイス名で認識されます。
Input Actions アセット上で通常のゲームパッドと同様に値を扱えます。
スクリプトから直接取得する場合も `InputSystem.GetDevice<SwitchJoyConLeftHID>()` などでデバイスを取得し、値を参照することが可能です。
実際のコード例は [Assets/JoyConSample/JoyConLeft.cs](./Assets/JoyConSample/JoyConLeft.cs) および [Assets/JoyConSample/JoyConRight.cs](./Assets/JoyConSample/JoyConRight.cs) を参照してください。

## サンプルシーン

`Assets/JoyConSample/JoyConSample.unity` にサンプルシーンが含まれています。
このシーンでは Joy-Con のボタン・スティック・IMU データを UI に表示する簡単なデモが実装されています。
また、ZR ボタンを押すと右 Joy-Con が振動します。
Joy-Con をペアリングしてシーンを再生することで、動作確認や使用例を確認できます。

## ライセンス

このパッケージは MIT ライセンスのもとで公開されています。詳細は [LICENSE](./LICENSE) を参照してください。
