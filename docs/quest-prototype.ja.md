# Quest初期プロトタイプ

## 実装範囲

原作を保存したまま、`QuestApp/` に新規Unityプロジェクトを作成しました。

- Unity 6000.6.7f1 / Android ARM64 / IL2CPP / Vulkan / URP。
- Meta XR Core SDK 207.0.1、Unity OpenXR 1.19.0、Meta OpenXR 2.7.0。
- MRを標準にし、パススルー初期化を確認した後にMetaの境界表示抑制APIを要求します。OSの応答や設定によって抑制されない場合があります。
- VRに切り替える前に境界表示の復帰をOSから確認します。確認できない場合はMRを続けます。常時境界なしを宣言する `BOUNDARYLESS_APP` は使いません。
- 日本語テキストを飛ばすプール式表示。Noto Sans CJK JPを同梱し、リッチテキストは無効です。
- マイク権限要求と音量表示。録音ファイル保存・音声のネット送信は行いません。
- 端末内日本語音声認識は未搭載です。今はサンプル文で表示・通信を検証できます。
- VR背景は単色の確認用です。原作の夕日、水面、キャラクターは未移植です。

`6000.6.7f1` はUnity **6.6のパッチ7**です。Unity 6.7ではありません。2026-10-28の[Unity公式記事](https://unity.com/ja/blog/xreal-aura-games-made-with-unity)はAndroid XR向けに6.6を推奨し、6.7 LTSの正式リリース後の移行を案内しています。Quest向けには別途[Meta公式のUnity要件](https://developers.meta.com/vr/documentation/unity/unity-development-requirements/)も確認しています。

## 同じWi-Fi内のP2P

ホスト選出を必要としない対等なUDP通信です。各端末がUDP 47726で待ち受け、IPv4ブロードキャストで相手を発見します。1秒間隔のhelloと6秒のタイムアウト、部屋番号1〜9、最大8端末（自分を含む）を設定しています。

AndroidではLAN接続中のみWi-Fiの受信ロックを取得し、切断時・スリープ時に解放します。[Android MulticastLock API](https://developer.android.com/reference/android/net/wifi/WifiManager.MulticastLock)を利用していますが、実機やアクセスポイントの受信制限を解消できる保証はありません。

発射イベントには一意なIDを付け、相手ごとにACKを確認し、250ms間隔・最大6回の送信で再試行します。同じイベントの再送は重複表示しません。再送が尽きた回数は「未達」に表示します。通信の完全な到達保証や順序保証はありません。

部屋番号は認証機能ではありません。信頼できるLAN内での試験を想定し、暗号化・ユーザー認証は未実装です。ゲストWi-Fiの端末分離、ブロードキャスト制限、VPNなどがあると接続できません。外部サーバー、課金サービス、NAT越えはありません。

送信対象は確定テキストと表示色です。相手の文字は受信側の視点付近から発射します。共有空間の座標合わせ、共有アンカー、物理オブジェクトの同期はまだありません。端末をスリープすると切断するので、復帰後は「接続」を押し直します。

## 操作

1. アプリ起動時はMRです。
2. 右コントローラーのポインターをボタンに合わせ、トリガーで選択します。
3. 「次のことば」でサンプルを選び、「サンプル発射」で文字を飛ばします。ボタン以外に向けたトリガーでも発射できます。
4. 複数端末では同じWi-Fiに接続し、同じ部屋番号で各端末の「接続 / 切断」を押します。
5. 「マイク確認」を押し、権限を許可すると音量値を表示します。発話の文字起こしはまだ行いません。
6. Bボタンで操作パネルを正面に移動します。「MR → VR」で背景を切り替えます。

Unity EditorでのプレビューはSpaceで発射、Nで接続切替、Rでパネルを正面へ移動、マウスでボタン選択です。Editorの表示確認はQuest実機のMR・境界API確認を代替しません。

## ビルド

WindowsのPowerShellでリポジトリルートから実行します。既定のUnityインストール先と異なる場合は各コマンドに `-Editor '絶対パス/Unity.exe'` を指定します。

```powershell
./tools/unity.ps1 -Stage Import
./tools/unity.ps1 -Stage TextResources -WithoutEditorXr
./tools/unity.ps1 -Stage Setup -WithoutEditorXr
./tools/unity.ps1 -Stage Tests -WithoutEditorXr
./tools/unity.ps1 -Stage Android -WithoutEditorXr
./tools/inspect_apk.ps1
```

`Setup` は生成シーンと設定を再構成するため、手でシーンを編集した後は必要なときだけ実行します。出力先は `QuestApp/Builds/Koekatamarin2026.apk`、ログとテスト結果は `.work/` です。APKは開発用署名・Development Buildで、ストア公開用成果物ではありません。生成済みシーンは `QuestApp/Assets/Koekatamarin/Generated/Playground.unity` です。

このPCではMeta SDK 207.0.1のWindows用 `OVRPlugin.dll` がEditor起動時に異常終了する現象を確認しました。ヘッドセットなしのバッチ作業では、上記コマンドに **`-WithoutEditorXr`** を付けて回避します。パッケージ取得済みの `Library/PackageCache` 内でPC用DLLのEditor互換設定だけを一時的に無効にし、プロセス終了後に元へ戻します。Android用AAR・SDKのC#コードには手を加えません。このオプションを付けたプレビューではQuest Linkの動作確認はできません。

```powershell
./tools/unity.ps1 -Stage Android -WithoutEditorXr
```

実機を接続できるときは、開発者モードとUSBデバッグを許可して以下を実行します。

```powershell
adb install -r QuestApp/Builds/Koekatamarin2026.apk
```

## 検証と残作業

2026-11-01にDevelopment APKのビルドが成功しました。サイズは77,831,148 bytes（約78 MB / 74.23 MiB）。`inspect_apk.ps1` で署名、ARM64のみであること、IL2CPP、Android用Meta XRネイティブライブラリ、マイク・LAN・パススルー・境界表示権限・VRカテゴリを検査済みです。`BOUNDARYLESS_APP` が含まれないことも確認しました。

SHA-256: `fd39286055eb7c1afd8101cf7ec313ce9eb420c1790f946200e0843d74cc33f8`

EditModeテスト7件が成功しました。日本語の入力検証、不正な文字列・バージョン・部屋の拒否、イベントの重複排除、UTF-16の切り詰め、実UDPソケットによる相手発見・受信・ACK・重複発射の防止を確認しています。これはWindows上のソケット試験であり、Quest間の通信確認ではありません。

現在Quest 3を接続できないため、装着しての起動、パススルー、境界復帰、コントローラー、AndroidでのLAN発見・マイク、描画速度は未確認です。

Editorで日本語UIと文字の描画を目視確認しました。下はPC用XRプラグインを無効にした表示確認用の画像です。このプレビューではOVRManager初期化時に `DllNotFoundException` が記録されるため、XR初期化や実機動作の成功を示すものではありません。APK内にAndroid用 `libOVRPlugin.so` が存在することは別途検査しています。

![Editorでの表示確認。黒背景部分は実機パススルーではない](images/prototype-editor.png)

次の実装対象は端末内ASRです。モデルの日本語精度、発話終了から表示までの遅延、発熱、メモリをQuest 3で比較して選びます。`ISpeechRecognizer` は接続予定のインターフェースで、まだ認識エンジンやPCM配信処理は接続していません。

## 同梱フォント

Noto Sans CJK JPはSIL Open Font License 1.1です。ライセンスは `QuestApp/Assets/Koekatamarin/ThirdParty/NotoSans-LICENSE.txt` に同梱しています。原作の旧フォント・旧SDKバイナリは新アプリへコピーしていません。
