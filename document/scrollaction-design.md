# ScrollAction 設計書

24h GameJam プロジェクトにおける2Dアクション + ショップシステムの設計ドキュメント。
2026-05-04 時点の実装をベースに記述。

---

## 1. ゲームコンセプト

プレイヤーは基本アクション (左右移動・ジャンプ等) を **所持し** 、ショップで自由に **売買** できる。
"アクションそのものをモノとして扱う" のが設計の中核で、売却すれば対応する挙動が消え、購入すれば復活する。

### 既存シーン
- **ScrollAction.unity**: スクロール動作のメインプレイ場 (カメラ追従あり)
- **Shop.unity**: 固定カメラのショップ部屋。右端カメラ外でScrollActionへ遷移、左端は壁

### 売買可能アクション (5種)
| 名称 | 種別 | 売却時の挙動 |
|---|---|---|
| 左右移動 | bool | 入力しても歩かない |
| 接地判定 | bool | 地面と衝突しなくなりすり抜け、`IsGrounded` 常時 false |
| ジャンプ | int (スタッカブル) | 総ジャンプ可能回数が減る (=0で不可) |
| 地上回避 | bool | 接地中の Shift 無効 |
| 空中回避 | int (スタッカブル) | 空中での Shift 使用回数が減る |

---

## 2. アーキテクチャ概要

```
                ┌──────────────────────────────────────┐
                │  ActionInventory  (ScriptableObject) │ ← 現在値 (RAM)
                │   - owned: List<OwnedAction>          │
                │   - money: int                        │
                │   - OnInventoryChanged event          │
                └──────────────────────────────────────┘
                         ▲          ▲             ▲
              起動時に     │          │             │ 売買で
              リセット      │          │             │ 加減
                         │          │             │
   ┌─────────────────────┘          │             └─────────────────┐
   │                                │                                │
┌──────────────────────────┐  ┌──────────────────────┐  ┌────────────────────────┐
│ ActionInventoryDefaults  │  │  PlayerController    │  │       Shop             │
│  (ScriptableObject)      │  │  (MonoBehaviour)     │  │  (MonoBehaviour)       │
│   - owned: List<...>      │  │  - 入力集約          │  │   - catalog: List<...>  │
│   - initialMoney: int     │  │  - 接地判定          │  │   - 近接時にOnGUI       │
│  ROM (デザイン編集用)      │  │  - 各 PlayerAction.   │  │   - inventory.Buy/Sell │
└──────────────────────────┘  │     Tick を委譲      │  │   - OnPlayerExitedShop │
                              └──────────────────────┘  │     event 発火          │
                                                        └────────────────────────┘

         ┌──────────────────────────────────────────────────────────┐
         │                  PlayerAction (abstract SO)               │
         │  ─ DisplayName / MaxCount / buyPrice / sellPrice          │
         │  ─ OnFixedTick(ctx, count)                               │
         │  ─ OnLanded / OnRespawn / OnSessionInit                   │
         └──────────────────────────────────────────────────────────┘
                                ▲
        ┌─────────┬─────────────┼─────────────┬───────────────┐
   HorizontalMove  Jump   GroundCheck   GroundEvasion   AirEvasion
     Action       Action     Action       Action          Action
```

### 設計の3本柱 (CLAUDE.md A+B+C)
- **A. イベント駆動**: `ActionInventory.OnInventoryChanged` / `Shop.OnPlayerExitedShop` で疎結合通信
- **B. ScriptableObject**: 全アクション・所持データ・パラメータがアセット化
- **C. (FSMの代替)**: Player の状態は IsGrounded + 各アクション内 NonSerialized フラグで管理

---

## 3. フォルダ構成

```
Assets/
├── Scenes/
│   ├── ScrollAction.unity          # メインプレイ
│   └── Shop.unity                  # ショップ部屋
├── ScrollAction/
│   ├── Scripts/
│   │   ├── Actions/                # 売買可能アクション
│   │   │   ├── PlayerAction.cs           (抽象基底)
│   │   │   ├── PlayerActionContext.cs    (Tick共有データ)
│   │   │   ├── OwnedAction.cs            (action+count)
│   │   │   ├── HorizontalMoveAction.cs
│   │   │   ├── JumpAction.cs
│   │   │   ├── GroundCheckAction.cs
│   │   │   ├── GroundEvasionAction.cs
│   │   │   └── AirEvasionAction.cs
│   │   ├── Inventory/
│   │   │   ├── ActionInventory.cs        (現在値SO)
│   │   │   └── ActionInventoryDefaults.cs (初期値SO)
│   │   ├── Player/
│   │   │   └── PlayerController.cs       (オーケストレータ)
│   │   ├── Shop/
│   │   │   └── Shop.cs                   (近接時UI)
│   │   ├── Camera/
│   │   │   └── CameraFollow.cs           (ScrollAction専用)
│   │   ├── Scene/
│   │   │   ├── CameraExitTransition.cs   (画面外で次シーン)
│   │   │   └── BelowGroundRespawn.cs     (Shop専用Y閾値リスポーン)
│   │   ├── Stats/
│   │   │   └── PlayerStats.cs            (物理パラメータSO)
│   │   └── KillZone.cs                   (ScrollAction専用Trigger落下死)
│   └── Data/
│       ├── PlayerStats.asset
│       ├── ActionInventory.asset
│       ├── ActionInventoryDefaults.asset
│       └── Actions/
│           ├── HorizontalMove.asset
│           ├── Jump.asset
│           ├── GroundCheck.asset
│           ├── GroundEvasion.asset
│           └── AirEvasion.asset
└── MCPTest/                        # 別プロト (ブロック崩し)
    ├── Scripts/
    └── Data/
```

asmdef なし。全コードは Assembly-CSharp 内。

---

## 4. データモデル

### 4.1 PlayerAction (abstract ScriptableObject)
全アクションの基底。

| フィールド/メンバ | 型 | 役割 |
|---|---|---|
| `DisplayName` | string (abstract) | UI 表示名 |
| `MaxCount` | int (virtual=1) | 最大所持数。1=bool型、0=無制限スタック |
| `buyPrice` | int | 購入価格 |
| `sellPrice` | int | 売却価格 |
| `OnFixedTick(ctx, count)` | virtual | 毎物理フレーム呼出 |
| `OnLanded()` | virtual | 着地時 |
| `OnRespawn()` | virtual | リスポーン時 |
| `OnSessionInit()` | virtual | ゲーム起動時 |

### 4.2 OwnedAction (Serializable class)
インベントリ1スロット。
```csharp
public PlayerAction action;
public int count;
```

### 4.3 ActionInventory (ScriptableObject)
現在値 (RAM)。シーンを跨ぐ。

| フィールド/メソッド | 役割 |
|---|---|
| `defaults` | 初期値SOへの参照 |
| `owned` | `List<OwnedAction>` 現在所持 |
| `money` | 現在の所持金 |
| `OnInventoryChanged` | 売買時に発火する event |
| `GetCount(action)` / `HasAny<T>()` | クエリ |
| `CanBuy / CanSell` | 売買可否判定 |
| `Buy / Sell` | money加減 + owned更新 + Notify |
| `ResetToDefaults` | defaults から owned/money をコピー再構築 |
| `EnsureInitializedThisSession` | 起動時1回ResetToDefaults呼出 (static flag) |

### 4.4 ActionInventoryDefaults (ScriptableObject)
初期値 (ROM)。

| フィールド | 役割 |
|---|---|
| `owned` | 初期所持アクションリスト |
| `initialMoney` | 初期所持金 |

### 4.5 PlayerStats (ScriptableObject)
物理パラメータ。各アクションが参照する。

| フィールド | 用途 |
|---|---|
| `walkSpeed` | HorizontalMoveAction |
| `acceleration` | HorizontalMoveAction (MoveTowards) |
| `jumpForce` | JumpAction |
| `groundCheckRadius / groundLayer` | PlayerController の接地判定 |
| `evasionSpeed` | GroundEvasionAction / AirEvasionAction |

### 4.6 PlayerActionContext (普通のクラス)
1物理フレーム中に各アクションが共有するコンテキスト。
PlayerController がフィールド埋め → 各 action の `OnFixedTick` に渡す。

```csharp
Rigidbody2D rb;
Transform groundCheck;
Collider2D bodyCollider;
PlayerStats stats;
ActionInventory inventory;
float inputX;
float facingDir;
bool jumpRequested;
bool evasionRequested;
bool isGrounded;
bool justLanded;
```

PlayerController が `private readonly PlayerActionContext ctx = new();` でインスタンス使い回し (アロケなし)。

---

## 5. 主要フロー

### 5.1 ゲーム起動 → 初期化
```
PlayerController.Awake()
  └─ inventory.EnsureInitializedThisSession()
        └─ (static フラグ未セットなら) ResetToDefaults()
              ├─ owned ← defaults.owned (deep copy)
              ├─ money ← defaults.initialMoney
              ├─ 各action.OnSessionInit()  ← jumpsUsed/airUsed = 0
              └─ NotifyChanged()
                    └─ Shop UI / PlayerController.SyncCollisionMask 反映
```

### 5.2 物理フレーム (FixedUpdate)
```
PlayerController.FixedUpdate()
  ├─ grounded = EffectiveHasGroundCheck && OverlapCircle(...)
  ├─ justLanded = grounded && !IsGrounded
  ├─ if justLanded: 全 action.OnLanded()  ← jumpsUsed/airUsed リセット
  ├─ ctx を埋める
  └─ foreach owned slot: slot.action.OnFixedTick(ctx, slot.count)
        ├─ HorizontalMove: walkSpeed 加減速
        ├─ Jump: jumpRequested && jumpsUsed<count なら踏切
        ├─ GroundCheck: no-op (マーカー)
        ├─ GroundEvasion: evasionRequested && isGrounded なら水平加速
        └─ AirEvasion: evasionRequested && !isGrounded && airUsed<count
```

### 5.3 ショップ売買
```
Player が Shop Trigger に進入
  └─ Shop.OnTriggerEnter2D → playerInside=true → OnGUI 描画開始

Player がボタン押下
  └─ inventory.Buy(action) or Sell(action)
        ├─ money 加減
        ├─ owned 更新
        └─ NotifyChanged()
              └─ PlayerController.SyncCollisionMask (接地判定変化に追従)
              └─ (今後) UI 再描画

Player が Shop Trigger を退出
  └─ playerInside=false
  └─ Shop.OnPlayerExitedShop event 発火
        └─ PlayerController.HandleShopExit
              └─ tempGroundCheckGrace=false → SyncCollisionMask
```

### 5.4 落下死とリスポーン
```
[ScrollAction] KillZone Trigger に接触 / [Shop] BelowGroundRespawn が y<-7 検知
  └─ player.RespawnToStart()
        ├─ 速度ゼロ + startPosition へワープ
        ├─ 各 action.OnRespawn()  ← 空中リソースリセット
        ├─ tempGroundCheckGrace = true  ← 接地判定未所持でも一時的に床と衝突
        └─ SyncCollisionMask
```

### 5.5 シーン遷移 (Shop → ScrollAction)
```
CameraExitTransition.Update()
  └─ player の WorldToViewportPoint.x > 1.0 で
        SceneManager.LoadScene("ScrollAction")
```
ActionInventory はSOアセットなのでロード後も状態維持。

---

## 6. イベント一覧

| イベント | 発火元 | 購読先 | 目的 |
|---|---|---|---|
| `ActionInventory.OnInventoryChanged` | Buy / Sell / ResetToDefaults | PlayerController.SyncCollisionMask | 接地判定の所持変化を即時反映 |
| `Shop.OnPlayerExitedShop` (static) | Shop.OnTriggerExit2D | PlayerController.HandleShopExit | 接地猶予の解除 |

---

## 7. 入力一覧 (Keyboard)

| 操作 | キー | 取得 |
|---|---|---|
| 左右 | `A`/`D`/`←`/`→` | `isPressed` (押している間) |
| ジャンプ | `Space`/`W`/`↑` | `wasPressedThisFrame` |
| 回避 | `Shift` (左右どちらでも) | `wasPressedThisFrame` |
| ショップUIボタン | マウスクリック | OnGUI標準 |

新 InputSystem (`Keyboard.current`) を直接読む方式。InputAction アセットは未使用。

---

## 8. シーン構成

### 8.1 ScrollAction.unity
- Main Camera + `CameraFollow` (Player 追尾)
- Player + `PlayerController`
- Ground (横長)、複数の足場 (MarkersRoot 配下)
- KillZone (画面下、Trigger Collider)

### 8.2 Shop.unity
- Main Camera + `CameraExitTransition` (Player→ScrollAction)
- Player + `PlayerController`
- Ground (短め)
- LeftWall (Trigger外、左端の物理壁)
- Shop GameObject (黒い四角 SpriteRenderer + Trigger Collider + `Shop`)
- BelowGroundRespawn GameObject (Y<-7 でPlayer.RespawnToStart 呼出)

---

## 9. 値の管理場所早見表

| 項目 | 種別 | 場所 |
|---|---|---|
| 物理量 (walkSpeed等) | パラメータ | `Data/PlayerStats.asset` |
| 各アクションの買値・売値 | パラメータ | `Data/Actions/*.asset` (`buyPrice`/`sellPrice`) |
| 初期所持アクション | ROM | `Data/ActionInventoryDefaults.asset` (`owned`) |
| 初期所持金 | ROM | `Data/ActionInventoryDefaults.asset` (`initialMoney`) |
| 現在の所持アクション | RAM | `Data/ActionInventory.asset` (`owned`) ※起動時上書き |
| 現在の所持金 | RAM | `Data/ActionInventory.asset` (`money`) ※起動時上書き |
| 各アクション固有の挙動 | コード | `Scripts/Actions/*.cs` |
| 入力読込・物理オーケストレート | コード | `Scripts/Player/PlayerController.cs` |
| 売買UI | コード | `Scripts/Shop/Shop.cs` (OnGUI、後で uGUI 化) |

---

## 10. 拡張手順

### 新アクション追加
1. `Scripts/Actions/` に `XxxAction.cs` を作成、`PlayerAction` 継承
2. 必要なら `OnFixedTick` / `OnLanded` / `OnRespawn` / `OnSessionInit` をオーバーライド
3. `[CreateAssetMenu]` 属性 → エディタで `.asset` を `Data/Actions/` に作成
4. インスペクタで `buyPrice` / `sellPrice` / `MaxCount`相当を設定
5. (初期所持にしたければ) `ActionInventoryDefaults.asset` の `owned` リストに追加
6. `Shop` GameObject の `catalog` リストに追加
7. PlayerController/Shop は触らずに完了

### 価格・初期所持金の調整
- `Data/Actions/*.asset` のインスペクタで `buyPrice`/`sellPrice` 編集
- `Data/ActionInventoryDefaults.asset` の `initialMoney`/`owned` 編集

### 難易度別構成
- `ActionInventoryDefaults` を Easy/Normal/Hard で作り分け、`ActionInventory.defaults` の参照を差し替える

---

## 11. 既知の課題と妥協

| 課題 | 影響 | 優先度 |
|---|---|---|
| `GroundCheckAction` が PlayerController で type-leak している | 同種のセットアップ系アクション追加時にコード分岐が増える | 高 |
| `PlayerStats` に全アクションの物理量が同居している | アクション追加で SO 肥大化 | 高 |
| Shop の UI が `OnGUI` (CLAUDE.md は本番UIに不可と明記) | 見た目の品質、規約整合性 | 中 |
| KillZone (ScrollAction) と BelowGroundRespawn (Shop) の二重実装 | 統一できる | 中 |
| アクション実行順への暗黙依存 | 同一入力で複数actionが反応する場合に挙動不安定の可能性 | 中 |
| `Shop.OnPlayerExitedShop` が static event | ショップ複数化で区別不能 | 低 |
| `sessionInitialized` static flag が "Disable Reload Domain" と非互換 | デフォルト設定なら問題なし | 低 |

---

## 12. 規約整合状況 (CLAUDE.md 抜粋)

| 規約 | 遵守状況 |
|---|---|
| 静的型付け / アクセス修飾子明示 | ✓ |
| インスペクタ公開は `[SerializeField] private` + プロパティ | ✓ (`PlayerStats`はpublicフィールドだが `[Header]` でグループ化) |
| 数値はSO側、コードに直書きしない | ✓ |
| イベント駆動 (`event Action`) | ✓ |
| `SendMessage` / `Find` 系 / 旧Input禁止 | ✓ (使用なし) |
| ScriptableObject でデータ分離 | ✓ |
| FSM (`enum + state`) | △ (`IsGrounded` / 各action 内フラグで擬似的に管理。明示的enumは未使用) |
| Composition (継承の浅さ) | ✓ (`PlayerAction` の継承1段のみ) |
| 日本語コメント (WHY) | ✓ |
| `<summary>` 完備 (publicメソッド+独自ロジックメソッド) | ✓ |
| `OnGUI` は本番UI不可 | ✗ (Shop で暫定使用、コメントで明記) |

---

## 13. 今後の拡張余地 (アイデアメモ)

- アクション追加: 重力反転 / 慣性売却 / 死亡判定 / 透視 等 → `colla_workspace/action_and_stage_ideas.md` 参照
- ショップ価格変動 (回数で値上げ)
- 通貨入手手段: コイン拾い / 敵撃破
- Shop UI の uGUI 化 (TextMeshPro + Button + Slider)
- アクション実行順の明示的優先度化
- セーブロード (PlayerPrefs もしくは JSON)
