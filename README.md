# OGJ_Koekatamarin2026

2013年のOculus DK1作品「コエカタマリン」を、Meta Quest 3向けに復活させるプロジェクトです。

原作: [haiatto/OGJ_Koekatamarin](https://github.com/haiatto/OGJ_Koekatamarin)

原作の取得コミット: `c07556014bbb4a17f4973f38aac01426bd11b4ea`

## 現在の状態

**初期調査段階です。Quest対応版はまだ実装・ビルドされていません。**

原作の履歴とファイルを保存しています。同じ所有者のため、GitHubのFork機能ではなく、履歴を引き継ぐ別リポジトリとして管理します。

- Quest 3単体動作、MRを標準、旧VR体験をオプションにする計画です。
- 原作の中心は「音声認識 → 日本語の文字を塊として飛ばす」です。読み上げ（TTS）は別機能です。
- 日本語音声認識は端末内推論を比較し、通信は同じWi-Fi内のQuest同士によるP2Pを前提にします。Unity Relayは採用候補から除外します。
- 元のUnity 4プロジェクトを最新Unityでそのまま開く前に、[調査報告](docs/revival-investigation.ja.md)を参照してください。

## 構成

| パス | 内容 |
| --- | --- |
| `New Unity Project/` | 保存した旧Unityプロジェクト。新規開発先ではありません |
| `ReleseFolder/` | 当時のWindows/Mac配布物とサーバー |
| `server/` | Chrome音声認識とUnityを接続していた旧Node.jsサーバー |
| `docs/revival-investigation.ja.md` | 欠損、技術選定、移植順序、実機検証計画 |
| `docs/legacy-audit.json` | 原作コミットに対する静的検査結果 |
| `tools/audit_legacy.py` | 標準Pythonだけで実行できる読み取り専用検査 |

検査の再実行: `python tools/audit_legacy.py`。Unityでのインポート・ビルド成功を判定するものではありません。

## 原作の取り扱い

旧SDK、第三者素材、フォント、配布実行ファイルは原作の資料として含まれます。リポジトリ全体を新たにMIT等で再ライセンスしていません。2026版への再同梱可否は素材ごとに確認し、旧証明書・旧サーバー・旧ネイティブプラグインは新アプリに流用しません。
