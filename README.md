# UnityJoyCon

UnityJoyCon は Nintendo Switch の Joy-Con を Unity から扱うためのライブラリです。ボタン入力、スティック座標、加速度・ジャイロセンサーの生データを取得でき、ゲームロジックやデバッグ表示に組み込めます。

## 導入方法

### UnityNuGet レジストリを設定する
本パッケージは `System.Threading.Channels` を UnityNuGet (`org.nuget.system.threading.channels`) から取得します。レジストリが設定されていないと依存関係が解決できないため、導入先プロジェクトで以下のいずれかの方法で UnityNuGet を追加してください。

- **manifest.json を直接編集する**
  ```json
  "scopedRegistries": [
    {
      "name": "Unity NuGet",
      "url": "https://unitynuget-registry.openupm.com",
      "scopes": [
        "org.nuget"
      ]
    }
  ]
  ```
  既存の `scopedRegistries` 配列がある場合は、`Unity NuGet` のエントリをマージし `org.nuget` スコープが含まれるように調整します。

- **GUI から追加する**
  1. `Edit > Project Settings...` を開きます。
  2. `Package Manager` セクションの `Scoped Registries` で `+` を押し、下記を入力します。
     - Name: `Unity NuGet`
     - URL: `https://unitynuget-registry.openupm.com`
     - Scope(s): `org.nuget`
  3. 保存すると manifest.json に反映されます。

### パッケージを Git から追加する
レジストリ設定後、`com.tuatmcc.unityjoycon` を Git パッケージとして導入します。

- **manifest.json を直接編集する**
  ```json
  "com.tuatmcc.unityjoycon": "https://github.com/tuatmcc/UnityJoyCon.git?path=Packages/com.tuatmcc.unityjoycon"
  ```

- **GUI から追加する**
  1. `Window > Package Manager` を開きます。
  2. 左上の `+` ボタンから `Add package from git URL...` を選択します。
  3. `https://github.com/tuatmcc/UnityJoyCon.git?path=Packages/com.tuatmcc.unityjoycon` を入力して `Add` します。

Unity の Package Manager を再読み込みすると `UnityJoyCon` がインストールされ、`UnityJoycon` 名前空間の API を参照できるようになります。

### サンプルを試す場合
リポジトリ全体を取得すると、サンプルシーンで動作確認が行えます。

```bash
git clone https://github.com/tuatmcc/UnityJoyCon.git
cd UnityJoyCon
```

`Assets/SampleJoyCon.unity` を開き、Joy-Con をペアリングした状態で Play モードに入ると、ボタン・スティック・IMU の値をリアルタイムで確認できます。

## クイックスタート
最初に見つかった Joy-Con に接続し、ボタン / スティック / IMU を取得するサンプルです。

```csharp
using System.Linq;
using UnityEngine;
using UnityJoycon;
using UnityJoycon.Hidapi;

public class JoyConBootstrap : MonoBehaviour
{
    private Hidapi? _hidapi;
    private JoyCon? _joycon;

    private async void Awake()
    {
        _hidapi = new Hidapi();
        var info = _hidapi.GetDevices(0x057e) // Nintendo Vendor ID
            .FirstOrDefault(d => d.ProductId is 0x2006 or 0x2007);
        if (info == null)
        {
            Debug.LogError("Joy-Con が見つかりません");
            return;
        }

        var device = _hidapi.OpenDevice(info);
        _joycon = await JoyCon.CreateAsync(device);
    }

    private void Update()
    {
        if (_joycon?.TryGetState(out var state) != true) return;

        if (state.IsButtonPressed(Button.A))
        {
            Debug.Log("A ボタンが押されています");
        }

        var stick = state.Stick;             // -1.0f ~ 1.0f に正規化済み
        var imu = state.ImuSamples[^1];      // 最新サンプル
        // stick / imu をゲーム内ロジックに反映
    }

    private async void OnDestroy()
    {
        if (_joycon != null)
        {
            await _joycon.DisposeAsync();
            _joycon = null;
        }

        _hidapi?.Dispose();
    }
}
```

## サンプルシーン
- `Assets/JoyConRight.cs` は Joy-Con のステートを UI に反映する参考スクリプトです。
- `Assets/SampleJoyCon.unity` を Play すると、ボタンが点灯し IMU が TextMeshPro で表示されます。

## hidapi バイナリの更新
ネイティブライブラリを更新する場合は、GitHub Actions の `Build hidapi binaries` ワークフローを Workflow Dispatch で起動してください。macOS / Windows / Linux 向けの hidapi をビルドし、`Packages/com.tuatmcc.unityjoycon/Runtime/Hidapi/Plugins/` 下へ自動コミットします。

## ライセンス
- パッケージ本体: MIT License
- hidapi: 各プラットフォーム向けバイナリは hidapi のライセンスに従います
