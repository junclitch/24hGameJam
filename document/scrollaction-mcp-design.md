# ScrollAction_MCP — ステージ設計・ギミック仕様

このドキュメントは MCP セッションで構築した `ScrollAction_MCP` シーンの最終状態を記録する。

参考: 既存ステージ `ScrollAction.unity` を複製して `ScrollAction_MCP.unity` を作成、その上に各ギミックを配置した (元ステージは初代マリオ 8-1 を参考にした横スクロール)。

---

## 1. プレイ全体フロー

```
Title ──→ Shop (装備調整) ──→ ScrollAction_MCP (本編) ──→ GameClear
                ↑                       │
                └─── GameOver ──────────┘
                       (時間切れ / 残機0)
```

- `Title.unity`: 「Shop へ」ボタンで Shop シーンへ
- `Shop.unity`:
  - `ShopSessionResetter.Awake` で `ActionInventory` を Defaults にリセット (残機 3 + 0 G で再構成)
  - 右に歩いて画面外へ → `CameraExitTransition.nextSceneName = ScrollAction_MCP` で本編に遷移
- `ScrollAction_MCP.unity`: 本編。`GameCycleManager` が制限時間とシーン遷移を管理
- `GameOver.unity` / `GameClear.unity`: 既存。クリア演出 / リトライ動線

---

## 2. ステージ全体構成 (x 軸)

開始 x=-24.5、ゴール x=113.09 (約 137u 横スクロール)

| 区画 | x 範囲 | 構成 | 設計意図 |
|---|---|---|---|
| 練習区画 (Section1) | -26 〜 -8 | 連続床、x=-12 に 3 段ピラー | 歩き + ジャンプ慣らし |
| 飛石 (Stones) | -7 〜 -1 | Stone_A / Stone_B、1.5u ギャップ x2 | 軽いジャンプ |
| Section2 | 0.6 〜 14 | 床 6 枚、ピラー 2 本 (x=6, x=10) | ジャンプ繰返 |
| ブリッジ | 16 〜 20 | Bridge_A / Bridge_B | ジャンプ |
| Section3 第1平坦 | 22 〜 28 | 床 3 枚、2 段ピラー (x=26) | 助走 |
| **崩落橋** (CrumbleBridge_Section3) | 28 〜 35 | 4 タイル、踏むと 0.5s で崩壊 | 走り抜け / 地上ダッシュ |
| Section3 第2平坦 | 36 〜 49 | 床 7 枚 + Floor_10 が 0.6u 凹み (くぼみ) | 落下鉄球エリア |
| **落下鉄球** (FallingBallsRoot) | 39 / 42 / 46 | 鉄球 3 体、画面外から落下 | くぼみでしゃがみ待機 |
| **8連小石ブリッジ** (RollingBridge_Gap2) | 50.82 〜 64.82 | 0.5u 幅 × 8 石、ギャップ 1.5u | 転がりで bridge |
| Section3 第3平坦 | 67.6 〜 79.5 | 床 7 枚 | 走り抜け |
| **台車ノコ** (Saw_Section3) | 64〜75 巡回 | fore-prop_323 を加工なしで使用 | ジャンプ越え |
| 最終登攀 | 82 〜 113 | 飛石階段 (Floor_13(2..11)) + 高所ピラー | ジャンプ繰返 |
| ゴール (GoalMarker) | (113.09, 10.36) | 完済ドアスプライト + GOAL TextMesh | クリア判定 |

---

## 3. ハザード仕様

### 3.1 崩落橋 (Crumble Bridge)

| 項目 | 値 |
|---|---|
| 配置 | x=28, 30, 32, 34 (4 タイル) |
| 元ピット | Section3_Floor が x=27〜35 で欠けている。崩落タイルが埋める |
| 崩壊 delay | 0.5 秒 |
| 警告色 | 橙茶 (0.85, 0.55, 0.35) |
| 復活条件 | プレイヤーリスポーン (`PlayerController.OnPlayerRespawned`) |

**実装**: `Hazards/CrumbleTile.cs`
- `OnCollisionEnter2D` で踏まれた瞬間に `CrumbleAfterDelay` コルーチン
- delay 経過で `Collider.enabled=false`, `SpriteRenderer.enabled=false`
- `PlayerController.OnPlayerRespawned` 静的イベント購読でリセット

**解法**: 歩きで連続移動 (1.14s で踏破、各タイル 0.5s 後崩壊する間にすでに次タイル) / 地上ダッシュ (0.44s で踏破)。停止すると死。

### 3.2 鉄球+くぼみ (Falling Iron Balls)

| 球 | x | phase offset | 周期 |
|---|---|---|---|
| FallingBall_0 | 39 | 0.0s | 2.7s |
| FallingBall_1 | 42 (くぼみ真上) | 0.9s | 2.7s |
| FallingBall_2 | 46 | 1.8s | 2.7s |

| 球 cycle | 値 |
|---|---|
| topY (待機) | 10 (画面外) |
| bottomY (着地) | -0.5 |
| fallTime | 0.7s |
| waitAtTop | 1.5s |
| waitAtBottom | 0.5s |
| sprite | fore-prop_322 (鎖+鉄球)、scale 2x、center pivot |
| Collider | CircleCollider2D, offset (0,-0.26), radius 0.25 |

**くぼみ**: Floor_10 (x=42) を y=-3.24 まで下げて床上端 y=-2.5 に。深さ 1.0u。

**ジオメトリ整合 (impact 時)**:
- 球 collider 中心 y=-1.02、半径 0.5 (world) → 球範囲 [-1.52, -0.52]
- 規定床上立ち頭頂 -0.3 → 重複、死
- 規定床上しゃがみ頭頂 -0.9 → 重複、死
- くぼみ内立ち頭頂 -1.3 → 重複、死
- **くぼみ内しゃがみ頭頂 -1.9** → margin 0.38u、安全 ✓

**実装**: `Hazards/FallingBallHazard.cs`
- `Awake` で `startTime = Time.time` をキャプチャ (リスタート後の phase ランダム化を防ぐ)
- t² で重力的な加速感
- 落下→着地切替で `static event OnImpact(Vector3 pos)` 発火 (SE 用)

### 3.3 8連小石ブリッジ (Rolling Bridge)

| 項目 | 値 |
|---|---|
| 石数 | 8 |
| 石幅 | 0.5u (scale.x = 0.286) |
| 石高さ | 1.49u (scale.y = 1.143、元 Floor 同等) |
| ギャップ | 1.5u 各 |
| 周期 D | 2.0u |
| 上端 y | -1.5 (周辺床と揃え) |

**KillZone トリガー** (`HazardsRoot/RollingFallTrap`):
- 中心 (57.82, -1.8)、サイズ (17.64, 0.4)
- y 範囲 [-2.0, -1.6]
- プレイヤー BC 底面が落ちて -1.6 を下回ると即死
- ロール bridge mode (vy=0) では BC 底面が -1.5 のままなのでセーフ

**Rolling.asset** の `maxGapWidth = 1.5` で 1.5u ギャップを bridge 可。

**解法**:
- 歩き: 0.7u の no-overlap 区間で 0.147u 落下 → トリガー入って即死
- ジャンプ: 各 1.5u を 9 連続でジャンプ可だが小石 0.5u に着地は精密ゲー
- 転がり (Q 長押し): 連続 bridge で素通り、9u/s で約 1.6 秒踏破

### 3.4 台車ノコ (Saw_Section3)

| 項目 | 値 |
|---|---|
| sprite | fore-prop_323 (加工なし、台車込み 1.82×1.0u) |
| 巡回 x | 64 〜 75 (11u) |
| 巡回速度 | 4 u/s |
| 床 | y=-1.5 上に台車底面 |

**実装**: `Hazards/HorizontalPatroller.cs` + `KillZone`。視覚回転なし、スライドのみ。

**解法**: 走り込んでジャンプで飛び越え (装置上端 y=-0.5、ジャンプ最大頭頂 y=3.03)。

---

## 4. スキルシステム

### 4.1 初期所持 (`ActionInventoryDefaults.asset`)

| アクション | count | 売却額 | 役割 |
|---|---|---|---|
| HorizontalMove (歩き) | 1 | 200 G | 地上左右移動 |
| GroundCheck | 1 | (売却不可) | 地面衝突。売ると地面すり抜け、ショップで再購入用 grace あり |
| Jump | 1 | 300 G | 縦方向 (スタック可) |
| Crouch (しゃがみ) | 1 | 100 G | 低姿勢、停止する |
| AirControl (空中制御) | 1 | 100 G | 空中横移動 |
| Life (残機) | 3 | 150 G/個 | リスポーン回数 |

**初期所持金**: 0 G

### 4.2 経済バランス (案 A — 初期売却で購入資金捻出)

- 初期手持ち 0 G → 何かを売らないと買えない設計
- 初期アクション全売却で **最大 1150 G**
- 全購入アクション総額 **1900 G** → 全買い不可、選択を強制

### 4.3 購入可能アクション

| アクション | 価格 | キー | 効果 |
|---|---|---|---|
| GroundEvasion (地上ダッシュ) | 80 G | Shift (地上) | 水平瞬間加速、無制限 |
| AirEvasion (空中ダッシュ) | 100 G | Shift (空中) | スタック消費 |
| SafetyShoes (安全靴) | 100 G | (自動) | 移動キー離して即停止 |
| Rolling (転がる) | 120 G | Q 長押し (地上) | 低姿勢移動、`maxGapWidth=1.5` で小穴 bridge |
| Sliding (スライディング) | 120 G | Z (地上) | 慣性ブースト + 線形減速 |
| Glider (グライダー) | 180 G | F 長押し (空中) | 横ブースト + 落下抑制 |
| WallKick (壁キック) | 200 G | Space (空中で壁) | 壁を蹴って反対方向ジャンプ |
| Warp (ワープ) | **500 G** | C | 進行方向に瞬間移動 (壁抜け) |
| Jetpack (ジェットパック) | **500 G** | Space 長押し | 燃料制で上昇 |

### 4.4 アクション間の前提順 (Tick 順依存)

1. HorizontalMove → CrouchAction → SlidingAction → RollingAction → ...
2. WallKickAction は JumpAction より前 (jumpRequested 消費で同フレ二重発火防止)
3. AirControlAction は HorizontalMove と排他 (isGrounded で分岐)

---

## 5. システム機能

### 5.1 制限時間 + HUD (`Scene/GameCycleManager.cs`)

| 項目 | 値 |
|---|---|
| `timeLimit` | 60 sec |
| 残り 10 秒以下 | タイマー赤色化 |
| 残機 ≤ 1 | 残機表示オレンジ |

OnGUI 描画: 画面中央上部にタイマー + その下に残機。右下にリスタートボタン。

### 5.2 残機システム (`LifeAction`)

- `PlayerController.RespawnToStart()` 内で `inventory.ConsumeOne<LifeAction>()` を呼ぶ
- 消費成功 → 通常リスポーン (`lastSafePosition` へ)
- 消費失敗 (残機 0) → `gameOverSceneName` (= "GameOver") へ遷移
- `gameOverSceneName` 未設定なら従来通り無限リトライ (旧シーン後方互換)

### 5.3 リスタートボタン (Shop へ戻る)

`GameCycleManager.Restart()` → `SceneManager.LoadScene(shopSceneName)` (= "Shop")

`ShopSessionResetter` が Shop シーン Awake で `ResetToDefaults()` を呼ぶので、装備・残機・所持金が初期化される (= 「諦めボタン」)。

### 5.4 チェックポイント (`Hazards/CheckpointTrigger.cs`)

各ハザード後の安全地帯に 4 個配置 (x=36, 49, 64, 78)。

- Trigger に PlayerController が触れた瞬間、`PlayerController.SetRespawnPoint(spawnOffset 込みの位置)` を呼ぶ
- 一度通過したら無効化 (戻り通過で前のポイントが消されない)
- 透明: SpriteRenderer 無し、Scene View 用 OnDrawGizmos のみ
- `spawnOffset` で旗本体と復活位置を分離可能

### 5.5 KillZone (`KillZone.cs`)

- `[RequireComponent(typeof(Collider2D))]` + `Reset()` で自動 Trigger 化
- `OnTriggerEnter2D` で `PlayerController.RespawnToStart()` 呼び
- 用途: y=-10 の落下死 / ノコ / 鉄球 / 8連石下のフォールトラップ

---

## 6. UI

### 6.1 操作説明オーバーレイ (`UI/ActionHelpOverlay.cs`)

画面下中央にテーブル: `所持中アクション名: HelpText`

HelpText は全アクション統一フォーマット:
- `[キー] 条件` (能動アクション)
- `(自動) 動作` (パッシブ: 残機 / 安全靴)

### 6.2 ショップ UI (`Shop/Shop.cs`)

横 2 分割タブ無し:
- 左 = 「購入」: catalog のうち未所持のみ + 購入ボタン
- 右 = 「売却」: 所持中のみ + 売却ボタン
- 同じアイテムが両列に出ない (重複なし)
- スタック品 (Life / Jump など MaxCount != 1) は売却列に "x N" で個数表示

### 6.3 タイマー / 残機 / リスタートボタン

`GameCycleManager.OnGUI` で描画 (本実装は uGUI 化推奨)。

---

## 7. 音声 (SE)

### 7.1 SE 一覧

| SE | 用途 | 適用方法 | volume |
|---|---|---|---|
| Jump.wav | ジャンプ / 壁キック | `JumpAction.OnJumped`, `WallKickAction.OnWallKicked` | 0.3 |
| Jet.wav | ジェットパック噴射 | `PlayerController.IsJetpacking` ループ | 0.3 |
| Glider.wav | グライダー | `PlayerController.IsGliding` ループ | 0.3 |
| rolling.wav | 転がり | `PlayerController.IsRolling` ループ | 0.3 |
| Sliding.wav | スライディング | `IsSliding` 立上がりエッジ 1 発 | 0.3 |
| worp.wav | ワープ | `IsWarping` 立上がりエッジ 1 発 | 0.3 |
| dash.wav | ダッシュ (地上/空中) | `GroundEvasionAction.OnDashed` / `AirEvasionAction.OnDashed` | 0.3 |
| dooon.wav | 鉄球着地 | `FallingBallHazard.OnImpact(Vector3)` 距離減衰 | 0.3 (max) |
| Coinv001.wav | 購入 / 売却 | `ActionInventory.OnPurchased / OnSold` | 0.3 |

### 7.2 配置

- **Player.prefab 配下** (Jump / Jetpack / Glider / Rolling / Sliding / Warp / Dash の 7 SE)
  - Player を置く全シーン (Shop, ScrollAction_MCP, ScrollAction) で自動継承
- **HazardsRoot/FallingBallSE** (ScrollAction_MCP のみ)
- **Shop.prefab/ShopCoinSE** (Shop シーンに渡る)

### 7.3 dooon の距離減衰 (`UI/FallingBallSE.cs`)

| パラメータ | 値 |
|---|---|
| `fullVolumeDistance` | 4u |
| `silentDistance` | 12u |
| 中間 | 線形減衰 |

`OnImpact(Vector3 pos)` で球の世界座標を受け、listener (Player) との距離で `PlayOneShot(clip, scaledVolume)`。

---

## 8. 物理パラメータ (`PlayerStats.asset`)

| 項目 | 値 |
|---|---|
| walkSpeed | 7 |
| acceleration | 80 |
| jumpForce | 14 |
| Rigidbody2D.gravityScale | 3 |
| 実効重力 | 29.43 u/s² |
| ジャンプ最大高さ | 約 3.33 u |
| ジャンプ最大横距離 | 約 6.66 u (走り込み時) |
| evasionSpeed (ダッシュ) | 18 u/s |
| groundCheckRadius | 0.18 |
| safeGroundCheckRange | 0.6 (動的安全地帯トラッキング用、現在未使用 — チェックポイントに置換) |

**ステージ穴サイズの設計**: 全ギャップ ≤ 6.66u。最大 5.27u はジャンプ射程の 80% で「ぎりぎり感」。

---

## 9. プロジェクト構成変化

### 9.1 新規スクリプト

```
Assets/ScrollAction/Scripts/
├ Actions/
│  ├ AirControlAction.cs        (空中横移動分離)
│  ├ LifeAction.cs              (残機)
│  ├ SafetyShoesAction.cs       (即停止)
│  └ (RollingAction.cs に maxGapWidth 追加)
├ Hazards/
│  ├ HorizontalPatroller.cs     (台車ノコ巡回)
│  ├ CrumbleTile.cs             (崩落橋)
│  ├ FallingBallHazard.cs       (落下鉄球)
│  └ CheckpointTrigger.cs       (リスポーン地点設定)
├ UI/
│  ├ GliderSE.cs / RollingSE.cs / SlidingSE.cs
│  ├ WarpSE.cs / DashSE.cs / FallingBallSE.cs
│  └ ShopCoinSE.cs
```

### 9.2 新規アセット

```
Assets/ScrollAction/Data/Actions/
├ Life.asset
├ AirControl.asset
└ SafetyShoes.asset
```

### 9.3 既存ファイル変更

- `PlayerController.cs`: `OnPlayerRespawned` 静的イベント追加、`SetRespawnPoint`、`gameOverSceneName`、残機消費分岐、`lastSafePosition`
- `ActionInventory.cs`: `ConsumeOne<T>()`、`OnPurchased / OnSold` 静的イベント
- `Shop.cs`: タブ無し横 2 列再構成、所持中は購入列に出さない
- `Inventory/ActionInventoryDefaults.asset`: Life × 3、AirControl × 1 を追加、initialMoney = 0
- `Stats/PlayerStats.asset`: `safeGroundCheckRange` 追加
- 各 `*.asset` の buyPrice / sellPrice を経済バランス案 A に合わせて再設定
- `Shop.prefab`: catalog に SafetyShoes / AirControl / Life / Crouch を追加
- `Shop.unity`: `nextSceneName: ScrollAction_MCP` に変更
- `KillZone.cs`: `Reset()` で自動 Trigger 化
- `Scenes/ScrollAction_MCP.unity`: シーン作成 (ScrollAction を複製)

---

## 10. 既知の挙動・調整可能ポイント

- 鉄球の `fullVolumeDistance` / `silentDistance` で SE の聞こえ範囲調整
- 鉄球周期 / 位相は Inspector で個別変更可
- チェックポイント位置は `CheckpointsRoot/Checkpoint_*` の transform で調整可、`spawnOffset` で復活地点との分離可
- `Rolling.asset` の `maxGapWidth` を 1.5 → 1.7 等に上げると 8連石ブリッジを楽にできる
- 制限時間は `GameCycleManager.timeLimit`、現在 60 秒
- 残機初期値は `ActionInventoryDefaults.asset` の Life count (現在 3)
