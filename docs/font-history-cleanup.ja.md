# HGフォントの公開履歴からの除外

2026-11-03。ユーザーの「消しといて。履歴ごと消せる？」「とりあえずフォントだけ消せばよし」に基づく整理です。対象は `haiatto/OGJ_Koekatamarin2026`。海・夕景・砂浜などの素材は変更対象に含めません。

## 削除対象

- `New Unity Project/Assets/Game/HGRPP1001.TTF` とその `.meta`
- `New Unity Project/TTC-TTF/HGRPP1.TTC`
- 同フォルダの `HGRPP1001.TTF`、`HGRPP1002.TTF`、`HGRPP1003.TTF`
- `New Unity Project/test_Data/sharedassets0.assets`
- `ReleseFolder/koekatamariProto_Data/sharedassets0.assets`
- `ReleseFolder/koekatamariProtoForMac.app/Contents/Data/sharedassets0.assets`
- 過去の `New Unity Project/Library/metadata/4f/4f8dfd3e06bae9b4081f39e19325d4cd`

フォント名テーブルでHG/HGP/HGS創英角ポップ体と著作権表示を確認し、全コミットのファイル内容とUnityパッケージ・ZIPの展開内容を調べました。埋め込み入り旧データはフォント以外も含むコンテナのため、ファイル全体を公開対象から外します。旧配布物は起動用の完全な一式ではなくなります。旧Prefabのフォント参照も欠けるため、そのまま旧プロジェクトを開いて動くという意味ではありません。

ファイル名による除外に加え、検出した9種類のフォント入りGit blobを内容IDで除去し、別パスの同一コピーも公開履歴から外します。現在のQuest用3書体（みかちゃん-PB、ラノベPOP旧版、Noto）とTMP同梱Liberation Sansは維持します。旧Photonデモのeurofurenceは今回のHG除外対象には含めません。これは全第三者素材の許諾確認が完了したという記録ではありません。

## 履歴を書き換えた後の扱い

- 古いcloneをそのままmerge/pushすると削除前の履歴を再公開する可能性があります。別のPCでは新しくcloneしてください。未公開作業がある場合は、必要な変更だけを確認して移します。
- 整理前のGit bundleとローカル編集のバックアップは、Git管理外の `.work/font-removal-20261103/` に保存します。公開リポジトリへ追加しないでください。
- 元の別リポジトリ `haiatto/OGJ_Koekatamarin`、第三者のclone、GitHubの過去SHAのキャッシュはこの履歴更新で消えません。元リポジトリへの変更は今回の対象外です。
- 過去の調査資料・素材の由来に記載した元コミットIDは、元リポジトリを指す歴史的情報です。整理後リポジトリ内のコミットIDとは一致しません。

GitHubも、履歴書き換えとforce pushだけでは別のcloneや過去SHAのキャッシュまで除去できないと説明しています。[GitHubの説明](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/removing-sensitive-data-from-a-repository)。
