# Unity Coding Conventions (2D Game / Unity 6 / C#)

このプロジェクトは Unity Engine 6000.3 系を使用した2Dゲーム開発プロジェクトです。AIアシスタント（Claude / Copilot 等）にコードを生成させる際は、本ドキュメントの規約に従うこと。

---

## 思考プロセス

**コードを生成する前に、必ず以下を行うこと：**
1. どの GameObject / コンポーネント構成が最適かを1行で説明する
2. 実装するロジックの概要を1行で説明する
3. その後に実装コードを提示する

---

## 推奨・設定変更の規律 (重要)

過去の事故 (`document/correction/20260506-232549-webgl-decompression-fallback.md`) を踏まえた行動規範。**コードに限らず、Player Settings・パッケージ設定・プラットフォーム固有設定を提案・変更する時にも適用する。**

### 1. 「○○推奨」と書く前にソースを引く
ユーザに提示する表のヘッダや本文に "X 推奨" と書くなら、**X の公式情報・コミュニティ実例のいずれかを参照済み**であること。引けない時は「X 推奨」と書かず、「一般的に言われる」「私の理解では」「未検証だが」と強さを下げる。"推奨" は裏取りの含みがある語であり、根拠なしに使うと信頼性の偽装になる。

### 2. 設定値の機構を1行で説明できなければ提案しない
推奨を出す前に、その設定が**何を制御しているか**を自分で言語化する。例: 「`decompressionFallback` = サーバが Content-Encoding を返せない環境用の JS デコーダ同梱フラグ」。説明文が書けない＝機構を理解していない、なので、その状態で値を勧めない。

### 3. 「保険」「安全側」「念のため」での有効化を警戒する
デフォルト false の設定を true にする時は、**有効化のコスト** (ビルドサイズ・パフォーマンス・複雑性) を併記して比較する。「コストゼロの保険」はほぼ存在しないという認識で構える。WebGL ビルドのように体験を直撃するコストがある場面では、効かない保険は"安全側"ではなく"悪化側"。

### 4. プラットフォーム指定があれば、汎用アドバイスを直接当てはめない
UnityRoom / itch.io / GitHub Pages / 自前サーバはそれぞれ前提が違う (CDN挙動・iframe 制約・canvas 仕様・配信ヘッダ)。前提が違えば最適値も違う。話題が「UnityRoom」と指定されたら、まず UnityRoom 固有の前提を確認層として通してから、その上で汎用知識を組み合わせる順序を守る。

### 5. 依頼された主たる修正以外の "ついで変更" をしない
バグ修正や設定見直しの場で、ついでに目に入った別設定を「触っといた方が安全」で雑に変えない。**主たる依頼スコープを越える変更は確認なしに行わない**。確認したものだけ提案する (`feedback_no_unrequested_additions.md` と同根の規範)。

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

## 確認・撮影プロトコル (完了報告前に必ず実施)

### 静的レイアウト (時間と無関係なもの)
GameObject 配置 / UI レイアウト / シーン構成 / 単一 sprite 表示の確認は同期描画スクショで OK:
```csharp
cam.targetTexture = rt;
cam.Render();          // 同期: 即時描画
cam.targetTexture = null;
RenderTexture.active = rt;
tex.ReadPixels(...);
File.WriteAllBytes(path, tex.EncodeToPNG());
```

### 動的・アニメーション (時間で変化するもの)
**単発スクショで完了判定しない。** アニメーション・物理挙動・状態遷移は以下のいずれかで確認する:

1. **連続フレームを同期撮影 → 1枚ずつ目視**
   ```csharp
   for (int i = 0; i < frameCount; i++) {
       anim.Update(1f / sampleRate);
       cam.Render();  // 同期撮影
       // save as frame_NNN.png
   }
   ```
2. **ffmpeg で動画化して目視**
   ```bash
   ffmpeg -framerate 6 -i frame_%03d.png -vf "scale=400:-1" anim.mp4
   ```
3. **ユーザーに実機録画を依頼** — 入力に応じた状態遷移など、自動化困難な操作は最初からこれを使う

### 撮影時の禁止事項
- ❌ `ScreenCapture.CaptureScreenshot` を非同期で使ってすぐ次の処理に進む (撮影は次フレーム末尾に遅延し、その間に状態が変わる)
- ❌ `QueuePlayerLoopUpdate` + `Sleep` で「待てば撮れる」と仮定 (MCP 経由では Unity 主スレッドがブロックされて進まないことがある)
- ❌ 強制配置 (`transform.position = ...`) して即撮る (物理が落ち着いていない / 接地判定未走行のまま撮ってしまう)
- ❌ 640×360 程度のサムネイルだけで判定 (数 px のずれが見落とされる → 200% 以上ズームしたクロップで足元/エッジを照合)

### 撮影前のチェックリスト
1. **状態を意図通りにセット** (Animator パラメータ・rb.velocity・transform)
2. **状態を「適用」させる** (`anim.Update(dt) × 数回` / `fixedUpdate.Invoke × 数回` を手動で呼ぶ)
3. **同期描画で撮る** (`Camera.Render()` → `ReadPixels` → `EncodeToPNG`)
4. **撮影後に状態をログ出力** (`sprite.name`、`transform.position` 等を return して撮影意図とのズレを検証)

### ユーザー観察 > Claude の数値検証
- **ユーザーが視覚的に観測した事象は、Claude の数値検証より優先する**
- 「数値は一致しているのに視覚で違和感」と言われたら、まず**「数値が見ているもの」と「視覚が見ているもの」が違う何かを指している可能性**を疑う
- 反論する前に、自分の検証用スクショを開示してユーザーと一緒に確認する

### Sprite 関連の追加チェック
- 新規スプライトをスライスしたら **各フレームを個別に extract して目視** (印刷参考のラベル・ガイド線・余白が rect 内に紛れ込んでいないか)
- pivot 位置 (例 `(0.5, 0)`) が**絵の足元**と一致しているか確認 — `sr.bounds.bottom` は **rect の底辺**であって**絵の足元**とは限らない
- 不安なら `sr.bounds.bottom` 位置に小さな赤クアッドをデバッグマーカーとして配置し、絵の実位置とのズレを可視化

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
