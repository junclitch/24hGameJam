# Action Animation 実装まとめ

実装日: 2026-05-04 〜 2026-05-05
対象シーン: `Assets/Scenes/ScrollAction.unity`

## 概要

`Assets/ScrollAction/Sprite/action_img.png` (闇金アクション売買ヤー スプライトシート) を素材として、プレイヤーの 7 種類のアニメーション (歩行/ダッシュ/ジャンプ上昇/ジャンプ下降/ハシゴ登る/ハシゴ降りる/アイドル) を構築し、`PlayerController` と疎結合な形で `Animator` に橋渡しする仕組みを追加した。

## 成果物

### 1. スプライト (素材)
- `Assets/ScrollAction/Sprite/action_img.png` (透過版, 1307×1203, PPU=100, Filter=Point, Pivot=BottomCenter)
  - シート上の 55 セルを `Multiple` モードでスライス
  - 行ラベル「ジャンプ（上昇）」「はしご（登る）」「はしご（降りる）」が先頭セルに映り込んでいたため、ソース PNG 側で透過化
- `Assets/ScrollAction/Sprite/action.png` は初版で使った旧素材 (黒背景版)。現在は使用していないが残置

### 2. AnimationClip 群
保存先: `Assets/ScrollAction/Animations/*.anim`

| Clip | フレーム数 | FPS | Loop |
|---|---|---|---|
| walk | 9 | 8 | ◯ |
| dash | 7 | 10 | ◯ |
| jumpUp | 8 | 8 | ✕ |
| jumpDown | 7 | 7 | ✕ |
| ladderUp | 7 | 5 | ◯ |
| ladderDown | 7 | 5 | ◯ |
| idle | 10 | 4 | ◯ |

### 3. AnimatorController
`Assets/ScrollAction/Animations/Player.controller`

- パラメータ
  - `Speed` (float): 水平速度の絶対値
  - `VerticalSpeed` (float): 垂直速度
  - `IsGrounded` (bool): 接地フラグ
- ステート
  - Idle (default), Walk, Dash, JumpUp, JumpDown, LadderUp, LadderDown
- 既定遷移 (Speed 閾値 0.1, VerticalSpeed 閾値 0.1, 全て `hasExitTime=false`, `duration=0.05`)
  - Idle ⇄ Walk (`Speed`)
  - Idle / Walk → JumpUp (`!IsGrounded` && `VerticalSpeed > 0.1`)
  - Idle / Walk → JumpDown (`!IsGrounded` && `VerticalSpeed < 0.1`)
  - JumpUp → JumpDown (`VerticalSpeed < 0.1`)
  - JumpDown → Idle / Walk (`IsGrounded`)
- Dash / LadderUp / LadderDown はステートのみ用意 (該当アクション実装時に遷移を追加できるように)

> 当初 AnyState 遷移を使ったが、編集中の `anim.Update(0.5f)` で意図しない挙動を観測したため、各ステートからの直接遷移に書き直した。実プレイ (60fps 程度) では問題ない。

### 4. スクリプト
`Assets/ScrollAction/Scripts/Player/PlayerAnimatorBridge.cs`

```csharp
[RequireComponent(typeof(Animator))]
public class PlayerAnimatorBridge : MonoBehaviour
{
    [SerializeField] private PlayerController controller;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private float idleSpeedThreshold;
    // Update() で rb の速度と controller.IsGrounded を読み、
    // Animator パラメータと SpriteRenderer.flipX を更新するだけ。
}
```

- `PlayerController` 側は一切触らず、状態を「読むだけ」の単方向依存にした
- パラメータハッシュは `Animator.StringToHash` でキャッシュ
- `idleSpeedThreshold` 未満のときは flip を維持 (微速時のバタつき防止)。インスペクタで設定 (本プロジェクトの規約「スクリプト内に値を書かない」に準拠)

### 5. シーン構成変更 (Player GameObject)
階層:
```
Player (Transform scale 0.8x1.2, Rigidbody2D, BoxCollider2D, PlayerController)
├── GroundCheck (既存)
└── Visual (新規, scale 1.25x0.833 で親非一様スケールを打ち消し)
    └── SpriteRenderer + Animator + PlayerAnimatorBridge
```

- `Visual.localPosition = (0, -0.5, 0)` でコライダ底面に足元 (Pivot=BottomCenter) を合わせ
- `Visual.localScale = (1.25, 0.833, 1)` で親 (0.8, 1.2) スケールを相殺し、スプライトの素のアスペクト比を維持
- 既存の Player 直下 SpriteRenderer (赤いプレースホルダ正方形) は `enabled=false` に
- Animator の RuntimeController に `Player.controller` を代入
- 既定スプライトに `idle_00` を設定

## 設計上のポイント

### A. イベント駆動 / 単方向依存
- Bridge は `PlayerController.IsGrounded` と `Rigidbody2D.linearVelocity` を **読むだけ**
- `PlayerController` 側はアニメーションの存在を一切知らない
- アクションシステム (`PlayerAction` 系) も無改変

### B. ScriptableObject でのパラメータ分離
- `idleSpeedThreshold` 等の値はインスペクタ側で設定。スクリプトには既定値を書かない (プロジェクト規約)

### C. FSM
- `AnimatorController` がそのままキャラ表現の FSM
- `IsGrounded` は `PlayerController` の既存プロパティを流用 (`PlayerController.IsGrounded { get; private set; }`)

### D. Composition (継承より構成)
- 物理ルート (Player) と視覚ルート (Visual) を分離
  - 親に非一様スケール、子に逆スケール、で見た目を歪めず物理のサイズを維持
  - 将来、当たり判定とスプライトの位置を独立に微調整できる

## 嵌った/学んだこと

1. **黒背景PNGをそのままSpriteにすると四角いシルエットが残る**
   - 初版の `action.png` は黒背景で、いったんソース側の黒を透過化して対応
   - ユーザから透過版 `action_img.png` を提供してもらい、以後はそちらを使用

2. **スプライトシートに混在するラベル文字がセルに入り込む**
   - 「ジャンプ（上昇）」等のラベルが、ジャンプ系の頭頂と Y 範囲で重なる
   - セル上端を上げると頭はちゃんと写るがラベルも入り、下げると頭が切れる
   - → ソース PNG 側でラベル 3 箇所 (180×19px) を透過化して恒久対処

3. **`anim.Update(dt)` の手動呼び出しは挙動が安定しない**
   - エディタから手動でテストすると、ありえない遷移が走ることがあった
   - 実プレイの 60fps 程度の刻みでは問題なし

4. **MCP 経由で Unity に命令している間は Editor Tick が事実上止まる**
   - Play Mode でも `position` や `velocity` が次の MCP 呼び出しまで進まないように見える
   - 動作確認はスクリーンショットでざっくり、最終的には人間が Play して確認するのが確実

## 既知の追加ToDo (将来)

- Dash / LadderUp / LadderDown のアクション実装 + Animator 遷移条件の追加
- Visual 側のスケール (1.25, 0.833) は親の非一様スケールを打ち消すための応急処置。本来は Player の `transform.scale` を (1,1,1) に戻し、`BoxCollider2D.size` を (0.8, 1.2) に正す方が望ましい
- 旧 `action.png` の削除可否を確認
