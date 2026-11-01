# OGJ_Koekatamarin2026

2013年のOculus DK1作品「コエカタマリン」を、Meta Quest 3向けに復活させるプロジェクトです。

原作: [haiatto/OGJ_Koekatamarin](https://github.com/haiatto/OGJ_Koekatamarin)

原作の取得コミット: `c07556014bbb4a17f4973f38aac01426bd11b4ea`

## 現在の状態

**0.8.0ではSilero VADを撤去し、原作の「認識途中の文字を差分で飛ばす」処理へ戻しています。** 47件のUnityテスト、導入ツール10件のテストを通過し、APKの生成・検査とQuest 3への更新・起動を確認しました。実機ログでReazonSpeechの途中結果を確認し、ユーザーもQuest 3で「話している途中から文字が飛ぶ」ことを確認しました（2026-11-02）。環境音の誤認識・長い発話の区切り・持続負荷は確認待ちです。[音声処理と原作との差](docs/offline-speech.ja.md)、[0.8.0検査結果](docs/validation/quest-0.8.0.json)。APKは `QuestApp/Builds/Koekatamarin2026.apk`（約509 MB）です。 **既知の問題：認識の訂正に伴う多重発射が実機で報告されています。途中から飛ぶ動作を維持し、認識方式を見直す必要があります。**

新規Unityプロジェクトは `QuestApp/` です。0.7.0で原作の5層TextMeshとWater4シェーダーを移植し、無料フォント切替を追加しました。「みかちゃん-PB」「ラノベPOP」「Noto Sans JP」の切替と文字表示は0.7.0のQuest 3でユーザー確認済みです（2026-11-02）。HG創英角ポップ体は新版に同梱しません。海の左右表示は確認待ちです。[表現の移植範囲とフォントの利用条件](docs/legacy-appearance-port.ja.md)。

原作の履歴とファイルを保存しています。同じ所有者のため、GitHubのFork機能ではなく、履歴を引き継ぐ別リポジトリとして管理します。

[要件ごとの検証状況](docs/requirements-status.ja.md)と、Questを接続できたときの[導入・実機確認手順](docs/device-validation.ja.md)を用意しています。

- Quest 3単体動作、MRを標準とします。オプションのVRでは原作素材を使った夕日・砂浜と、Water4移植版の水面を表示します。[VR復元の範囲](docs/vr-restoration.ja.md)。
- 原作の中心は「音声認識 → 日本語の文字を塊として飛ばす」です。読み上げ（TTS）は別機能です。
- 日本語音声認識は端末内処理です。ReazonSpeech k2-v2を初期選択し、SenseVoice Smallへパネルで切り替えられます。音声は起動時ONです。0.8.0ではSilero VADを撤去し、原作の途中結果差分発射へ戻しています。一括認識モデルを0.512秒ごとに実行し直すため、原作との認識タイミングの差や長い発話の区切りは[音声処理の説明](docs/offline-speech.ja.md)に記載しています。[音声・手・移動の操作](docs/modern-controls.ja.md)。通信は同じWi-Fi内のQuest同士によるP2Pで、Unity Relayは使いません。
- 文字は原作の5層TextMesh構造とRigidbodyを使い、頭の前から発射します。MRでは許可された部屋データのメッシュ、VRでは地面・海面に衝突します。[原作の文字・衝突・海・声量の調査](docs/original-voice-and-water.ja.md)。
- 同室のMRでは、共通の2点をコントローラーで指定して文字の発射位置と向きを共有できます。[位置合わせの操作](docs/shared-space.ja.md)。
- NAT越えは基本的に対象外とし、インターネット越しの対戦や接続支援サービスは開発計画に含めません。
- 元のUnity 4プロジェクトを最新Unityでそのまま開く前に、[調査報告](docs/revival-investigation.ja.md)を参照してください。

## 構成

| パス | 内容 |
| --- | --- |
| `QuestApp/` | Unity 6000.6.7f1 / Quest 3用の新規プロジェクト |
| `docs/quest-prototype.ja.md` | プロトタイプの操作、ビルド方法、確認範囲 |
| `New Unity Project/` | 保存した旧Unityプロジェクト。新規開発先ではありません |
| `ReleseFolder/` | 当時のWindows/Mac配布物とサーバー |
| `server/` | Chrome音声認識とUnityを接続していた旧Node.jsサーバー |
| `docs/revival-investigation.ja.md` | 欠損、技術選定、移植順序、実機検証計画 |
| `docs/legacy-audit.json` | 原作コミットに対する静的検査結果 |
| `tools/audit_legacy.py` | 標準Pythonだけで実行できる読み取り専用検査 |

検査の再実行: `python tools/audit_legacy.py`。Unityでのインポート・ビルド成功を判定するものではありません。

## Questプロトタイプ

Unity Hubで **6000.6.7f1** とAndroid Build Support（SDK/NDK/OpenJDKを含む）を入れ、`QuestApp/` を開きます。Meta XR Core SDK 207.0.1、MR Utility Kit 207.0.0、OpenXR 1.19.0、URP 17.6.0を使用します。

モデルとネイティブライブラリはGitに含めないため、Unityを起動する前にPython 3.12以降で `python tools/setup_speech.py` を実行してください。固定SHA-256を確認して公式配布物を配置します。生成されたAPKにはモデルが含まれ、Questで追加ダウンロードは不要です。

同じWi-Fi・同じ部屋番号の端末間で直接UDP通信します。全端末が対等なP2Pで、Photon、Unity Relay、UGSアカウント、外部サーバー、NAT越えは使用しません。詳細は[操作・ビルド手順](docs/quest-prototype.ja.md)を参照してください。

自動発見が通らないときは、コントローラーで[相手のIPを指定](docs/lan-manual.ja.md)できます。

## 原作の取り扱い

旧SDK、第三者素材、フォント、配布実行ファイルは原作の資料として含まれます。リポジトリ全体を新たにMIT等で再ライセンスしていません。2026版への再同梱可否は素材ごとに確認し、旧証明書・旧サーバー・旧ネイティブプラグインは新アプリに流用しません。
