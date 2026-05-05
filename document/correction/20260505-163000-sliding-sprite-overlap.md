# 是正報告書: スライディングアニメーションの地面めり込み

報告日: 2026-05-05
対象: `Assets/ScrollAction/` (スライディングアクション実装)
対象セッション: 2026-05-05 15:10 〜 16:30

---

## 1. 事象

「スライディング」アクションの追加実装中、ユーザーから次の3点の指摘を受けた:

1. **アニメーションがおかしい** — 9フレーム (立ち→屈み→滑走→戻り→立ち) のループで、立ち姿勢と滑走姿勢が交互に表示されてちぐはぐだった
2. **地面にめり込んでいる** — 滑走中、キャラクタースプライトの下半分が地面より下に描画されていた
3. **アニメは固定にしたい** — 滑走中はアニメ切替なしで1枚の絵を表示してほしい

上記のうち、最も深刻なのは 2. の **めり込み**。これは Claude が実装直後に「アニメは3行目のフレームを9枚使って実装しました」と完了報告した時点では検出できず、ユーザー実機プレイの目視で発覚した。

---

## 2. 真の原因

### 2.1 直接原因: pivot.y(rel) の不整合

`Assets/ScrollAction/Sprites/add-action.png` 内のスライディングフレーム (`add-action_154`, `_155`, `_156`) は **alignment=Center (0)** がそのまま使われており、`pivot.y(rel)=0.5` (= 絵中央) になっていた。

一方、既存の `idle` / `walk` 系で使われている `action.png` の `base_idle_*` などは `alignment=Custom (9)` + `pivot=(0.5, 0)` で **`pivot.y(rel)=0`** (= 絵下端中央) になっていた。

| スプライトシート | 用途 | alignment | pivot (rect 内 px) | pivot.y(rel) |
|---|---|---|---|---|
| `action.png` | idle / walk / jump 等 | Custom | (中央, 0) | **0 (絵下端)** |
| `add-action.png` | sliding 候補 (誤) | Center | (中央, 高さ/2) | **0.5 (絵中央)** |

Player の Visual GameObject は `localPosition.y = -0.5`、親 (Player) の `localScale.y = 1.2` のため、**Visual world中心 = Player底面 (= 接地面)** に配置される設計だった。pivot が絵下端なら絵全体が接地面より上に表示されるが、pivot が絵中央だと **絵の下半分 (高さ約 0.355 unit ≒ 35cm) が接地面より下** に描画される。これがユーザーが観察した「めり込み」の正体。

### 2.2 間接原因: 既存資産との整合性チェック省略

`add-action.png` の sprite 一覧を読み出した時点で、Sprite.pivot の絶対座標は手元にあった:

```
sliding: add-action_156 rect=(791,339,143,71) pivot=(71.50, 35.50) ppU=100
    halfHeight(world units)=0.355 pivot.y(rel)=0.5
```

しかし Claude はこの時点で「pivot=(71.5, 35.5) は中央 = 自然な配置」と片付けてしまい、**idle 系のpivot.y(rel) と比較しなかった**。比較していれば 0 と 0.5 のズレが即座に判明し、初回実装時にめり込みを防げた。

### 2.3 根本原因: 「pivot は絵足元かもしれない」を一度も疑わなかった

本プロジェクトには `feedback_verify_colliders.md` に次の記録があった:

> **追加の罠 (2026-05-05 しゃがみ実装セッション)**:
> 3. **sprite rect 内の余白/ラベル問題**: …pivot(0.5, 0) はラベル下端を指すため、`sr.bounds.bottom` の世界 Y は ground top と一致するのに、視覚的にはキャラ本体が宙に浮いていた。**「sr.bounds 数値が一致 = 足が地面に付く」とは限らない**

つまり「pivot がどこを指すか」「絵の足元と pivot がどう関係するか」が**過去のしゃがみ実装で既に問題になり、メモリにも残っていた**。にもかかわらず、Claude は新規スプライトを採用するときにこの教訓を引き出せず、idle 系と pivot ratio が違うことに気付かなかった。

---

## 3. 是正措置

### 3.1 即時是正 (本セッション内で実施済み)

**A. `add-action_156` の pivot を絵下端中央へ変更**

`add-action.png.meta` を編集:

```yaml
# 変更前
- name: add-action_156
  alignment: 0           # Center
  pivot: {x: 0, y: 0}    # alignment=Center のときは無視され、自動で (0.5, 0.5) 相当

# 変更後
- name: add-action_156
  alignment: 9           # Custom
  pivot: {x: 0.5, y: 0}  # 絵下端中央
```

**B. `sliding.anim` を `_156` のみの1フレーム固定に変更**

ユーザー要望「滑走中は絵を固定」と、複数フレームでめり込み問題を再発させないため、滑走表示は `_156` 1枚で固定。Animator Controller の Sliding state は `LoopTime=0` でフリーズ表示。

**C. 数値検証**

Unity API 経由で確認:

```
_156: pivot=(71.50, 0.00) pivot.y(rel)=0
idle 系: pivot.y(rel)=0
```

両者の pivot.y(rel) が一致したことを数値で確認。

### 3.2 メモリへの追記候補

既存の `feedback_verify_colliders.md` には「sprite 内のラベル問題」が書かれているが、**「同じプロジェクト内に異なるスプライトシートがある場合、pivot 設定が混在しているリスク」**は明示されていなかった。次セッション以降の運用ルールとして以下を追加すべき:

> 新しいスプライトシートから sprite を採用するときは、**既存の同じ用途のスプライトと `pivot.y(rel)` を比較**してから使う。プロジェクト内に複数スプライトシートがあると、シートごとに alignment/pivot 設定がバラバラなことがある。違いを見落とすと Visual GameObject の配置と pivot のズレで絵が浮く/めり込む。

### 3.3 検証チェックリスト (新規スプライト採用時)

1. **既存資産の pivot を先に確認** — 同じ Visual に表示する既存 sprite の `pivot.y(rel)` を Unity API で取得 (`Sprite.pivot.y / Sprite.rect.height`)
2. **新規 sprite の pivot.y(rel) と比較** — 値が一致しなければ alignment/pivot を変更してから採用
3. **採用後、1フレーム固定で表示して目視確認** — 接地面と絵足元のずれが数 px でも見えるよう、最低 200% 以上ズーム
4. **完了報告に「既存 pivot.y(rel)=X と一致を確認」を1行記載**

---

## 4. 派生的な不具合 (本セッション中に同時是正)

めり込み修正と同じセッションで、ユーザーから別の不具合報告を受けた:

### 4.1 「Z単押しで 0.3秒しか滑らない」問題

**症状**: 静止状態で Z を押下すると、設計上は 1秒滑るはずが 0.3秒で停止していた。

**原因**: SlidingAction が `MoveTowards(vx, 0, deceleration*dt)` で減速していたが、HorizontalMoveAction も同じ Tick 内で `MoveTowards(vx, 0, acceleration*dt)` を実行していたため、**両者の減速が二重に効いていた**。

| アクション | 1Tick の減速 |
|---|---|
| HorizontalMoveAction | acceleration × dt = 80 × 0.02 = 1.6 unit/sec |
| SlidingAction | deceleration × dt = 8 × 0.02 = 0.16 unit/sec |
| 合計 | 1.76 unit/sec/Tick (実質 88 unit/sec²) |

設計値 deceleration=8 unit/sec² に対して 11 倍の実効減速率になっていた。

**是正**: SlidingAction を「線形減速の絶対計算」方式に変更。`startSpeed` と `elapsed` から `currentSpeed = startSpeed - deceleration × elapsed` を毎 Tick 計算し、`rb.linearVelocity` を上書き。HorizontalMoveAction が vx を変えても次 Tick で SlidingAction が再上書きするため、二重減速が無効化される。

### 4.2 「移動速度で距離が変わらない」問題

**症状**: 走行中に Z を押しても、静止時とほぼ同じ距離しか滑らない。

**原因**: `walkSpeed=7` × `speedMultiplier=1.5` = 10.5 が `staticBoostSpeed=10` とほぼ同じになり、`max(慣性ブースト, staticBoost)` が常に staticBoost 側に張り付いていた。

**是正**: `speedMultiplier` を 1.5 → 2.0 に上げ、`maxDuration` を 1.0 → 1.2 に延長。検証結果:

| 開始時 vx | 初速 | 滑走時間 | 距離 |
|---|---|---|---|
| 0 (静止) | 10 | 1.0s | 6u |
| 7 (走行) | 14 | 1.2s | 11u |
| 12 (高速) | 24 | 1.2s | 23u |

### 4.3 「Z+方向キーで初速ブーストが弱い」問題

**症状**: 立ち止まった状態で「方向キー押しながら Z」をすると、HorizontalMove が 1Tick で vx を 1〜2 unit/sec 加速 → SlidingAction が「移動中」と判定 → `vx × multiplier` ≈ 2〜3 の弱い初速で滑り出す。

**是正**: `StartSliding` で初速を `Mathf.Max(|vx| × speedMultiplier, staticBoostSpeed)` に変更。最低保証として常に staticBoostSpeed が出るので、低速加速中でも 10 unit/sec の初速が出る。

---

## 5. 教訓

### 5.1 「過去の罠は別の形で再発する」

しゃがみ実装で「pivot 位置と絵足元の関係」で既に1回つまずき、メモリにも記録していた。にもかかわらず今回も同じ系統の罠 (pivot 設定の不一致) で再発した。**メモリの教訓を「字面通り (= sprite 内のラベル問題)」と狭く解釈せず、原則 (= pivot は常に検証対象) に一般化する**癖をつける必要がある。

### 5.2 「他のアクションとの相互作用は事前検証」

二重減速の問題は、SlidingAction を単体で見れば線形減速で 1秒滑る計算が正しい。しかし HorizontalMoveAction が同じ vx を同じ Tick 内で変更することを考慮していなかった。**新しいアクションを既存のアクション群に追加する場合は、ctx.rb.linearVelocity に書き込むアクションが他に何があるか、それらが同じ Tick 内で衝突しないかを確認する**。今回は「絶対計算で上書き」に変更することで衝突を解消した。

### 5.3 「ユーザーの体感は数値より優先」

「Z単押しで 0.3秒しか滑らない」とユーザーが報告した時点で、Claude の手元の予測 (1秒滑るはず) と乖離していた。これは「他のアクションとの相互作用」を見落としていたサイン。**ユーザーが体感した時間が予測と違ったら、まず予測モデルが現実を捉えているか疑う**。

---

## 6. 関連ファイル

### 修正対象
- `Assets/ScrollAction/Sprites/add-action.png.meta` — `_156` の alignment / pivot を変更
- `Assets/ScrollAction/Animations/sliding.anim` — 1フレーム固定 (`_156` のみ) に変更
- `Assets/ScrollAction/Scripts/Actions/SlidingAction.cs` — 線形減速の絶対計算に変更、初速 `Max(慣性, staticBoost)` 化
- `Assets/ScrollAction/Data/Actions/Sliding.asset` — speedMultiplier=2、maxDuration=1.2

### 検証用 asset
- `MCP_Screenshots/sliding_frames/add-action_154.png` 〜 `_159.png` — 各フレームの目視確認用に切り出し

### 関連メモリ
- `feedback_verify_colliders.md` — pivot と絵足元のズレを過去に経験済み (本件で再発)

---

## 7. ユーザー対応の反省

- ユーザーから「めり込んでいる」と最初に報告を受けた際、Claude は「立ちポーズフレームが入っているのが原因」と推測してアニメフレームを 9 → 3 枚に絞った。これは部分的に有効 (立ちポーズが消えた) だったが、**真因の pivot 不一致は解消されていなかった**。ユーザーから2回目の「まだめり込んでいる」指摘を受けてようやく Sprite.pivot の数値検証を行い、根本原因に到達した。
- **教訓**: 不具合の表面 (= 立ちポーズの違和感) を直す対症療法と、根本原因 (= pivot 不一致) の修正を混同しない。最初の報告時点で `Sprite.pivot` を idle 系と比較していれば、1回の修正で済んだ。
