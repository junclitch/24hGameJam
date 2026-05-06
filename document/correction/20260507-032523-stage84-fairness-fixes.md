# 是正報告書: Stage84 終盤区画の理不尽要素 4 件 (8連飛び石・落下鉄球)

報告日: 2026-05-07
対象シーン: `Assets/Scenes/ScrollAction_MCP.unity`
対象スクリプト:
- `Assets/ScrollAction/Scripts/Actions/RollingAction.cs`
- `Assets/ScrollAction/Scripts/Hazards/FallingBallHazard.cs`

---

## 1. 事象

ユーザから以下 4 件の「理不尽」フィードバックを順に受け、同一セッションで連続修正した。

| # | 区画 | ユーザ主訴 | 根本症状 |
|---|---|---|---|
| 1 | 8連 (`RollingBridge_Gap2`) | 「穴に少し触れただけでダメージをくらう」 | `HazardsRoot/RollingFallTrap` の Trigger が小石上端 (y=-1.50) のわずか 0.10u 下 (y=-1.60) で発火する設計。着地時の物理めり込みや小ジャンプの掠り着地で即死 |
| 2 | 落下鉄球 (`FallingBall_*`) | 「鉄球の当たり判定が消えた後も残ってて理不尽」 | サイクル全フェーズ (上待機 / 落下中 / 下着地) で Collider2D が常時有効。下着地 (waitAtBottom=0.5s) の間「もう落ち切った」と見える瞬間に判定が残る。ジェットパックで上に上がると上待機 (topY=10) の見えない球にも当たる |
| 3 | 8連 (転がり通過) | 「転がるなら通り抜けられる想定だったが落ちた」 | `CanBridgeSmallGap` が "1.5u 先 1 点の `OverlapCircle`" だったため、小石ピッチ 2u と石幅 0.5u の組み合わせで **`[center+0.93, center+1.57]` (0.64u 幅) の死角** が発生。プローブが次の足場を飛び越えてしまう区間で `currentlyShrunk` がリセットされて落下 |
| 4 | 8連 (転がり通過、3 修正後) | 「(落ちはしないが) 引っかかる」 | bridging で `vy=0` を立てても、その直後の `Physics2D.Simulate` が `gravity × Rigidbody2D.gravityScale (=3) × dt = 0.59 u/s` を加速度として加算。毎フレーム約 0.012u ずつ落下し、8 フレーム (≒ギャップ通過時間) で BC 底面が小石上端を割り、次の小石の左側面と side-collision で押し戻されて固着 |

---

## 2. 真の原因と修正方針

### 2.1 #1 RollingFallTrap の過剰な絞り

設計ドキュメント (`document/scrollaction-mcp-design.md` §3.3) で **「ロール (vy=0 ブリッジ) 限定で渡らせる」** 意図のもと、KillZone 上端を石上端の 0.10u 下に置き「歩きで 0.147u 落ちただけで即死」を狙っていた。意図は明確だが、

- 着地時の物理めり込みで偽陽性死 (= 乗ったのに死ぬ)
- 微小な踏み外しで「穴に触れただけ」感

を生み、体感を犠牲にしていた。

**対応**: `HazardsRoot/RollingFallTrap` を `SetActive(false)`。落下死は既存メイン `KillZone (y=-10)` が引き継ぐ。マリオ的な「穴に落ちて画面下で死ぬ」挙動になり、視覚と判定が一致する。設計縛り (= ロール限定通過) は緩むが、「理不尽さの解消」を優先した。

### 2.2 #2 落下鉄球コライダの常時有効化

`FallingBallHazard` は **3 フェーズ全てで Collider2D 有効**だった。視覚的に「落ち切った」と見える `waitAtBottom (0.5s)` でも当たり判定が残る。さらに `topY=10` の上待機中、ジェットパックで届く高さに「見えない判定」が居座る。

**対応**: `FallingBallHazard.Awake` で `GetComponent<Collider2D>` をキャッシュし、`Update` 末尾で **`hitCollider.enabled = falling` (落下フェーズのみ true)** に。

```csharp
if (hitCollider != null && hitCollider.enabled != falling)
    hitCollider.enabled = falling;
```

`OnImpact` イベント (SE) は `wasFalling`/`impacted` フラグから決まるので、判定オフでも着地音は鳴る。

### 2.3 #3 ブリッジ判定の死角 (1 点プローブの幾何問題)

旧版:
```csharp
Vector2 probe = (Vector2)ctx.groundCheck.position + new Vector2(dir * maxGapWidth, 0f);
return Physics2D.OverlapCircle(probe, ctx.stats.groundCheckRadius, ctx.stats.groundLayer) != null;
```

これは **「現在地から `maxGapWidth` 先の 1 点を見る」** だけで、その点が次の足場を飛び越して空中だと false になる。8連 (石幅 0.5u, ピッチ 2u, ギャップ 1.5u, groundCheckRadius=0.18) を幾何で詰めると:

- Stone N の grounded 範囲: `[center-0.43, center+0.43]`
- 1.5u 先 1 点で Stone N+1 (center+2) を捕捉できる範囲: `[center+0.07, center+0.93]`
- Stone N+1 の grounded 範囲: `[center+1.57, ...]`
- → **`[center+0.93, center+1.57]` (0.64u 幅) は両方 false の死角**

新版:
```csharp
Vector2 origin = ctx.groundCheck.position;
float r = ctx.stats.groundCheckRadius;
Vector2 center = origin + new Vector2(dir * maxGapWidth * 0.5f, 0f);
Vector2 size = new Vector2(maxGapWidth, r * 2f);
return Physics2D.OverlapBox(center, size, 0f, ctx.stats.groundLayer) != null;
```

**「足元〜進行方向 maxGapWidth まで・高さ 2*r の帯状」を `OverlapBox` で連続走査** に変更。これで `[center+0.25, center+2.25]` がブリッジ範囲となり grounded と連続接続、死角ゼロ。8連区間 49.5..67.5 を 0.05u 刻みでサンプリングしてシミュレーションした結果:

| 実装 | 連続失敗最大長 |
|---|---|
| 旧 (1 点) | 12 ステップ ≒ 0.60u |
| 新 (帯) | **0** |

### 2.4 #4 bridging 中の重力打ち消し漏れ

#3 修正後に「引っかかる」が再発。Play モードで `Physics2D.simulationMode = Script` に切替えて 1 フレームずつ手回しした結果、

- bridging で `vy=0` を立てた直後の `Physics2D.Simulate(0.02)` で `vy = -0.59` まで落ちる (= 0 + gravity*gravityScale*dt = -9.81 * 3 * 0.02)
- 1 フレームあたり約 0.012u 落下
- 8 フレームで BC 底面が小石上端 -1.50 を 0.023u 下回る
- 次の小石の左側面 (y range [-2.99, -1.50]) と side-collision、押し戻されて x=50.155 で固着

**真因は速度 (vy) の操作タイミングが Physics2D.Simulate の重力積分より前**であること。`vy=0` を書いてもその直後に重力で書き換わる以上、速度ベースで重力を打ち消すには整合しない。

**対応**: bridging 中だけ `Rigidbody2D.gravityScale = 0` を立てて重力そのものを停止。

```csharp
if (bridging)
{
    if (!gravityOverridden)
    {
        originalGravityScale = ctx.rb.gravityScale;
        gravityOverridden = true;
    }
    ctx.rb.gravityScale = 0f;
}
else
{
    RestoreGravityScale(ctx.rb);
}
```

冪等な `RestoreGravityScale` で「2 度 restore しても安全」「途中で respawn しても次 Tick で確実に戻る」を保証。`OnSessionInit` で `gravityOverridden=false` / `originalGravityScale=-1` を初期化し Domain Reload に追従。

修正後シミュレーション (200 フレーム回転、x=48 → 70):

| ステップ | x | y | grounded | gravityScale |
|---|---|---|---|---|
| 7-13 | 49.4..50.5 | **-0.885** | False | **0** |
| 14-18 | 50.7..51.4 | -0.885 | True (Stone 0) | 3 |
| 19-24 | 51.5..52.4 | -0.885 | False | 0 |
| ... 各石/ギャップで反復 ... |
| 120 | 69.5 | -0.884 | True (Floor_7 (1)) | 3 |

**y が -0.885 で完全固定** されており、ギャップ全 8 区間を vy=0 で水平に渡れることを確認。

---

## 3. 反省 / 学び

### 3.1 「理不尽」フィードバックが連鎖したのは検証粒度の不足

#1 を直したら #3 が露出 (= 落下死を緩めたら今度は「ロールでも落ちる」が見えた)、#3 を直したら #4 が露出 (= 落下しなくなったら今度は「引っかかる」が見えた)。順次顕在化したように見えるが、**根本は当該シーンの "通り抜け試験" を Play モードで通していなかった**こと。最初の修正の段階で 1 度通せていれば、#3 #4 はそこで一緒に発見できていた。

教訓: アニメ/遷移系は単体検証だけで完了報告しない (既存メモリ `feedback_animator_e2e_verify.md` と同根)。**当該区画を「実際にプレイヤー位置/入力で通過させて」観測する**まで「直した」と言わない。今回 #4 ではこの作法に従い、`Physics2D.simulationMode=Script` で手回しシミュレーションして固着を実測してから修正に入った。

### 3.2 物理エンジンの「速度操作」は Simulate のタイミングと一緒に考える

#4 の真因 — `vy=0` と書いても Simulate が重力で上書きする — は **「速度を立てる」操作の意味が物理エンジン内のステップ順序に依存する**ことを示す典型例。

| 操作意図 | 適切なフィールド |
|---|---|
| 1 フレームの初速 (Simulate がそこから重力等で時間発展させる前提) | `linearVelocity.y` |
| 「とにかく落ちるな」(重力そのものを停止) | `gravityScale = 0` |
| 一瞬のジャンプ | `AddForce(..., Impulse)` または `linearVelocity.y` 上書き |

**「物理エンジンが何をやってくれるか」を理解せずに速度だけ書くと、Simulate に書き戻される**。ロール bridging のような「重力を完全に打ち消したい」要件は速度ではなく gravityScale で表現するのが整合する。

### 3.3 幾何条件の検証は数値計算で詰める

#3 の死角は **すべて式で書ける** (Stone 中心・半径・半幅・groundCheckRadius・maxGapWidth から「両方 false の区間」が機械的に出る)。1 点プローブで maxGapWidth を設定する設計判断の段階で、**「死角が出ない maxGapWidth 範囲」を式で確認していれば**実装前に問題が見えていたはず。後追いの帯走査で fix できたが、最初から幾何で詰めて 1 点プローブの限界を認識すべきだった。

### 3.4 設計意図と体感のトレードオフは体感を優先

#1 の `RollingFallTrap` は「ロール限定通過を強制する」明確な設計意図のもと配置されていたが、ユーザに「理不尽」と評された。**設計者本人がプレイして「理不尽」と言う以上、その縛りは現状コストに見合っていない**。設計ドキュメントを根拠に縛りを残すのではなく、ユーザの体感に合わせて緩めるのが正解。設計意図 (ロール推奨) はステージ難度や石の幅で再現する余地があるので、KillZone の絞りに頼らない。

---

## 4. 関連ファイル

### 修正
- `Assets/ScrollAction/Scripts/Actions/RollingAction.cs`
  - `CanBridgeSmallGap` を `OverlapCircle` 1 点 → `OverlapBox` 帯走査に
  - `OnFixedTick` に `gravityScale=0` 切替ロジック (`gravityOverridden` フラグで冪等管理)
  - `OnSessionInit` で gravity 関連フラグを初期化
- `Assets/ScrollAction/Scripts/Hazards/FallingBallHazard.cs`
  - `Awake` で `Collider2D` をキャッシュし初期 `enabled=false`
  - `Update` 末尾で `hitCollider.enabled = falling` (落下フェーズのみ有効)
- `Assets/Scenes/ScrollAction_MCP.unity`
  - `HazardsRoot/RollingFallTrap` を `activeSelf=false`

### 参照
- `document/scrollaction-mcp-design.md` §3.3 (8連小石ブリッジ設計)
- 既存メモリ `feedback_animator_e2e_verify.md` (アニメ/遷移系は E2E 検証必須)
- 既存メモリ `feedback_physics_contact_detection.md` (GetContacts vs Raycast の使い分け)
