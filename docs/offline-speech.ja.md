# 端末内の日本語音声認識

2026-11-01、SenseVoice Small INT8とSilero VADを組み込みました。話し終えた短い発話を文字にして発射します。クラウドAPI、従量課金、音声アップロードはありません。Quest実機のマイク・速度・精度・発熱は未検証です。

## 構成と選定

| 要素 | 固定したもの | 用途 |
| --- | --- | --- |
| 認識モデル | `sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2024-07-17` | SenseVoice Small、言語を日本語 `ja` に固定、句読点あり |
| 発話検出 | `silero_vad.onnx` | 無音約0.45秒で区切り、最小0.20秒、長い発話は約6秒で分割 |
| 推論 | sherpa-onnx 2.0.1 / ONNX Runtime 1.28.2 | ARM64 CPU、認識2スレッド、VAD 1スレッド |
| Unityとの接続 | 同じバージョンの公式C# C APIバインディング | Windows Editorで実認識を検証し、AndroidはIL2CPPでビルド |

SenseVoiceは非自己回帰型で、日本語を含む複数言語に対応します。逐次確定前の文字表示は行わず、VADで切った発話ごとの確定結果だけを使います。[公式モデル資料](https://k2-fsa.github.io/sherpa/onnx/sense-voice/pretrained.html)の2025年版は広東語を追加学習したモデルであり、日本語用途では2024年版を採用しました。

この選択は実機評価前の初期構成です。Quest 3上で発話終了から表示までの遅延、騒音下の日本語精度、MR描画と並行した負荷を測り、必要ならモデルを交換します。PC上の速度からQuestの速度は断定しません。

## 操作とデータ

「音声 ON / OFF」を押し、マイク権限を許可します。初回はAPK内のモデルをアプリ専用ストレージへ展開し、SHA-256で検証してから読み込みます。端末でのインターネット接続やモデルダウンロードは不要です。モデル展開用に約240 MBの追加ストレージを使用します。

モデル準備中、聞き取り中、変換中をパネルに表示します。発話が確定するとローカルで文字を発射し、LANに接続していれば同じテキストと色を相手に送ります。音声入力OFF、アプリの一時停止、マイク切断で録音を停止します。OFF前に処理中だった結果は破棄します。復帰後の録音開始は操作が必要です。

録音はメモリ上の短いバッファだけです。音声ファイル保存・音声のネット送信はしません。ログには認識処理時間のみを出し、ユーザーの発話テキストを記録しません。パネルには最後の結果の冒頭を表示します。

音声処理は描画とは別のワーカースレッドで行います。入力待ちは約3秒分、結果待ちは8件に制限します。処理が追いつかない場合は古い入力を捨て、パネルに表示します。マイクのリングバッファを一度だけ順番に読み、実際のサンプルレートから16 kHzモノラルへ連続変換します。

## 再現手順

Unityを起動する前に、Python 3.12以降で次を実行します。

```powershell
python tools/setup_speech.py
```

公式配布物を `.work/asr/` に取得し、固定SHA-256を確認してから必要ファイルを配置します。モデル・ネイティブライブラリはGitに含めません。C#バインディング、Importer設定、ライセンスはGitに含めます。APKのビルド前検査でモデル欠損・不一致を検出し、音声機能のないAPKを誤って生成することを防ぎます。

取得先は [sherpa-onnx 2.0.1](https://github.com/k2-fsa/sherpa-onnx/releases/tag/v2.0.1) と [asr-models](https://github.com/k2-fsa/sherpa-onnx/releases/tag/asr-models) です。C#ソースも2.0.1に固定し、Unity C# 9向けの構造体コンストラクター、C呼出規約、初期化失敗の検出を補っています。

Windows Editorでは隣接DLLを検索する明示ロードを使います。この処理は `UNITY_EDITOR_WIN` 内のみで、Quest APKには含まれません。Androidでは同梱したARM64ライブラリを使います。

## 検証範囲

- 16 / 44.1 / 48 kHz入力の連続リサンプリングと、入力を分割しても同じ出力になることを自動検査。
- 配布モデルの日本語 `test_wavs/ja.wav` を実際のVADとネイティブ認識エンジンへ流し、日本語の確定結果が返ることをWindows Unity Editorで確認。
- 約5.924秒の検出発話に対する認識部分は、このPCでは約0.200秒。マイク入力、VADの発話終了待ち、Questの描画負荷を含む測定ではありません。
- リセット後の結果破棄、無音で発射しないこと、ワーカー終了を検査。
- APK内のARM64認識ライブラリ、3つのモデルファイルのハッシュ、ライセンスの存在を `tools/inspect_apk.ps1` で検査する。
- `tools/inspect_native_speech.py` で、APKから取り出した2つの音声ライブラリがELF64/AArch64であり、依存ライブラリ名がAPKまたは最小API 32のNDK公開ライブラリに存在し、C#の呼出先25関数がexportされていることを確認。[0.5.0の検査結果](validation/quest-0.5.0-native-speech.json)。ネイティブコードを実行する検査ではなく、構造体の配置や実機ロード・推論成功の保証ではありません。

最後の静的検査は、APK生成後に `python tools/inspect_native_speech.py` で再実行できます。対象APKのSHA-256を含む `.apk.native-speech.json` をAPKと同じフォルダーへ出力します。Unityの場所が異なる場合は `--android-player` を指定してください。

Quest 3での確認項目は、日本語の短文・長文・無音・周囲の会話、音声ON/OFFの連打、許可拒否後の再操作、スリープ復帰、モデル初回展開、MRとLAN通信の同時利用です。

## ライセンス

SenseVoiceモデルは **FunASR Model Open Source License Agreement v1.1という独自条件**です。Apache/MITとして扱いません。出典・作者・モデル名の保持に加え、独自の行動条項と改訂条項があります。[モデルカード](https://huggingface.co/FunAudioLLM/SenseVoiceSmall)が指定する[モデル利用条件](https://github.com/modelscope/FunASR/blob/main/MODEL_LICENSE)を参照してください。

sherpa-onnxはApache-2.0、Silero VADとONNX RuntimeはMITです。APK内の `Speech/Notices/` に取得時の条件、出典、ONNX Runtimeの第三者表示を同梱しています。リポジトリ全体をこれらのライセンスへ変更するものではありません。
