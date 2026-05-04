# Unity Coding Conventions (2D Game / Unity 6 / C#)

このプロジェクトは Unity Engine 6000.3 系を使用した2Dゲーム開発プロジェクトです。AIアシスタント（Claude / Copilot 等）にコードを生成させる際は、本ドキュメントの規約に従うこと。

---

## 思考プロセス

**コードを生成する前に、必ず以下を行うこと：**
1. どの GameObject / コンポーネント構成が最適かを1行で説明する
2. 実装するロジックの概要を1行で説明する
3. その後に実装コードを提示する

---

## 言語と構文 (C# 12 / Unity 6)

### 必須事項
- **静的型付け**を徹底する（`var` は型が右辺から自明な場合のみ可）
  ```csharp
  float speed = 100f;
  Rigidbody2D rb;            // 型を明示
  var enemies = new List<Enemy>();  // 右辺から自明なので var OK
  ```
- **アクセス修飾子を明示**する（`private` を省略しない）
- **インスペクタ公開はフィールド + `[SerializeField] private`** を基本とする
  ```csharp
  [SerializeField] private float speed;
  public float Speed => speed;   // 外公開はプロパティで
  ```
- **`using` を頂上で整理**し、未使用 using は残さない
- **`async/await`** が必要な場面では UniTask か C# 標準 `Task` を使う

### スクリプト内に「パラメータの値」を書かない
**インスペクタで設定するフィールドにはコード上の初期値（`= 8f` など）を付けない。** 値はインスペクタ／ScriptableObject アセット側で設定すること。

```csharp
// ❌ 禁止: スクリプト側に数値を直書き
[SerializeField] private float speed = 8f;
public int score = 100;
public Color color = Color.white;

// ✅ 推奨: 構造だけ宣言。値は .asset / シーン上で設定
[SerializeField] private float speed;
public int score;
public Color color;
```

**理由:**
- スクリプトは「値の構造」を定義する場所、`.asset` / シーンは「値の中身」を持つ場所、と責務を分離する
- インスペクタ側で値を設定したのに、コードのデフォルトを見ると別の値が書いてあって混乱する事故を防ぐ
- 値の変更でコード差分が発生せず、Unity アセットの差分だけで済む

**例外:**
- `const` / `static readonly` の本物の定数（例: `Mathf.PI` 相当）はコードで定義してよい
- 純粋に内部状態として保持する `private` フィールドの初期化（例: `private int hitCount = 0;`）はOK
- ただし `0` / `false` / `null` 相当の初期化は冗長なので書かない（C#のデフォルト値で十分）

### モダン C# の活用
- ラムダ・LINQ
- 式形式メンバー (`=>`)
- パターンマッチング (`is`, `switch` 式)
- null 合体演算子 / null 条件演算子 (`??`, `?.`)

---

## API スタイル

### イベント駆動 (疎結合)
```csharp
// ✅ 正しい: C# event / Action / UnityEvent でイベント発火
public static event Action<int> OnAnyDestroyed;
OnAnyDestroyed?.Invoke(score);

// ❌ 禁止: SendMessage / BroadcastMessage（型安全性なし、遅い）
gameObject.SendMessage("OnHit", damage);
```

- **発火側は `static event Action<...>`** が基本（誰が購読するか発火側は知らない）
- **購読側は `OnEnable` で `+=`、`OnDisable` で `-=`** をペアで書く
- インスタンスがシーンに1つだけのマネージャ系は `static` プロパティで状態公開

### コンポーネント参照
```csharp
// ✅ 正しい: 起動時にキャッシュ
[SerializeField] private Rigidbody2D rb;
void Awake() { if (rb == null) rb = GetComponent<Rigidbody2D>(); }

// ❌ 禁止: Update 内で毎フレーム取得
void Update() { GetComponent<Rigidbody2D>().linearVelocity = ...; }
```

### プロパティ vs フィールド
```csharp
// ✅ 正しい: 外公開はプロパティ、内部状態はフィールド
public int Score { get; private set; }

// ❌ 禁止: public フィールドの直さらし
public int score;
```

---

## アーキテクチャ設計

このプロジェクトは **A + B + C** の3点セットを基本パターンとする。

### A. イベント駆動 (Signal の Unity 版)
- ノード/オブジェクト間の直接参照を避け、`event` で通信する
- 「Brick が GameManager を知る」のような下流→上流の参照は禁止
- 上流が下流の event を購読する形にする

### B. ScriptableObject でデータ分離
- 数値・パラメータ・データ定義はコードから分離して `.asset` 化する
- 同じパラメータを複数オブジェクトで共有可能
- インスペクタで触れて、再ビルド不要で調整できる
```csharp
[CreateAssetMenu(fileName = "PlayerStats", menuName = "Game/Player Stats")]
public class PlayerStats : ScriptableObject
{
    public float walkSpeed = 5f;
    public float jumpForce = 12f;
}
```

### C. FSM による状態管理
- ゲーム進行・キャラクタの挙動などは `enum` ベースの FSM で表現する
- 各コントローラは「状態がXのときだけ動く」とガードする
```csharp
void Update()
{
    if (GameManager.State != GameState.Playing) return;
    // ...
}
```

### Composition (継承より構成)
- 機能は小さなコンポーネントに分割（HealthComponent, MovementComponent など）
- MonoBehaviour の継承チェーンは深くしない
- 再利用可能なコンポーネントを意識する

---

## ドキュメントスタイル

### 日本語コメントを積極的に書く
- WHAT より **WHY** を書く
- 自明なコメント（`// score を加算` など）は不要
- 順序依存・微妙な不変条件は明記する

### クラスのドキュメント
```csharp
/// <summary>
/// プレイヤーの体力を管理するコンポーネント。
/// ダメージ処理と回復処理を担当する。
/// </summary>
public class HealthComponent : MonoBehaviour { ... }
```

### メソッドのドキュメント
```csharp
/// <summary>ダメージを計算して適用する。</summary>
/// <param name="baseDamage">基礎ダメージ値</param>
/// <param name="critical">クリティカルヒットかどうか</param>
/// <returns>最終的なダメージ値</returns>
public int ApplyDamage(int baseDamage, bool critical = false) { ... }
```

### メソッドに `<summary>` を付ける判断基準
**判定軸は「メソッド名とシグネチャだけ見て、中身の挙動が予測できるか？」**

- ❌ 予測できない（独自ロジック・分岐・副作用がある）→ `<summary>` **必須**
- ✅ 予測できる（慣用名・自明な責務）→ `<summary>` 不要

Unity ライフサイクル系（`Awake`, `Start`, `Update`, `OnEnable`, `OnDisable` 等）も**自動で「不要」と決めつけない**。中身に独自ロジックがあれば必須。

| メソッド例 | summary 要否 | 理由 |
|---|---|---|
| `Awake() { rb = GetComponent<Rigidbody2D>(); }` | 不要 | 名前と中身が完全に予測通り |
| `OnCollisionEnter2D` で相手別に反射処理を分岐 | **必須** | 名前は「衝突時」しか伝えず、分岐ロジックが予測不能 |
| `OnEnable() { Brick.OnAnyDestroyed += Handle...; }` | 不要 | 購読/解除はライフサイクル慣用 |
| `Update()` でstateゲートして物理補正 | **必須** | 中身に独自処理がある |
| `public string GetName()` | 不要 | 名前から自明 |
| `public int CalculateDamage(...)` で複雑な計算 | **必須** | 計算式の意図を残す |

### コーディング後の最終チェック
クラスを書き終えたら、以下のパスを必ず実施する:
1. **public メソッド全件**を見て、`<summary>` が付いているか確認
2. **独自ロジックを持つ private メソッド**にも同じチェック
3. ライフサイクル系で「自明じゃない処理」が入っていないか再確認
4. 取りこぼしを発見したら追加してから完了とする

「メソッドの種類（ライフサイクル/通常）で雑に分類してスキップする」のは禁止。判断軸はあくまで**「名前+シグネチャから挙動が予測できるか」**ひとつ。

---

## 禁止事項

### 絶対に使用しないこと
| 禁止 | 代替 |
|------|------|
| `SendMessage` / `BroadcastMessage` | C# `event` / `Action` / `UnityEvent` |
| `GameObject.Find("Name")` | `[SerializeField]` で参照を渡す or `FindFirstObjectByType<T>()` |
| `FindObjectOfType<T>` (廃止) | `FindFirstObjectByType<T>()` |
| `Resources.Load` | 直接参照 / Addressables |
| `UnityEngine.Input` (旧 Input Manager) | `UnityEngine.InputSystem` (Keyboard.current 等) |
| `public` フィールドのインスペクタ公開 | `[SerializeField] private` + プロパティ |
| `OnGUI` を本番UIに使う | uGUI / UI Toolkit (OnGUIはデバッグのみ) |
| Update 内で `GetComponent` を呼ぶ | Awake でキャッシュ |
| Update 内で `new WaitForSeconds(...)` | コルーチン / フィールドにキャッシュ |
| マジックナンバー直書き | `const` / ScriptableObject |

### 例: 旧 Input Manager 禁止
```csharp
// ❌ 禁止: 旧 Input Manager
float h = Input.GetAxisRaw("Horizontal");

// ✅ 正しい: 新 Input System
var kb = Keyboard.current;
float h = 0f;
if (kb.leftArrowKey.isPressed)  h -= 1f;
if (kb.rightArrowKey.isPressed) h += 1f;
```

---

## コード品質

### DRY 原則 (Don't Repeat Yourself)
- 重複コードは関数化／コンポーネント化
- 定数は `const` または `static readonly`、データは `ScriptableObject`
- ただし **YAGNI とのバランス**を取ること。1〜2回の重複なら抽出せず、3回目で関数化する判断もOK

```csharp
public static class PhysicsConstants
{
    public const float Gravity = 9.81f;
    public const float MaxFallSpeed = 20f;
}
```

### YAGNI 原則 (You Aren't Gonna Need It)
**「将来使うかもしれない」で実装を増やさない。必要になってから書く。**

- 実装スコープは依頼されたタスクの範囲にとどめる（バグ修正のついでにリファクタしない、等）
- 1種類しかないのに interface・抽象クラスを切らない
- 使われない引数・オプション・フラグを足さない
- 1回しか使わない処理を関数化しない
- 「設定可能にしておく」を理由にした無駄な `[SerializeField]` を増やさない
- 半端な実装・未使用コードは残さない（書き始めたら使い切る、不要なら消す）

```csharp
// ❌ 避ける: 1種類しか敵がいないのに先回りして抽象化
public interface IEnemy { void TakeDamage(int amount); }
public abstract class EnemyBase : MonoBehaviour, IEnemy { ... }
public class GoblinEnemy : EnemyBase { ... }

// ✅ 推奨: まずは具象1クラスで動かす。種類が増えた時に抽象化
public class Enemy : MonoBehaviour { public void TakeDamage(int amount) { ... } }
```

```csharp
// ❌ 避ける: 今は使わないオプション・将来用の引数
public void Attack(int damage, bool useCritical = false, AttackType type = AttackType.Normal,
                   float knockback = 0f, bool ignoreArmor = false) { ... }

// ✅ 推奨: いま必要な引数だけ
public void Attack(int damage) { ... }
```

```csharp
// ❌ 避ける: 「念のため」の null チェック・例外ハンドリング
public void Hit(Enemy enemy)
{
    if (enemy == null) return;        // 呼び出し側が保証している
    try { enemy.TakeDamage(10); }     // 必要な例外しか出ない
    catch { /* nothing */ }
}

// ✅ 推奨: 信頼できる前提条件は信頼する
public void Hit(Enemy enemy) { enemy.TakeDamage(10); }
```

> **DRY と YAGNI は対立することがある**。共通化を急ぎすぎると、異なる事情で似た形になっただけのコードを早すぎる抽象化で縛ってしまう。
> 経験則: **3回目に重複したら抽出**。それまでは具体のまま並べておく方が安全。

### パフォーマンス配慮
```csharp
// ❌ 避ける: Update 内での過剰な検索
void Update()
{
    var enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);  // 毎フレーム検索
    foreach (var e in enemies) { /* ... */ }
}

// ✅ 推奨: イベント駆動でキャッシュを更新
private readonly List<Enemy> _enemies = new();

void OnEnable()  { Enemy.OnAnySpawned += _enemies.Add; Enemy.OnAnyDefeated += e => _enemies.Remove(e); }
void OnDisable() { Enemy.OnAnySpawned -= _enemies.Add; }
```

### その他のパフォーマンス指針
- `Update` / `FixedUpdate` は本当に必要なときだけ実装
- 不要な MonoBehaviour は `enabled = false` で停止
- 文字列比較より `CompareTag` / レイヤーマスクを使う
- 高速移動する Rigidbody2D は `CollisionDetectionMode2D.Continuous`

---

## UI 設計

### uGUI (Canvas) を基本とする
- 本番UIは `Canvas` + `Text(TMP)` / `Button` / `Slider` 等で構築
- `OnGUI` (IMGUI) は **プロトタイプとデバッグ用途のみ**

### OnGUI を使う場合
```csharp
// デバッグ専用なのでスタイルは inline で十分
void OnGUI()
{
    var style = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold };
    style.normal.textColor = Color.white;
    GUI.Label(new Rect(20, 16, 400, 48), $"Score: {score}", style);
}
```

### uGUI のシグナル接続例
```csharp
[SerializeField] private Button startButton;
[SerializeField] private Slider healthBar;

void OnEnable()  { startButton.onClick.AddListener(OnStartPressed); }
void OnDisable() { startButton.onClick.RemoveListener(OnStartPressed); }

void OnStartPressed() { SceneManager.LoadScene("Game"); }
```

---

## 2D ゲーム固有のガイドライン

### キャラクタの移動 (Rigidbody2D)
```csharp
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private PlayerStats stats;
    private Rigidbody2D rb;

    void Awake() { rb = GetComponent<Rigidbody2D>(); }

    void FixedUpdate()
    {
        if (GameManager.State != GameState.Playing) return;

        var kb = Keyboard.current;
        float dir = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
        rb.linearVelocity = new Vector2(dir * stats.walkSpeed, rb.linearVelocity.y);

        if (kb.spaceKey.wasPressedThisFrame && IsGrounded())
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, stats.jumpForce);
    }
}
```

### 当たり判定 (Collider2D)
```csharp
void OnCollisionEnter2D(Collision2D c)
{
    if (c.gameObject.CompareTag("Enemy")) HandleEnemyCollision(c);
}

void OnTriggerEnter2D(Collider2D other)
{
    if (other.CompareTag("Item")) HandleItemPickup(other);
}
```

### スプライト・物理設定の指針
- スプライトの `pixelsPerUnit` はプロジェクト全体で一貫させる（混在NG）
- 高速移動するボール等は `CollisionDetectionMode2D.Continuous` + `Interpolate`
- 反射には `PhysicsMaterial2D` (friction=0, bounciness=1 等)

### アニメーション
- `Animator` + `AnimatorController` または `AnimatedSprite2D` 系
- 状態名は `StringName` 相当の `int Animator.StringToHash` でキャッシュ
```csharp
private static readonly int RunHash = Animator.StringToHash("Run");
animator.Play(RunHash);
```

---

## ファイル・フォルダ構成の推奨

```
Assets/
├── Scenes/                # シーンファイル (.unity)
├── Scripts/
│   ├── Core/              # GameState, シングルトン的なもの
│   ├── Stats/             # ScriptableObject 定義（PlayerStats, EnemyType 等）
│   ├── Player/
│   ├── Enemies/
│   ├── UI/
│   └── Common/            # 再利用コンポーネント (HealthComponent 等)
├── Prefabs/
├── {機能名}/              # 機能ごとに専用フォルダ (例: MCPTest/)
│   ├── Data/              # SO アセット (.asset)
│   ├── Sprites/
│   └── Materials/
├── Audio/
└── Fonts/
```

- スクリプトは `namespace` を切る (`namespace ProjectName.Player`)
- 1ファイル1クラスを原則

---

## スクリーンショットの保存先

このプロジェクトではスクリーンショットを **Unity Assets/ 外**に保存する:
- 保存先: `C:/GitHub/24hGameJam/MCP_Screenshots/shot-yyyyMMdd-HHmmss.png`
- gitignore 済み

---

## 回答時のフォーマット

コード生成時は以下の形式で回答すること:

```
【コンポーネント構成】: (最適なGameObject/コンポーネント構成を1行で)
【ロジック】: (実装するロジックの概要を1行で)

(実装コード)
```

### 例
```
【コンポーネント構成】: Player(Rigidbody2D + BoxCollider2D + PlayerController) + GroundCheck子オブジェクト
【ロジック】: 入力で水平速度を更新、地面に接地中のスペースキーでジャンプ速度を加算

[実装コード省略]
```

---

## 参考: 本プロジェクトでの A+B+C 実装例

`Assets/Scripts/MCPTest/` のブロック崩し実装が、本規約の A (イベント駆動) + B (ScriptableObject) + C (FSM) を全て満たす雛形になっている。新規実装時はこれを参照すること。
