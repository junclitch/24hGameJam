# Unity 2D 物理接触検出の使い分け指針

このプロジェクトのプレイヤー / 敵 / ハザードなどで「壁に当たっているか」「地面に接地しているか」「攻撃が当たったか」を判定する時、Unity 2D 物理には複数の API がある。**用途を取り違えると検出取りこぼしや無音の不発で根治しないバグになる**ので、選び方を本ドキュメントにまとめる。是正報告書 `document/correction/20260507-003046-air-control-wall-friction-detection.md` の事故を受けて起こした。

---

## 1. 結論 (まずこれを守る)

| 用途 | 第一候補 API |
|---|---|
| **既に接触している状態への反応** (壁押し付け抑制、滑り判定、接地後の挙動) | `Rigidbody2D.GetContacts` / `Collider2D.GetContacts` |
| **まだ接していない対象の予測検出** (壁ジャンプ発動、足場予測、先読み) | `Physics2D.Raycast` / `Collider2D.Cast` |
| **領域判定** (踏むと発動するハザード、トリガーゾーン、接地サークル) | `Physics2D.OverlapBox/Circle` または `OnTriggerEnter2D` |

**TL;DR**: 「既存接触への反応」と「未接触対象の予測検出」を分けて API を選ぶ。**隣接コードが Raycast を使っているからといって同じ手段が自分の用途にも適切とは限らない**。

---

## 2. なぜ Raycast を「既存接触」に使うとはまるか

### 2.1 レイヤー依存
`Physics2D.Raycast(origin, dir, distance, mask)` は LayerMask 引数で「どのレイヤーを当てに行くか」を指定する。
- レイヤー指定を間違える
- シーンの壁が想定と違うレイヤーにいる
- レイヤー設定を後から変更した

これらで**音もなく検出が発火しなくなる**。隣接コードのコメント (例: 「壁=groundLayer 前提」) を根拠にレイヤー前提を採用すると、シーン構成変更で簡単に破綻する。

### 2.2 距離・origin 精度
- `origin` に `rb.position` を使うと、Rigidbody 中心と Collider 中心が違う場合 (Collider Offset がある場合) 想定位置からズレる
- distance に `bodyCollider.bounds.extents.x + slop` を使うと、コライダー形状が長方形でない場合 (Capsule, Polygon) bounds の AABB 半幅と実形状半幅にズレが出る

### 2.3 単一直線の取りこぼし
中心高さからの 1 本の Ray は、壁が短い場合 (例: 半身高さの段差) や Player 高さの上下端で当たる場合に高さが合わず通り抜ける。Player 高さ全体をカバーするには複数 ray か `Collider2D.Cast` (シェイプキャスト) が必要。

これらは「**まだ接していない対象を予測検出したい**」用途では設計コストとして許容できる (能動的検出なので発火条件を意図的に絞り込みたい)。だが「**既存接触に反応する**」用途では検出取りこぼしが直接バグになる。

---

## 3. 推奨パターン: 既存接触への反応は GetContacts

`Assets/ScrollAction/Scripts/Actions/AirControlAction.cs` で採用しているパターン:

```csharp
private static readonly ContactPoint2D[] ContactsBuffer = new ContactPoint2D[16];
private const float WallNormalThreshold = 0.7f;

private bool IsTouchingWallInDirection(PlayerActionContext ctx, float sign)
{
    int n = ctx.rb.GetContacts(ContactsBuffer);
    for (int i = 0; i < n; i++)
    {
        Vector2 normal = ContactsBuffer[i].normal;
        if (Mathf.Abs(normal.x) < WallNormalThreshold) continue;
        if (Mathf.Sign(normal.x) != Mathf.Sign(sign)) return true;
    }
    return false;
}
```

ポイント:
- **物理エンジンが解決済みの接触をそのまま読む**ので、レイヤー指定も distance 計算も origin 計算も不要
- `ContactPoint2D.normal` から「左右どちら側の壁か」を `normal.x` の符号で判別できる
- `WallNormalThreshold = 0.7f` で水平に近い接触のみ「壁」扱い (床・天井の接触と区別)
- バッファは `static readonly` で事前確保し毎フレームのアロケーションを抑制

### 3.1 ContactPoint2D.normal の方向 (重要)

Unity 2D の ContactPoint2D.normal は「**相手 (other) → 自分 (this rigidbody)**」向き:

| 接触相手 | 法線方向 | 符号 |
|---|---|---|
| 右壁 | 左向き | `normal.x < 0` |
| 左壁 | 右向き | `normal.x > 0` |
| 床 | 上向き | `normal.y > 0` |
| 天井 | 下向き | `normal.y < 0` |

**「自分が右に押そうとしている (sign=+1) のに、右に壁がある (normal.x < 0)」を「入力符号と normal.x の符号が逆」**で判定する。

### 3.2 押し付け摩擦の機構 (なぜ targetVx=0 が必要か)

Box2D / Unity 2D の摩擦力:

```
friction_impulse ≤ μ × normal_impulse
normal_impulse = 押し付けを止めるのに必要な mass × Δvx
```

押し付け速度が大きいほど normal_impulse が大きく、摩擦上限も大きい。摩擦は**接線方向 (この場合は vy)** に作用するので、壁に押し付け続けると縦速度が削られる ("引っかかる" 症状の正体)。

**`targetVx = 0` にして押し付け速度ゼロ → normal_impulse ゼロ → 摩擦ゼロ** という機構で症状を消す。これが効くためには「壁との接触」を確実に検出する必要があるので、検出側 (= GetContacts) の信頼性が決定的に重要。

---

## 4. 推奨パターン: 未到達対象の予測検出は Cast/Raycast

`Assets/ScrollAction/Scripts/Actions/WallKickAction.cs` の壁ジャンプ発動条件:

```csharp
private float DetectWall(PlayerActionContext ctx)
{
    Vector2 origin = ctx.rb.position;
    LayerMask mask = ctx.stats.groundLayer;

    var hitR = Physics2D.Raycast(origin, Vector2.right, wallCheckDistance, mask);
    if (hitR.collider != null) return 1f;

    var hitL = Physics2D.Raycast(origin, Vector2.left, wallCheckDistance, mask);
    if (hitL.collider != null) return -1f;

    return 0f;
}
```

ポイント:
- **接触前から検出したい** (壁にぶつかってからでは壁ジャンプの発動チャンスを逃す) ので Raycast が適切
- レイヤー指定 (`groundLayer`) で「壁ジャンプ可能な対象」だけを絞り込めるのが利点
- `wallCheckDistance` で「どこまで近づいたら発動可能とするか」をパラメータ化できる

### 4.1 シェイプキャスト版 (背丈方向の取りこぼしを防ぎたい時)

```csharp
private static readonly RaycastHit2D[] CastHits = new RaycastHit2D[4];

ContactFilter2D filter = default;
filter.SetLayerMask(layerMask);
filter.useTriggers = false;
int n = bodyCollider.Cast(direction, filter, CastHits, distance);
return n > 0;
```

`Collider2D.Cast` はコライダー形状を入力方向に前進させて衝突するか確認するので、単一 ray より背丈方向の取りこぼしに強い。

---

## 5. 領域判定は OverlapBox/Circle または Trigger

`Assets/ScrollAction/Scripts/Player/PlayerController.cs` の接地判定:

```csharp
bool grounded = Physics2D.OverlapCircle(groundCheck.position, stats.groundCheckRadius, stats.groundLayer);
```

「特定の点・領域が何かに被っているか」を見るだけなので Overlap 系。距離・方向の概念は不要。

トリガーゾーン (チェックポイント、ハザード当たり、ゴール) は `OnTriggerEnter2D` / `OnTriggerStay2D` で受ける方が素直。`Physics2D.Overlap*` を毎フレーム呼ぶより軽く、コリジョン側で `Is Trigger` をオンにするだけで設定が済む。

---

## 6. 検証プロトコル

「直った」と報告する前に必ず行う:

### 6.1 検出が発火しているか先に確認する
コードに修正を入れたら、検出メソッド (`IsTouchingWallInDirection` 等) の戻り値を `Debug.Log` で出して、**期待タイミングで true になっているか**を play mode で先に確認する。検出が発火していないと、その後ろのアクション (`targetVx=0` 等) が正しくても効果ゼロで根治しない。

### 6.2 修正の (i)〜(iv) を独立に検証する

物理修正は 4 段階に分けて、各段階を独立に「これで本当に動くか」を問い直す:

- **(i) 仮説**: なぜその症状が出るかの機構を 1 行で説明できるか (例: μ × normal_impulse による vy 削り)
- **(ii) 検出**: 期待タイミングで発火するか (上記 6.1)
- **(iii) アクション**: 検出時の挙動が想定通りか (例: vx が 0 にクランプされているか)
- **(iv) 副作用**: 周辺機能が壊れていないか (壁から離れる入力、関連アクション、地上挙動の回帰)

(i) と (iii) の対応だけで完了判定しない。(ii) を独立に確認しないと「仮説は正しいのに検出が不発で症状が直らない」パターン (本ドキュメントの起点になった事故) を踏む。

### 6.3 play mode end-to-end 観測
コード変更だけで「直しました」と報告しない。play mode で

- バグ症状そのもの (今回なら: 壁横ジャンプで落下速度が削られないか)
- 周辺機能の回帰 (壁から離れる入力、WallKick 発動、地上歩行)

を最低 1 周通してから報告する。MCP / 撮影で確認できない部分は「ユーザに play mode 確認を依頼」と明示する。沈黙でユーザに丸投げしない (`feedback_animator_e2e_verify.md` 参照)。

---

## 7. 関連ドキュメント・メモリ

- `document/correction/20260507-003046-air-control-wall-friction-detection.md` — このドキュメントを起こした事故の是正報告
- `document/unity-coding-conventions.md` — プロジェクト全般のコーディング規約
- 自動メモリ `feedback_physics_contact_detection.md` — 本指針の TL;DR
- 自動メモリ `feedback_animator_e2e_verify.md` — 動的なものは end-to-end で観測する
- 自動メモリ `feedback_verify_colliders.md` — 見た目だけで完了判定しない

---

## 8. 既存実装の使い分け一覧

このプロジェクトで物理接触判定を持つコンポーネントの API 選択 (本指針の参照実装):

| ファイル | API | 用途 | 妥当性 |
|---|---|---|---|
| `Actions/AirControlAction.cs` | `Rigidbody2D.GetContacts` | 既存接触に反応 (壁押し付け抑制) | ✅ |
| `Actions/WallKickAction.cs` | `Physics2D.Raycast(groundLayer)` | 未到達対象の予測検出 (壁ジャンプ発動条件) | ✅ |
| `Player/PlayerController.cs` | `Physics2D.OverlapCircle(groundLayer)` | 領域判定 (接地判定サークル) | ✅ |

新たに物理接触判定を実装する時は本指針に沿って API を選ぶこと。
