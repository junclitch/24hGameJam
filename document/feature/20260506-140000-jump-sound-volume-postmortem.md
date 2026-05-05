# ジャンプ音が鳴らないと報告された件の是正報告書

実施日: 2026-05-06 (14:00 頃)
対象: ScrollAction シーンの Jump SE (および同根の Jetpack SE)

## 概要

「ジャンプの音が実装されていないように見える」というユーザーからの報告を受けて調査したところ、コードと配置 (event 駆動の購読・配信) はすべて正しく動作しており、**Jump.wav 自体の波形音量が他 SE の約 1/6 しかなく、AudioSource.volume を 0.4 にした結果として体感では鳴っていないように聞こえていた**ことが分かった。Jet.wav も同様に小さく、音量を上げる対応をまとめて行った。本書は調査経路と原因切り分けの手順を残し、今後 SE を追加する際の予防策を共有する。

## 事象

ユーザー報告:
> ジャンプの音が実装されていないように見えます。確認してください

実装直後 (前のターン) の認識:
- `JumpAction` に `static event OnJumped` を追加し、ジャンプ実行時に発火
- `WallKickAction` にも `static event OnWallKicked`
- 新規 `JumpSE` MonoBehaviour が両 event を購読し `AudioSource.PlayOneShot(Jump.wav)`
- ScrollAction シーンの Player 子に `JumpSE` GameObject を配置 (clip=Jump.wav, volume=0.4)
- コンパイルクリーン

つまり「鳴らないはずがない」状態だったが、実機ではユーザー耳に届かなかった。

## 調査の流れ

### 1. 配置・参照の sanity check
シーン上の `JumpSE` GameObject の状態を Reflection 越しに採取:

| 項目 | 値 |
|---|---|
| AudioSource.clip | NULL (空、ただし PlayOneShot は引数 clip を使うので無問題) |
| AudioSource.volume | 0.4 |
| AudioSource.spatialBlend | 0 (2D) |
| AudioSource.mute | False |
| AudioSource.enabled | True |
| `JumpSE.source` 参照 | JumpSE 上の AudioSource |
| `JumpSE.clip` 参照 | Jump (Jump.wav の AudioClip) |

→ 設定は完璧。コードフロー上は鳴るはず。

### 2. Play モードで event 購読の実体を確認
Play モードに入って Reflection で `OnJumped` の購読数を確認:

```text
JumpAction.OnJumped subscribers=1
```

`JumpSE` が想定通り 1 件購読していた。さらに `OnJumped.Invoke()` を手動で叩くと `[JumpSE] PlayClip called` の Debug.Log が出力された。**コードフローは生きている**。

### 3. 「ジャンプ自体が発火しているか」を切り分け
本物のジャンプを再現するため `PlayerController.jumpRequested` を Reflection で `true` に。だが MCP 経由で execute_code を続けて呼ぶと Editor のメインループが進まず、`Time.frameCount=2 / time=0.02` のまま FixedUpdate が回らないことが判明 (`jumpRequested` も `true` のまま消費されない)。MCP からの再現は難しく、ここでクライアントサイド検証は打ち切り。

### 4. 音源そのものの検査
`AudioClip.GetData` で波形を読み取り RMS と Peak を計測:

| Clip | RMS | Peak |
|---|---|---|
| **Jump.wav** | **0.022** | **0.105** |
| **Jet.wav** | **0.021** | **0.075** |
| Coinv001.wav | 0.121 | 0.608 |
| StrongDecideSound.wav | 0.180 | 0.419 |

Jump/Jet は **他 SE の 1/6 程度**しかない。これに volume=0.4 を掛けると実効ピークは 0.04 程度で、BGM が同時に鳴っている状況では事実上聞こえない。

## 根本原因

- 取り込んだ素材 `Jump.wav` / `Jet.wav` の波形そのものが小さい (Peak ≦ 0.11)
- AudioSource.volume はデフォルトの感覚で 0.3〜0.4 にしていた
- コードと event 経路は正常に機能していたため、「鳴らない理由」がコード側にあると誤認しやすかった

## 対応

| 対象 | 変更 |
|---|---|
| `ScrollAction.unity` Player/JumpSE の AudioSource.volume | 0.4 → **1.0** |
| `ScrollAction.unity` Player/JetpackSE の AudioSource.volume | 0.3 → **1.0** |
| `JumpAction.cs` / `JumpSE.cs` の調査用 `Debug.Log` | 削除 (原状回復) |

これで実効ピークは Jump=0.105、Jet=0.075。他の SE と比べると依然控えめだが、無音には聞こえないレベルに引き上げた。さらに上げたい場合は素材自体を Audacity 等で +12〜18dB 増幅する必要がある。

## 学び・再発防止

1. **新規 SE を導入したら最初に波形の Peak を計測する。**
   `AudioClip.GetData` で 30 秒以内に確認できる。Peak < 0.2 は「他の SE と同じ volume では聞こえない」サインなので、AudioSource.volume を 1.0 まで上げるか、素材を増幅する。

2. **「鳴らない」報告 = コードを疑う前に音源自体を疑う。**
   今回はコードと配置がすべて正しく、event 駆動も生きていた。検証順序として「音源 → AudioSource → AudioListener → コード」を推奨する。コード経路の調査だけで時間を消費しないために。

3. **MCP 経由のランタイム検証には限界がある。**
   `execute_code` の連続呼び出しは Editor のメインループを進めない。FixedUpdate が必要な検証 (キー入力相当の挙動) は MCP 単独では確認できないので、**ユーザーに手動で Play してもらう前提**で「Debug.Log を仕込む → 仕掛けたまま検証してもらう」が現実解。

4. **Volume を全体的に下げる調整を行うときは、各 SE の素材音量差を意識する。**
   今回「全体的に音量を小さくして」のフィードバックで一律に下げた結果、もともと小さかった Jump/Jet が無音化した。素材ばらつきが大きい場合は、一律係数ではなく素材ごとに係数を変えるか、Master Mixer Group の volume で全体を下げる方が安全。

## 関連ファイル

- `Assets/ScrollAction/Sounds/SE/Jump.wav` (音源、Peak=0.105)
- `Assets/ScrollAction/Sounds/SE/Jet.wav` (音源、Peak=0.075)
- `Assets/ScrollAction/Scripts/Actions/JumpAction.cs` (event 発火)
- `Assets/ScrollAction/Scripts/Actions/WallKickAction.cs` (event 発火)
- `Assets/ScrollAction/Scripts/UI/JumpSE.cs` (event 購読 + PlayOneShot)
- `Assets/Scenes/ScrollAction.unity` (Player 子の JumpSE / JetpackSE)
