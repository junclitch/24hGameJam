# ゲームサイクル整備 + UI/オーディオ実装 セッションまとめ

実施日: 2026-05-05 〜 2026-05-06 (深夜 〜 翌午後)
対象シーン: `Title.unity` / `Shop.unity` / `ScrollAction.unity` / `GameOver.unity` / `GameClear.unity`
関連是正: [Jump 音量是正報告書](20260506-140000-jump-sound-volume-postmortem.md)

## 概要

ScrollAction プロジェクトに**ゲームサイクル一式 (落下死・ゴールクリア・遷移先統一・リトライ初期化)、UI 強化 (操作説明オーバーレイ・フォント拡大)、価格バランス調整、オーディオ全般 (BGM/SE)** を一気に実装したセッションの記録。1 機能ずつでは小さい変更だが、**ScrollAction が「ステージにエントリ・ゴール・リトライ・装備リセット・音」を備えた最低限のゲームの形**になったので、構成の俯瞰として残す。

## 1. ゲームサイクル (Y<-10 → GameOver / Goal → GameClear)

### 目的
- ScrollAction シーンで Player.Y が **-10 を下回ったらゲームオーバー**シーンへ
- ゴール (画面右端の `GoalMarker`) に**接触 + 接地**でクリアシーンへ

### 変更
- 新規 `Assets/ScrollAction/Scripts/Scene/GameCycleManager.cs`
  - Player の Y を毎フレーム監視、閾値未満で `SceneManager.LoadScene(gameOverSceneName)`
  - `GoalTrigger.OnGoalReached` を購読してクリアシーンへ
  - `transitioning` フラグで二重発火を防止
- 新規 `Assets/ScrollAction/Scripts/Scene/GoalTrigger.cs`
  - `OnTriggerEnter2D` で Player を `insidePlayer` に保持
  - `Update` で `insidePlayer.IsGrounded == true` になった瞬間 static event 発火
  - 「ジャンプで触れただけで誤発火」を防止
- ScrollAction シーン:
  - `GameCycleManager` GameObject を新規 (player ref / threshold=-10 / GameOver / GameClear)
  - `GoalMarker` に `BoxCollider2D` (Trigger, size=25 → world 2×2) と `GoalTrigger` を追加
  - 既存の `KillZone` (リスポーン用) は `SetActive(false)` に変更

### 設計判断
- リスポーン挙動は **Shop シーンのみ**に限定 (ScrollAction では落下=ゲームオーバー一択)
- Goal の当たり判定は最初 50×50 (4×4 world) → 「広すぎる」FB を受け 25×25 (2×2 world) に縮小
- ゴール判定に接地条件を入れて空中接触を弾く

## 2. 遷移先を Shop に統一

### 目的
Title・GameOver・GameClear のスタートボタンから直接 ScrollAction に飛ばさず、**Shop で装備を整えてから本編へ**進ませる導線にする。

### 変更
- `TitleManager.StartGame`: `LoadScene("ScrollAction")` → `LoadScene("Shop")`
- `GameClearManager.StartGame`: 同上 (GameClearManager は GameOver/GameClear 両シーンで共用)

## 3. Shop でリトライ時の Inventory リセット

### 目的
リトライ時 (GameOver → Shop → ScrollAction) で `GroundCheckAction` が抜けて地面をすり抜ける事故が発生していた。Domain Reload 依存の static フラグに依拠していた `EnsureInitializedThisSession` の脆さが露呈。

### 変更
- 新規 `Assets/ScrollAction/Scripts/Shop/ShopSessionResetter.cs`
  - `Awake` で `inventory.ResetToDefaults()` を呼ぶ
- Shop シーンに `SessionResetter` GameObject を追加 (inventory = `ActionInventory.asset`)

### 効果
Title・GameOver・GameClear どこから来ても、Shop に入った時点で必ず Defaults (`HorizontalMove`, `GroundCheck`, `Jump`, `Crouch` + 700G) に戻る。「リトライ = Shop で装備買い直し」のゲーム性が確定。

## 4. 価格バランス + 「歩き」リネーム

### 目的
プレースホルダー価格 (20-50G) を、敵なし・アスレチック前提の価値判断で再設定。所持金で**全部は買えず戦略を悩める**バランスに。

### 変更 (DisplayName)
- `HorizontalMoveAction.DisplayName`: 「左右移動」 → **「歩き」**

### 価格表 (買値 = 売値)

| アクション | 旧 | **新** |
|---|---|---|
| 歩き | 30 | **250** |
| 接地判定 | 30 | **250** |
| ジャンプ | 20 | **120** |
| しゃがみ | 30 | **40** |
| ジェットパック | 50 | **300** |
| ワープ | 50 | **220** |
| 壁キック | 50 | **200** |
| グライダー | 40 | **180** |
| スライディング | 40 | **120** |
| 転がる | 35 | **120** |
| 空中回避 | 30 | **100** |
| 地上回避 | 25 | **80** |
| 所持金 (initialMoney) | 1000 | **700** |

### 設計判断
- 歩き・接地判定は売却で詰むレベル → Jetpack に次ぐ高額に設定して「捨ててリスクを取る選択」に意味を持たせる
- Defaults の歩き+接地+ジャンプ+しゃがみ (660G 相当) が**無料で復元**されるので、所持金 700G は「追加で 2-3 個買える」緊張感のある量

## 5. 所持アクションの操作説明オーバーレイ

### 目的
Shop で買った装備の**キー操作を画面に常時表示**する。

### 変更
- `PlayerAction.HelpText` (`virtual string`, 空文字デフォルト) 追加
- 各 12 アクションで `HelpText` を override (例: ジャンプ「Space / ↑ / W」、ワープ「C で進行方向にワープ」)
- 新規 `Assets/ScrollAction/Scripts/UI/ActionHelpOverlay.cs`
  - OnGUI で `inventory.owned` を走査、count>0 のスロットだけ列挙
  - 自前 GUIStyle (fontSize=18) で `GUI.skin` のグローバル汚染を回避
- Shop / ScrollAction の各シーンに `ActionHelpOverlay` GameObject 配置

### 配置の変遷
- v1: 画面右上 → ユーザー FB「右上はダサい」
- v2: **画面下中央**に変更 (現状)

## 6. UI フォント拡大

### 目的
Shop / Jetpack ゲージ含めて全体的にフォントが小さく読みにくいので大型化。

### 変更
- `Shop.cs` の OnGUI を自前 GUIStyle 化 (label/box/button、fontSize=18)、panelW 420→**560**、rowH 32→**40**
- `JetpackGaugeUI.cs` ラベル fontSize=20、`DefaultBarRect` (240,28)→(**320,40**)
- ScrollAction / Shop の Player.JetpackGaugeUI の barRect も新サイズに更新

## 7. ジェットパック購入時 100% + 燃料量適正化

### 目的
- Shop で Jetpack を買った瞬間に燃料が 0 のまま (UI が 0%) だった
- 既存 `maxFuel=20` だと 1 噴射で 40 unit 上昇でき、**ステージ高さ 10.7 unit に対し過剰**

### 変更
- `PlayerAction.OnPurchased()` (`virtual`) を追加
- `JetpackAction.OnPurchased() => currentFuel = maxFuel;`
- `ActionInventory.Buy()` 内で `action.OnPurchased()` を呼び出し
- `Jetpack.asset` の `maxFuel`: 20 → **5** (riseSpeed=2 のままで 1 噴射 10 unit)

### ステージ計測 (参考)
- ステージ bounds: X[-27.0, 106.8] (133.8 unit) / Y[-3.0, 7.7] (10.7 unit)
- Player スタート Y=1.9
- → maxFuel=5 で「縦をほぼ覆える」が「1 回ですべて飛び越せはしない」レベルに

## 8. オーディオ実装

### 8.1 BGM (Shop / ScrollAction)
- 各シーンに `BGM` GameObject + AudioSource (clip=`BGM_ScrollAction.wav`, loop=true, playOnAwake=true)
- 各シーン独立 AudioSource (シーン遷移で頭からリスタート、シームレス継続は将来課題)

### 8.2 GameOver SE
- `GameOver` シーンに `GameOverSE` GameObject + AudioSource (clip=`24hGameJam_GameOver_v001.wav`, loop=false, playOnAwake=true)

### 8.3 クリア時のコイン連発
- 新規 `Assets/ScrollAction/Scripts/UI/CoinShowerSE.cs`
  - `Update` で interval ごとに `PlayOneShot(coinClip)`
  - interval=0 なら `clip.length` を自動採用 (重ならない最短間隔)
- `GameClear` シーンに `CoinShower` GameObject 配置 (clip=`Coinv001.wav`, interval=0.1 = ジャラララ感)

### 8.4 ジェットパック噴射中の Jet.wav ループ
- `PlayerActionContext.isJetpacking` フィールド追加
- `JetpackAction.OnFixedTick`: 燃料を消費するフレームで `ctx.isJetpacking = true`
- `PlayerController.IsJetpacking { get; private set; }` 公開プロパティ追加
- 新規 `Assets/ScrollAction/Scripts/UI/JetpackSE.cs`
  - `Update` で `IsJetpacking` を見て `Play` / `Stop` を切替
- ScrollAction の Player 子に `JetpackSE` 配置

### 8.5 ジャンプ・壁キックの一発 SE
- `JumpAction` に `public static event Action OnJumped;` 追加、ジャンプ実行時に発火
- `WallKickAction` に `public static event Action OnWallKicked;` 追加、キック実行時に発火
- 新規 `Assets/ScrollAction/Scripts/UI/JumpSE.cs`
  - 両 event を購読し `PlayOneShot(Jump.wav)`
- ScrollAction の Player 子に `JumpSE` 配置

### 8.6 タイトル・GameOver・GameClear のボタン押下 SE
- 新規 `Assets/ScrollAction/Scripts/UI/DecideSoundPlayer.cs`
  - `DontDestroyOnLoad` な singleton AudioSource をコードで生成
  - `PlayOneShot` 直後に `LoadScene` しても音切れしない
- `TitleManager` / `GameClearManager` に `[SerializeField] AudioClip _decideClip;` 追加、各操作冒頭で `DecideSoundPlayer.Play(_decideClip)` 呼び出し
- Title / GameOver / GameClear の各 Manager インスペクタに `StrongDecideSound.wav` を割当て

### 8.7 AudioListener 不在の事故と修正
- BGM が鳴らない報告 → 調査結果、**ScrollAction / Shop の Main Camera に AudioListener が無かった** (GameOver / GameClear / Title にはあった)
- 全 Main Camera に `AudioListener` を追加

### 8.8 音量バランス調整 (一律ダウン → Jump/Jet だけ補正)
当初:

| 対象 | volume |
|---|---|
| BGM (Shop/Scroll) | 0.7 |
| GameOver SE | 0.8 |
| CoinShower | 0.7 |
| JetpackSE | 0.6 |
| JumpSE | 0.7 |
| DecideSoundPlayer | 1.0 (default) |

「全体的に小さく」FB を受けて一律ダウン:

| 対象 | volume |
|---|---|
| BGM | **0.3** |
| GameOver SE | **0.5** |
| CoinShower | **0.4** |
| JetpackSE | 0.3 → 後に **1.0** |
| JumpSE | 0.4 → 後に **1.0** |
| DecideSoundPlayer | **0.5** (コード) |

ジャンプ音が「鳴らない」報告 → `Jump.wav` Peak=0.105 / `Jet.wav` Peak=0.075 と他SE (0.4-0.6) に比して小さい素材だったため、JumpSE/JetpackSE のみ volume=1.0 に補正。詳細は [是正報告書](20260506-140000-jump-sound-volume-postmortem.md) 参照。

## 新規スクリプト一覧

| ファイル | 役割 |
|---|---|
| `Scripts/Scene/GameCycleManager.cs` | Y<閾値 → GameOver、Goal → GameClear の遷移管理 |
| `Scripts/Scene/GoalTrigger.cs` | 接地待ちでクリア発火する Trigger |
| `Scripts/Shop/ShopSessionResetter.cs` | Shop 入場で Inventory を Defaults リセット |
| `Scripts/UI/ActionHelpOverlay.cs` | 所持アクションの操作説明常時表示 |
| `Scripts/UI/CoinShowerSE.cs` | クリア時のコイン連発 |
| `Scripts/UI/JetpackSE.cs` | 噴射中だけ Jet.wav ループ |
| `Scripts/UI/JumpSE.cs` | ジャンプ/壁キックで Jump.wav 一発 |
| `Scripts/UI/DecideSoundPlayer.cs` | DontDestroyOnLoad なシーン遷移用 SE |

## 既存スクリプトの変更点

| ファイル | 変更 |
|---|---|
| `PlayerAction.cs` | `HelpText` (virtual) と `OnPurchased()` (virtual) を追加 |
| `PlayerActionContext.cs` | `isJetpacking` フィールド追加 |
| `PlayerController.cs` | `IsJetpacking` 公開プロパティ追加、ctx に `isJetpacking` をリセット/反映 |
| `ActionInventory.cs` | `Buy()` 内で `action.OnPurchased()` を呼び出し |
| `JumpAction.cs` | `static event OnJumped` 追加 + 発火、HelpText, 価格更新 |
| `WallKickAction.cs` | `static event OnWallKicked` 追加 + 発火、HelpText, 価格更新 |
| `JetpackAction.cs` | `OnPurchased` で燃料リセット、`isJetpacking` フラグセット、HelpText |
| `HorizontalMoveAction.cs` | DisplayName 「左右移動」→「歩き」、HelpText |
| 各他 Action | HelpText 追加、価格更新 |
| `Shop.cs` | OnGUI を自前 GUIStyle 化 (フォント 18、パネル拡大) |
| `JetpackGaugeUI.cs` | フォント 20、デフォルトバー拡大 |
| `TitleManager.cs` | StartGame の遷移先 Shop に変更、`_decideClip` で SE 再生 |
| `GameClearManager.cs` | StartGame の遷移先 Shop に変更、`_decideClip` で SE 再生 |

## アセット側の変更

| ファイル | 変更 |
|---|---|
| `ActionInventoryDefaults.asset` | `initialMoney` 1000 → **700** |
| 各 Action `.asset` | buyPrice / sellPrice を新価格に (詳細は §4 価格表) |
| `Jetpack.asset` | `maxFuel` 20 → **5** |
| `ScrollAction.unity` | GameCycleManager / GoalMarker (Trigger+GoalTrigger) / KillZone 無効化 / ActionHelpOverlay / BGM / JetpackSE / JumpSE / Player.JetpackGaugeUI.barRect / Main Camera AudioListener |
| `Shop.unity` | SessionResetter / ActionHelpOverlay / BGM / Main Camera AudioListener / Player.JetpackGaugeUI.barRect |
| `GameOver.unity` | GameOverSE / Manager._decideClip |
| `GameClear.unity` | CoinShower / Manager._decideClip |
| `Title.unity` | Manager._decideClip |

## ゲームの遷移グラフ (本セッション後)

```
       ┌──────── Title ────────┐
       │                        │
       ▼ Start                  │ Title
     Shop ◄──────── Start ──────┤
       │                        │
       ▼ 画面右に出る           │
   ScrollAction                 │
   ├── Y < -10 ──► GameOver ────┤ Retry/Title
   └── Goal+接地 ► GameClear ───┘ Start/Title
```

## 学び・次に活かす点

1. **Domain Reload 依存の static フラグは脆い**: `EnsureInitializedThisSession` のような「セッション全体で 1 回だけ」のロジックは Enter Play Mode Options やシーン遷移経路次第で簡単に壊れる。明示的なリセットポイント (Shop 入場時など) を別途用意するのが安全。

2. **音源を追加したら最初に Peak を計測する**: §8.8 と是正報告書参照。素材ばらつきに気づかず一律 volume を当てると無音化する。

3. **MCP からの Play モード検証は限定的**: `execute_code` の連続呼び出しは Editor のメインループを進めない。FixedUpdate を伴う動作確認はユーザーに手動 Play してもらう前提で組む。

4. **「ダサい」「鳴らない」のフィードバックは具体化しない**: 配置や素材の感覚は実機で見ないとわからない。一発で当てようとせず、配置・サイズ・音量は変更コストを低くしておいて FB ループで詰める設計が GameJam では速い。
