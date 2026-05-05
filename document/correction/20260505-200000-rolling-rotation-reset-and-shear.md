# 是正報告書: 転がるアクションの回転後 axis 戻らない問題と sprite shear (せん断歪み)

報告日: 2026-05-05
対象: `Assets/ScrollAction/` (転がるアクション実装)
対象セッション: 2026-05-05 18:00 〜 20:00 頃

---

## 1. 事象

「転がる」アクション (Q キー長押し → 5 コマ目を回転) を実装した過程で、ユーザーから 3 段階の指摘を受けた:

| 指摘 | 内容 |
|---|---|
| 1 | 「目の位置が上下します。固定して。左も」 |
| 2 | 「比率を変えるのはやめてください」 (回転中に絵の比率が変わる = shear 歪み) |
| 3 | 「終了時に rotate を 0 に戻して」(ロール完了後に体の軸が傾いたまま) |

特に 1, 3 については **「直りました」と報告した直後にユーザーが play mode で動かして「直っていない」と返してくる**パターンが続いた。

---

## 2. 真の原因

### 2.1 指摘 1: 目の位置が上下する

#### 直接原因 (前段): Animator State 機械の構造バグ

最初は「Any State → RollingStart の遷移条件に Bool パラメータ `IsRolling` を使っていた」ことが原因。Bool だと条件が真の間ずっと再発火し、`RollingStart → RollingLoop` の出口時間で Loop に着いた瞬間、Any State 遷移が再発火して RollingStart に引き戻される。これにより、Animator は Start と Loop を高速で往復するだけで、Loop に居座らなかった。

LateUpdate の補正は Loop 状態でしか走らないので、状態が安定しないと「補正が走ったり走らなかったり」になり、目が暴れて見えていた。

→ **修正**: `RollingTrigger` (Trigger 型) を追加し、Any State → RollingStart の条件を Trigger に変更。Trigger は発火時に消費されるので一度しか入らない。`PlayerAnimatorBridge` が `controller.IsRolling` の立ち上がりエッジで `SetTrigger` を呼ぶ。

#### 直接原因 (後段): 不動点の数値が実測値とズレていた

Trigger 化で状態は安定したが、まだ目の位置が微振動。原因は **不動点 (sprite-pixel 座標での目位置) の推定値がズレていた** こと。最初は連結成分 bounding box 中心 (67, 43.5) を採用していたが、実機レンダリングで empirical に測ると (67.67, 43.92) だった。

→ **修正**: empirical 値に置換 (`rollingEyeOffsetSprite = (0.20, 0.435)` sprite-local world units, ただし後段の Spinner 構造化でこの値に統一)。

### 2.2 指摘 2: 比率が変わる (shear 歪み)

#### 真の原因: 親の非一様スケールと子の回転の合成

シーン構造:

```
Player    (localScale = 0.8, 1.2)   ← 物理ルートの非一様スケール
  └─ Visual (localScale = 1.25, 0.83)  ← Player.scale を打ち消す counter-scale
     └─ (SpriteRenderer + Animator)
```

Visual で回転を行うと、**Visual の transform 行列は `T_v × R_v × S_v`**、それに親 `T_p × R_p × S_p` (R_p = identity) が合成される。スプライト pixel P の世界座標は:

```
P_world = T_p + S_p × T_v + (S_p × R_v × S_v) × P
```

ここで合成行列 `S_p × R_v × S_v` は、`S_p`, `S_v` が **非一様 diagonal** であるため、`R_v(θ)` が回転行列でも **積は純粋な回転にならず shear (せん断) 成分が混ざる**。

例: `θ = 90°` で計算すると:
```
S_p × R_v(90°) × S_v
 = diag(0.8, 1.2) × [[0,-1],[1,0]] × diag(1.25, 0.83)
 = [[0, -0.667], [1.5, 0]]
```

これは「単位ベクトル (1, 0) を (0, 1.5) に、(0, 1) を (-0.667, 0) に」写像する変換で、純粋な 90° 回転 (それなら (0, 1) と (-1, 0)) ではない。結果、回転中の sprite は「縦に 1.5 倍、横に 0.667 倍に変形しながら回転」して見えていた。これが「比率が変わる」現象。

#### なぜ最初気付かなかったか

shear 量は 45°, 135° 等の中間角度で最大化され、0/90/180/270° では (たまたま) 比率が保たれる。Claude が最初に検証で出していた grid スクリーンショットでは、十字 (crosshair) の中心位置だけ目視確認しており、**「絵の縦横比」は気にしていなかった**。ユーザーは絵全体の歪みを観察していたので気付いた。

### 2.3 指摘 3: rotate が 0 に戻らない

#### 直接原因: rolling-end.anim の reset カーブが Animator playback で commit されない

Spinner 構造化後、Visual / Player 親由来の shear は消えた。回転終了時のリセット用に、`rolling-end.anim` に以下の reset カーブを置いた:

```
path: Spinner
  localEulerAnglesRaw.x = 0 (1 keyframe at t=0)
  localEulerAnglesRaw.y = 0
  localEulerAnglesRaw.z = 0
  m_LocalPosition.x = 0
  m_LocalPosition.y = 0
  m_LocalPosition.z = 0
```

**仮定**: Animator が `RollingEnd` ステートに入ると上記カーブを評価して Spinner.transform に書き戻す。

**実態 (play mode 観測)**:

```
at Loop mid: spinner.pos=(0.40, 0.87, 0.00) rot=(0.00, 0.00, 180.00)
step 3 state=End  pos=(0.40, 0.87, 0.00) rot=(0.00, 0.00, 180.00)
step 9 state=Idle pos=(0.40, 0.87, 0.00) rot=(0.00, 0.00, 180.00)
```

→ Animator は RollingEnd / Idle に遷移しているのに、**reset カーブが transform に commit されず、Spinner は Loop の最終値のまま固定**。

なお `clip.SampleAnimation(visualGo, 0f)` で単発でクリップを当てると、ちゃんと Spinner が (0, 0, 0) にリセットされる。**curve のデータ自体は正しい**が、Animator の state playback 経由では効かない。

#### なぜ最初気付かなかったか

Claude は reset カーブを置いた段階で「これで動く」と判断し、**実機 play で press → release の end-to-end フローを通した検証をしていなかった**。検証していたのは:

- `clip.SampleAnimation` 単発検証 (◯ pass) → これで「カーブは正しい」と確認したつもり
- 補正の数学検証 (◯ pass)
- pixel grid スクリーンショット (◯ pass)

これらは全て**「補正のロジック」**の検証であって、**「Animator が state 遷移後に curve を transform に書き戻すか」**の検証ではなかった。

#### 仮説 (深掘りせず): なぜ Animator が curve を commit しなかったか

確証は取っていないが:

1. **WriteDefaults + 単一キーフレーム curve の最適化**: 「変化のない curve」とみなして評価をスキップする可能性。これなら position も rotation も両方 commit されない事実と整合する (実測で両方戻っていない)。
2. **Animator のバインドキャッシュ**: クリップを編集して path を `""` → `"Spinner"` に migrate した後、Animator のキャッシュが古いバインド (= 存在しない `""` パスの SpriteRenderer) を握っている可能性。
3. **`localEulerAnglesRaw` vs `m_LocalRotation`**: Unity Animator は内部で rotation を quaternion で扱う。Euler curve はそれと干渉する可能性がある (ただし position も戻らないので、これだけでは説明できない)。

仮説 1 が最有力。

---

## 3. 是正措置

### 3.1 即時是正 (本セッション内で実施済み)

#### A. Trigger 化 (指摘 1 への対処)

`Player.controller` に `RollingTrigger` (Trigger 型) を追加。Any State → RollingStart の条件を `IsRolling=true` (Bool) から `RollingTrigger` (Trigger) に変更。

`PlayerAnimatorBridge.Update`:

```csharp
if (controller.IsRolling && !prevIsRolling)
{
    animator.SetTrigger(RollingTriggerHash);
}
prevIsRolling = controller.IsRolling;
```

→ 立ち上がりエッジでのみ発火。Loop 中の再発火が無くなり、状態が Loop に居座る。

#### B. Spinner 構造の追加 (指摘 2 への対処)

`Player.prefab` の階層を変更:

```
Player    (localScale = 0.8, 1.2)
  └─ Visual    (localScale = 1.25, 0.83)  ← 回転しない、scale 専任
     └─ Spinner   (localScale = 1, 1)     ← 回転 + 位置補正担当 (新設)
        └─ (SpriteRenderer)
```

`Visual.scale × Player.scale = (1, 1)` がちょうど打ち消しあい、**Spinner より上の合成スケールが単位行列**になる。Spinner で回転させても shear 成分が出ない (純粋な世界回転)。

実装変更:
- SpriteRenderer を Visual から Spinner へ移動
- 全 14 個の `.anim` クリップの PPtr m_Sprite curve のパスを `""` → `"Spinner"` に一括 migrate (script で機械的に実施)
- `PlayerAnimatorBridge` の補正対象を `transform` (Visual) → `spinner` に変更
- 不動点座標は Visual-local から sprite-local 直書きに変更 (`rollingEyeOffsetSprite = (0.20, 0.435)`)

#### C. 終了時リセットを script に寄せる (指摘 3 への対処)

`rolling-end.anim` の transform reset カーブが Animator playback で commit されない事象を確認。**Animator のカーブに頼らず、`PlayerAnimatorBridge.LateUpdate` が常に Spinner の transform を支配する方式**に変更:

```csharp
void LateUpdate()
{
    // RollingLoop じゃないなら 0 にスナップ
    bool inLoop = current.shortNameHash == RollingLoopStateHash;
    if (inLoop && animator.IsInTransition(0))
    {
        // Loop → End 抜け遷移中も Loop じゃないとみなす
        if (next.shortNameHash != RollingLoopStateHash) inLoop = false;
    }

    if (!inLoop)
    {
        spinner.localPosition = Vector3.zero;
        spinner.localEulerAngles = Vector3.zero;
        return;
    }
    // 以下、回転補正の適用
}
```

合わせて `rolling-end.anim` の transform 系 reset カーブを削除 (script が責任を持つので不要、むしろ衝突リスク)。sprite curve は残す。

検証:

```
at Loop mid: pos=(0.40, 0.87, 0.00) rot=(0.00, 0.00, 180.00)
step 0 state=Loop pos=(0.00, 0.00, 0.00) rot=(0.00, 0.00, 0.00)  ← 抜け遷移開始即リセット
step 9 state=Idle pos=(0.00, 0.00, 0.00) rot=(0.00, 0.00, 0.00)
```

→ 抜け遷移が始まった瞬間に Spinner が (0, 0, 0) に戻り、End / Idle と進んでも 0 で維持される。

### 3.2 メモリへの追記

- **`feedback_animator_e2e_verify.md`** (feedback): アニメ・遷移系の修正は単体検証だけで完了報告しない。play mode で press → release の end-to-end 観測まで通すこと。

`MEMORY.md` のインデックスにも追加。

---

## 4. 教訓

### 4.1 「実装が意図通り動く」と「end-to-end の挙動が正しい」は別

今回 3 段階で同じ失敗を繰り返した:

| 段階 | Claude の検証 | 実機での結果 | 根本 |
|---|---|---|---|
| 指摘 1 | 数学 OK / sprite grid OK | 目が暴れる | Animator state が往復していて補正が安定しない |
| 指摘 2 | 数学 OK / 不動点固定 OK | 比率が変わる | shear に気付いていない |
| 指摘 3 | 単発 SampleAnimation で 0 リセット OK | 実機ではリセット効かず | Animator playback の curve commit 不確実 |

共通パターン: **「ロジックの単体検証」で OK が出た時点で完了報告**してしまい、**実機で end-to-end のフローを最後まで通す**ことを怠った。

ロジック単体検証は必要だが**十分ではない**。アニメ・状態遷移・transform 連携が絡む箇所では、play mode で「ユーザーが実際に行う操作 (= press / release / 状態遷移トリガー)」をなぞって、**transform を時系列で観測する**まで含めて完了とする。

### 4.2 Animator のカーブに頼らない方が事故が少ない

`rolling-end.anim` の reset カーブは「設定としては正しい」が、Animator の評価フロー (WriteDefaults + state playback + transition の組合せ) では確実に commit されないケースがあった。原因の深掘りは時間の都合で割愛したが、**「カーブを置けば確実に効く」という前提に立たない**のが教訓。

確実性を優先するなら、**script の LateUpdate で transform を直接支配**する方が制御しやすい。今回の最終構成 (script ownership) は副次的にこの教訓を体現している。

### 4.3 ユーザーの観察は数値検証より優先する

指摘 2 (shear) は、Claude が出していた数値レポートでは「目の中心は不動点に乗っている」と検証 OK だった。だがユーザーは絵全体の歪みを目視で観測していた。**「数値が見ているもの」と「視覚が見ているもの」の解像度が違う**ことに、Claude 側が気付けなかった。

CLAUDE.md にも「ユーザー観察 > Claude の数値検証」が書いてある (memory: feedback_verify_colliders.md とも整合) が、今回はその原則の発火が遅れた。「目の位置」だけを検証対象にしていて、「目の周辺の絵の比率」は検証対象に入っていなかった、という設計ミス。

### 4.4 非一様スケールに rotation を入れない

「回転は scale が単位行列のフレームで行う」を rule of thumb とすべき。`Player (0.8, 1.2)` のような非一様スケールが上にある中で `Visual` を回転させると、ユーザー体感には shear として現れる。`Spinner` 階層を 1 段挟むコストは小さい。

---

## 5. 副次的に実施したこと

### 5.1 全 .anim の path 一括 migrate

SpriteRenderer を Visual → Spinner へ移したのに合わせて、14 個の `.anim` クリップの PPtr m_Sprite curve のパスを `""` → `"Spinner"` に変更した。`AnimationUtility.GetObjectReferenceCurveBindings` でクリップ全件を走査し、対象のバインディングだけ rebind するスクリプトで一括処理。

### 5.2 不動点の empirical refinement

最初は連結成分 bbox 中心 (67, 43.5) を不動点にしていたが、実機レンダリング測定で実際の visual center が (67.67, 43.92) と判明し empirical に補正。Spinner 構造化でさらに sprite-local 座標 (0.20, 0.435) として記述方式を整理。

### 5.3 補正式の左右対応

facing left (flipX=true) では eye が x 方向に反転して描画されるので、補正の x 符号と回転方向の符号も反転する必要がある。**y 補正は左右で同じ式**になる (sin 項の符号反転と回転方向反転が打ち消しあう)。これは数学的に確認した上で `sign = flipX ? -1 : +1` という単一スイッチで両対応している。

---

## 6. ファイル変更一覧

### 修正対象 (本是正で実施)

#### 構造変更
- `Assets/ScrollAction/Prefabs/Player.prefab` — Visual の下に Spinner GameObject 追加、SpriteRenderer を移動
- `Assets/ScrollAction/Animations/*.anim` (14 ファイル) — PPtr m_Sprite path を `""` → `"Spinner"` に一括変更
- `Assets/ScrollAction/Animations/rolling-end.anim` — transform reset カーブを削除 (script が責任を持つようになったので不要)
- `Assets/ScrollAction/Animations/Player.controller` — `RollingTrigger` (Trigger) パラメータ追加、Any State → RollingStart の条件を Trigger に変更

#### スクリプト
- `Assets/ScrollAction/Scripts/Player/PlayerAnimatorBridge.cs` —
  - Spinner Transform 参照を保持
  - Update で IsRolling 立ち上がりを検出して SetTrigger
  - LateUpdate を「常に Spinner を支配」方式に変更 (RollingLoop 中は補正、それ以外は 0 にスナップ)

### 追加メモリ
- `feedback_animator_e2e_verify.md` (feedback) — play mode end-to-end 検証ルール

---

## 7. ユーザー対応の振り返り

### 7.1 「直りました」を 3 回連続で取り下げた

ユーザーから 3 回連続で「直っていない」と返された。これは Claude 側が単体検証で完了判定する癖が抜けていなかった証拠。memory に教訓化したので次セッションでは play mode end-to-end 観測を「完了の必要条件」として組み込む。

### 7.2 ユーザーの観察ポイントを過小評価しない

指摘 2 で「比率を変えるな」と言われたとき、最初「数値的には目の中心は固定されている」と返答してしまうところだった (実際は素直に shear の存在を認めて Spinner 構造化に進んだ)。**ユーザーが見ているもの (絵の全体的な歪み) と Claude が検証しているもの (1 点の座標固定) の差**を意識する。

### 7.3 ユーザーの仮説を検証して必要なら修正する

最後に「reset カーブを置いてもリセットされるのは transform だけで rotate はないってことですもんね」というユーザー仮説を受けたが、実測ログを見直すと **position も rotation も両方 reset されていなかった** ことが分かったので、礼儀正しく仮説を訂正した上で正しい説明 (両方 commit されない / 単一キーフレーム最適化が有力仮説) を返した。**ユーザーの仮説に同調するのではなく、データに基づいて訂正する**のが long-term の信頼につながる。

---

## 8. 関連ドキュメント

- 機能実装ドキュメント: `document/feature/20260505-200000-rolling-action.md` (本セッションの最終構成)
- 過去類似事例:
  - `document/correction/20260505-180000-warp-sprite-pivot-misalignment.md` (sprite pivot 不整合)
  - `document/correction/20260505-163000-sliding-sprite-overlap.md` (同上、初出)
- 関連メモリ:
  - `feedback_animator_e2e_verify.md` (本件で追加)
  - `feedback_verify_colliders.md` (見た目だけで完了判定しない)
  - `feedback_action_sprite_pivot.md` (sprite pivot 都度修正ルール)
