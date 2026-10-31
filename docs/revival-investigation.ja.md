# コエカタマリン復活・初期調査

調査日: 2026-11-01（日本時間）

対象: `haiatto/OGJ_Koekatamarin`、コミット `c07556014bbb4a17f4973f38aac01426bd11b4ea`

検証範囲: Git全履歴の取得、ソース読解、追跡ファイルとYAML GUIDの静的検査、公式資料の調査。Unityインポート、APKビルド、Quest実機性能は未検証。

## 1. 結論

復活に必要なゲームの核は残っている。ただし「中身が完全に揃っていて、そのまま現代Unityで動く」状態ではない。新しいQuest用Unityプロジェクトを別フォルダーに作り、原作の振る舞いと使える素材を移植する方式を推奨する。

最初の実装目標は **MRで日本語の声が文字の塊になって飛ぶ、Quest 3単体の1人用体験**。次に2台で文字イベントを共有し、最後に夕日のVRモードを復活させる。音声モデルの採否は実機測定で決める。

## 2. 原作は何をしていたか

`server/client.html` の `webkitSpeechRecognition` が `ja-JP`、中間結果あり、連続認識でChromeのマイク入力を文字列化する。Node.jsのHTTPS/WebSocketサーバーを経由し、Unityの `KoeKatamarinBehaviourScript.cs` が `wss://localhost:8888` から受信する。

受信文字列と前回文字列の差分を `shoot.cs` または `multiShoot.cs` に渡す。`text.prefab` を生成し、`TextManage.cs` が複数の `TextMesh` に表示する。Photonの `ShootMulti` RPCには文字列・位置・回転を載せている。音声波形の転送やTTS生成が中心ではない。

旧コードにあるGoogle系の仕組みは **STT/ASR（音声→文字）**。TTS（文字→音声）が新仕様で必要なら、別の音声合成層として追加する。

## 3. 保存状態と欠損

| 調査項目 | 結果 |
| --- | --- |
| 履歴 | 65コミット、最終コミット2013-11-17、追跡ファイル1,004件 |
| Unity | 配布物の `output_log.txt` に `4.2.0f4`。`ProjectVersion.txt` は存在しないため、最終編集Editor版まで断定はしない |
| ゲーム本体 | 主要C# 4本、ゲーム用シーン4件、文字・射出用Prefab、夕日素材、アバター素材あり |
| ビルド対象 | `Assets/Game/test_taguchi.unity` が唯一の有効シーン。登録シーン10件はすべて存在 |
| SDK | DK1世代OVR、Photon PUN changelog先頭v1.24、Photon DLL履歴3.2.2.1、websocket-sharpあり |
| メタデータ | 追跡アセットのファイルに対応する `.meta` 欠落0、重複GUID 0。孤立 `library.meta` 1件 |
| GUID参照 | YAML 78件を検査。未解決GUID 23種類。主にデモ・水面・旧OVR素材 |
| 有効シーンの依存 | 静的依存43ファイル。水面 `Water4ExampleSimple.mat` のGUID参照3種類が未解決 |
| クマ素材 | `Game/Chara/Resouces/TeddyBear.prefab` のAvatar/MeshとMaterialが未解決。`Resouces` は原文の綴り |
| 検査できない部分 | バイナリ等の非YAMLシリアライズ35件、fileID整合性、Importer依存、実行時Resources/Photonロード、unitypackage内部 |
| LFS | 未取得LFSポインター0件 |

詳細は [legacy-audit.json](legacy-audit.json)。この結果は「参照先の完全性」や「動作可能性」を保証しない。特にFBX内のfileID、古いUnityのシリアライズ変換、実行時文字列ロードはEditorでの検証が必要。

旧ソースには次の移植対象がある。

- DK1用 `OVRDevice.GetPredictedOrientation` とデスクトップ用OculusバイナリはQuest Android ARM64へ置換する。
- `gameObject.rigidbody`、旧PUN API、旧シェーダー・水面表現を現代APIに置換する。
- WebSocketのコールバックとUpdateが同じ `List<string>` を操作する構造を、スレッド安全なキューに置換する。
- 中間認識結果の単純な文字列差分は、日本語の漢字確定・訂正・同文繰り返しで誤射出しうる。発話ID、暫定表示、確定イベントを分離する。
- `TextManage.DestroySelf` はTextMeshとスクリプトを削除し、生成したGameObject自体を片付けていない。2026版ではTTLとプールで管理する。

原作にルートLICENSEはない。`HGRPP1001.TTF`、第三者アバター、旧SDKの再配布条件は、原作に含まれるという理由だけで新しいライセンスを付けない。日本語フォントはSIL OFL等の配布条件を確認したものに交換する方針。旧サーバーにはテスト証明書と固定パスフレーズがあり、新版へ流用しない。これらは元から公開されている資料であり、今回新しく取得した認証情報は追加しない。

## 4. UnityとQuest基盤

公式発表で確認した最新Supported系統は **Unity 6.6**、LTS系統は **Unity 6.3 LTS**。最新を使う希望に合わせ、実装開始時にHubで6.6の最新安定パッチとMeta SDKの組み合わせを確認して固定する。Alpha/Betaを選ぶ意味ではない。[Unity 6.6公式発表](https://discussions.unity.com/t/unity-6-6-is-now-available/1735357)、[リリース方針](https://unity.com/releases/unity-6/support)、[公式アーカイブ](https://unity.com/releases/editor/archive)。

このPCには `6000.3.10f1` とAndroid Build Support、SDK/NDK/OpenJDKが存在する。今回の初期調査ではEditorの追加インストールや旧プロジェクトの自動変換は実施していない。6.6の正確な最新パッチ番号は本調査では固定していない。

推奨する初期構成:

- 新規フォルダー `QuestApp/`、URP、Android ARM64、IL2CPP。
- Unity OpenXR Plugin + Unity OpenXR: Meta + Meta XR Core SDK。必要に応じてInteraction SDK / MR Utility Kitを追加。
- 旧 `Assets/OVR`、旧Photon、旧Standard Assetsを新プロジェクトへ丸ごとコピーしない。
- 最初に文字だけを描画する小さなMRシーンでAPKビルドとマイク取得を確認する。
- 解決した `manifest.json`、`packages-lock.json`、`ProjectVersion.txt` をコミットし、再現可能にする。

Metaの現行資料はOculus XR Pluginを非推奨とし、OpenXRを推奨している。Depthによる遮蔽を使う場合はUnity OpenXR: Metaも必要。[Meta公式XR設定](https://developers.meta.com/vr/documentation/unity/unity-xr-plugin/)。

## 5. MRを標準、VRをオプション、境界をどう扱うか

本作品には **Contextual boundaryless（Boundary API）** が適する。MR起動時にPassthroughを有効にし、`OVRManager.shouldBoundaryVisibilityBeSuppressed` を要求する。`isBoundaryVisibilitySuppressed` と `BoundaryVisibilityChanged` で実際のOS状態を確認する。

アプリ全体を `com.oculus.feature.BOUNDARYLESS_APP` にする方式は、完全な没入VRを含められないため採用しない。VRオプションとの両立には、場面ごとのBoundary APIを使う。両方式は排他的で、OSバージョン条件もある。[Meta公式Boundaryless資料](https://developers.meta.com/vr/documentation/unity/unity-boundaryless/)。

提案する状態遷移:

1. 起動 → Passthrough稼働確認 → 境界抑制を要求 → MR操作開始。
2. VR切替 → 境界抑制を解除 → OSが境界有効を確認 → 没入背景へ切替。
3. MR復帰 → Passthrough稼働確認 → 境界抑制を要求。
4. 要求拒否・API未対応・追跡喪失 → 現在のOS境界状態を尊重し、必要ならMRに留まる。無限待ちしない。

「ガードなしを標準」はアプリのMR時の要求として実現する。ユーザー全体のGuardian設定を強制無効化する機能ではない。Link上の確認だけでは不十分で、Quest単体APKで検証する。固定物の位置にはStage原点の永続性を仮定せず、必要な場合は空間アンカーを使う。

同室でのMRマルチプレイには座標合わせが別途必要。通信が成立しても、各Questのローカル原点は一致しない。最初は各人のローカルな発射位置に表示する遠隔共有方式とし、同室で同じ場所の文字を見る体験は共有アンカー/コロケーション検証を追加する。

## 6. 日本語音声認識の候補

Quest 3上での速度は未測定。Android対応やPCベンチマークを、そのままQuestの72/90Hz描画と同時動作できる保証にしない。

| 候補 | 採用理由と注意点 | 位置づけ |
| --- | --- | --- |
| sherpa-onnx + SenseVoiceSmall INT8 + VAD | 日本語対応、Android ARM64/C#等の経路あり。公開INT8モデルの例は約228 MiB。非ストリーミング認識を短い発話区間に適用する | 最初の実機試験候補 |
| sherpa-onnx + ReazonSpeech k2 v2 / 日本語Zipformer | 日本語専用、約159Mパラメータ。公式Android simulated-streamingサンプルがある | 日本語精度の比較候補 |
| whisper.cpp + multilingual tiny/baseの量子化 | Android対応。小さいモデルから比較できる。日本語には `.en` モデルを選ばない | メモリ/速度/精度の比較候補 |

根拠: [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx)、[SenseVoiceの配布モデル](https://k2-fsa.github.io/sherpa/onnx/sense-voice/pretrained.html)、[SenseVoiceSmallモデルカード](https://huggingface.co/FunAudioLLM/SenseVoiceSmall)、[ReazonSpeechモデルカード](https://huggingface.co/reazon-research/reazonspeech-k2-v2)、[Androidの疑似ストリーミング例](https://k2-fsa.github.io/sherpa/onnx/android/apk-simulate-streaming-asr-cn.html)、[whisper.cpp](https://github.com/ggml-org/whisper.cpp)。

SenseVoice/ReazonSpeech候補は本来の逐次認識モデルとは区別する。VADで短い区間を作り、必要なら重なり付き再認識で暫定字幕を更新する。射出は確定イベントだけにすれば重複を抑えられる。声を出した瞬間の反応は、音量に連動する未確定の塊で補う案がある。

実装は `ISpeechRecognizer` 相当の交換可能な境界を用意する。マイク入力・16kHzモノラル化・VAD・推論を分離し、推論はメインスレッド外で実行。発話ID、部分結果、確定結果をUnity側へ渡す。音声ファイルは標準では保存せず、ネットワークには確定文字と演出情報のみ送る。

第三者の [Unity-Sherpa-ONNX](https://github.com/Ponyu-dev/Unity-Sherpa-ONNX) は調査用の統合候補。ただしUnity公式SDKではなく、Android IL2CPP対応、ネイティブライブラリ配置、ライフサイクル、ライセンスを採用する版で確認する。ランタイムとモデル重みのライセンスは別々に記録する。

### TTSも追加する場合

日本語読み上げには **VOICEVOX Core** を優先して評価する。公式配布にAndroid arm64のC APIがあり、Unityからネイティブ連携する経路がある。モデル・辞書の配置、初回音声開始時間、音声ごとの利用条件とクレジットを確認する。[VOICEVOX Core](https://github.com/VOICEVOX/voicevox_core)、[Android配布を含むリリース](https://github.com/VOICEVOX/voicevox_core/releases)。

Supertonic 3も候補にはなるが、原作者のリポジトリは2026-09-09にアーカイブされ、サポート終了を明記している。サンプルコードはMIT、モデルはOpenRAIL-Mで異なるため、保守の第一候補にはしない。[原作者のアーカイブとライセンス説明](https://github.com/supertone-oss-archive/supertonic/blob/main/README.md)。Kokoroも「モデルに日本語音声がある」ことと「選んだランタイムが日本語前処理を実装している」ことは別であり、無条件に動作可能とは扱わない。

## 7. 通信の候補と無料範囲

| 案 | 無料範囲 / 費用の性質 | 判断 |
| --- | --- | --- |
| Unity Netcode for GameObjects + Multiplayer Services SDK + Relay | Relayは月平均50 CCUまで。帯域はCCUあたり3 GiB、合計上限150 GiB/月。Lobby帯域も別枠 | 小規模なネット越し共有の第一候補 |
| 同じNGO + Unity TransportでLAN直結 | Relay利用なし。サービス利用量の課金を避けやすいが、LAN設定・接続先指定が必要 | 展示・同室体験の追加モードに向く |
| Photon Fusion | 100 CCU無料プランあり。Fusion/Quantum向け、顧客あたり1アプリ等の条件 | Photonを離れる必然はない。旧PUNからは移植が必要 |
| Mirror | ライブラリはMIT。サーバー・中継・NAT越えは別途設計/運用 | 自前運用をしたい場合の候補 |

Unityの50 CCUは「部屋に50人」「登録ユーザー50人」「瞬間同時接続50人」という意味ではなく月平均。同時に帯域枠を見る。無料枠を超えた場合の挙動はUGS契約/課金状態に依存するため、Dashboardで確認し、無制限無料とは扱わない。

根拠: [Unity公式料金](https://unity.com/products/gaming-services/pricing)、[MPSとNGO/Relayの公式手順](https://docs.unity.com/en-us/mps-sdk/tutorials/relay-and-ngo)、[Photon公式料金条件](https://doc.photonengine.com/photon/current/pricing)、[Mirror](https://github.com/MirrorNetworking/Mirror)。

推奨は **NGO + MPS Sessions + Relay、2〜4人のホスト方式**。新規Unity 6では旧Relay単体パッケージより統合MPS SDKを選ぶ。常時サーバーやDistributed Authorityは初期段階では不要。

まず共有するのは `utteranceId / senderId / text / spawnPose / styleSeed / timestamp / lifetime`。文字長・頻度をホスト側で制限し、同じIDは再生成しない。手や頭の姿勢は低頻度補間、文字の飛び方が演出だけならローカル再生で帯域を節約する。文字同士の衝突結果にゲーム上の意味を持たせる場合はホストの物理結果を同期する。

初期版はホスト退出で部屋終了と明示し、再接続/途中参加でTTL内イベントだけ復元する。Host migrationは追加機能として扱う。音声チャットは元の文字共有とは別で、最初からVivox等を入れる必要はない。

## 8. 実装順序と合格条件

1. **保存・調査（今回）**: 原作履歴保存、欠損一覧、技術候補、公開リポジトリ準備。
2. **Quest最小基盤**: 最新安定Unity/SDKを固定し `QuestApp/` を新規作成。Passthrough、日本語フォント、コントローラー入力、文字発射、単体APKを確認。
3. **音声比較**: 同一の日本語短文をSenseVoice/ReazonSpeech/Whisperで測定し、1種類を採用。マイク拒否・中断・復帰時も破綻しないこと。
4. **MR標準化**: Contextual boundarylessの要求と実状態、再センタリング、スリープ復帰、非対応時の挙動を確認。
5. **2台通信**: 入退室、文字共有、重複排除、途中参加、切断処理。サービス使用量も記録。
6. **VR復活**: 夕日/海岸の軽量なURP表現、境界復帰を伴うMR/VR切替。必要なら同室座標共有やTTSを追加。

音声比較で記録する項目:

- モデルID・revision・SHA256・ライセンス、ランタイム/SDK/Unity/OS版、スレッド数。
- 冷間ロード時間、初回認識時間、発話終了→確定文字までのp50/p95、実時間係数（処理秒数/音声秒数）。
- 文字誤り率、無音時誤認識、短い掛け声、固有名詞、漢字訂正、同じ言葉の繰り返し。
- 実機のピークメモリ、APK/モデル容量、CPU/GPUフレーム時間、15〜30分連続時の熱による速度低下。
- MR描画と2人通信を同時に行った状態で評価する。PC単体速度は補助資料。

暫定の体験目標は72Hz維持、確定までp95 1秒程度以内、連続時も認識待ちが積み上がらないこと。これは測定結果ではなく採用判断の目標で、精度と発熱の結果で調整する。

## 9. まだ確認していないこと

- 旧配布版の起動とUnityでの完全な参照検証。
- 最新Unity/Meta SDKの具体的な固定版とQuest 3実機での動作。
- 旧アセットごとの再配布許諾、採用モデルの配布物ごとの条件。
- UGSアカウントの料金プラン・使用量・上限時の挙動。
- ネット越し中心か、同室で空間を共有する遊びが中心かという最終仕様。

これらを確認する前に「Quest対応済み」「リアルタイム性能達成」「無料で無制限」とは表記しない。
