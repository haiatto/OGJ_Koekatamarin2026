> 0.7.0更新: 原作の5層TextMeshとWater4を移植し、みかちゃん-PB・ラノベPOP・Noto Sans JPの切替を追加しています。[現在の表現と変更範囲](legacy-appearance-port.ja.md)を参照してください。以下は0.6.0以前の記録を含みます。

# 原作の声・衝突・水面の再調査

2026-11-02、保存済み原作ソース（元コミット `c07556014bbb4a17f4973f38aac01426bd11b4ea`）を確認しました。原作の `New Unity Project/` は変更しません。現代版のUnityプロジェクトは `QuestApp/` です。

## 文字の塊

`Assets/Game/Prefabs/text.prefab` は `Text000` / `Text025` / `Text050` / `Text075` / `Text100` の5枚のTextMeshを持ちます。各面の `m_OffsetZ` は -0.5、-0.25、0、0.25、0.5、親のZスケールは0.2。実質的に約20cmの奥行きに文字面を重ねていました。立体文字を押し出したメッシュではなく、同じ文字の積層です。

各TextMeshの文字サイズは1、フォントサイズ100、XYスケール0.1。旧TextMeshとTMPではフォント単位が異なるため、現代版の絶対的な文字寸法まで同一だとは扱いません。

`TextManage.cs` は全5面の文字・色を変更し、5秒後にTextMeshと自身を破棄します。親GameObjectやRigidbodyまで破棄するコードではありません。`KoeKatamarinBehaviourScript.cs` の単独プレイ経路では、外側を黒・中央面を白にしています。通信経路の `multiShoot.cs` はPrefabの色を使い、約1/3の確率で全体を赤へ変更します。単独・通信で色の扱いが異なります。

0.5.0はこの構造を再現しておらず、TMP1枚、視線への追従回転、最後1秒のフェードでした。0.6.0ではTMP5面、発射時の向き、黒い外層と白い中央層、5秒後のプール返却へ変更します。原作の色のランダム変更は復元対象に含めていません。

## 発射と衝突

`shoot.cs` / `multiShoot.cs` はPlayer位置と回転でPrefabを生成し、Rigidbody速度に `rotation * (0, 5, ShootPow)` を設定します。`ShootPow` の既定値は10です。`shoot.cs` はDK1の予測姿勢を取得していますが、その取得値を発射回転には使用していません。

PrefabのRigidbodyは質量1、重力ON、ドラッグ0、回転制約なしです。BoxColliderはサイズ(1,1,1)で、親のZ縮尺0.2が掛かります。0.5.0の位置加算・簡易床反射はこのRigidbody衝突を代替していませんでした。

0.6.0はRigidbody・BoxCollider・連続衝突検出を使い、初速(0,5,10)と重力、衝突時の回転を復元します。ColliderのXYは認識した文字の実際の範囲に合わせます。発射位置は現代化の要望に合わせ、左右の目の中心から視線方向へ40cm、下へ8cm。頭の位置・向きと仮想移動を反映します。視線の上下回転も使用するため、DK1版のPlayer回転のみの発射とは異なります。

MRではMRUKが読み込む部屋のグローバルメッシュを優先し、ない場合は壁・床・家具の面とボリュームから当たり判定を生成します。Questの空間データ許可と部屋設定が必要です。見えている全物体をリアルタイムに測る機能ではなく、移動した家具・人物などは保存済み部屋データと一致しない場合があります。部屋データは保存・送信しません。VRでは部屋のColliderを無効にし、砂浜と水面のColliderを使用します。水面のColliderは静的メッシュで、シェーダーの小波には追従しません。

文字の発射はLANへ送りますが、その後の物理衝突は端末ごとの計算です。衝突結果の完全なネットワーク同期は未実装です。

## 声量

`server/client.html` はwebkitSpeechRecognitionによる認識文字を送り、Unity側は文字列として受け取ります。確認したゲームスクリプトにはマイク振幅・声量による文字サイズや初速変更がありません。現代版もRMS音量表示のみで、声量から大きさへの反映は未実装です。

## 海

原作の水面は `Standard Assets/Water (Pro Only)/Water4` の `FX-Water4.shader` / `FX-SimpleWater4.shader`、`WaterInclude.cginc` と、`WaterBase` / `WaterTile` / `PlanarReflection` / `GerstnerDisplace` 等の構成です。シェーダーにはGerstner波、法線テクスチャ、Fresnel、泡、反射テクスチャ等があります。`PlanarReflection` は表示カメラに対応する反射カメラと、画面の半解像度のRenderTextureを作ります。旧シーンには左右目用の反射カメラも残っています。

現代版の `SunsetOcean.shader` は新規URPシェーダーです。小さな正弦波による頂点変形、色・太陽方向から計算する反射表現を使い、原作のPlanarReflectionは移植していません。原作の夕景・砂浜画像と空のメッシュは再利用していますが、海まで原作同一という説明は不正確です。0.6.0でもこの区別を維持し、水面表現を原作準拠へ移植したとは扱いません。
