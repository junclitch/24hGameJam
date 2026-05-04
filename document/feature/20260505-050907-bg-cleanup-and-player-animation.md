# 背景マテリアル整理 & プレイヤーアニメーション実装

実装日: 2026-05-05
対象シーン: `Assets/Scenes/ScrollAction.unity`
対象アセット:
- `Assets/ScrollAction/Sprites/background.png` (アルファ透過済みに置換された前提)
- `Assets/ScrollAction/Sprites/base-action.png` (アイドル/歩き/ジャンプ/しゃがみの素材集)
- `Assets/ScrollAction/Animations/*.anim`, `Assets/ScrollAction/Animations/Player.controller`

## 概要

本セッションは大きく 2 部構成。

1. 前回 (`20260505-045000-additive-blend-background.md`) で構築した加算合成パイプラインの破棄。`background.png` に正規のアルファチャンネルが入ったため、加算合成・α=輝度焼き込み・専用マテリアル・パララックス追従の存在意義が消えた。残骸の削除と、それに付随して発生した Moon の表示崩れの修復。
2. `base-action.png` (4種アニメ素材集) からプレイヤーの待機/歩行/ジャンプアニメーションを再構築。Unity の自動スライスがラベル文字や番号も含めて 174 個に細切れになっていたため、ピクセル走査ベースの独自スライサーで 4 行 × N 列に整理し直し、既存の AnimationClip 4 本の sprite 参照を新スプライトに差し替え。

## 成果物

### 削除
```
Assets/ScrollAction/Materials/                  ← フォルダごと
Assets/ScrollAction/Materials/SpriteAdditive.mat
Assets/ScrollAction/Scripts/Camera/ParallaxFollow.cs (※後で再作成)
Assets/Screenshots/shot-additive-*.png 等 9 セット (検証用スクショ)
```

### 新規作成 / 再作成
```
Assets/ScrollAction/Scripts/Camera/ParallaxFollow.cs   ← 削除→復活 (Moon を残すために必要だった)
```

### 改変
```
Assets/ScrollAction/Sprites/base-action.png            ← 174 → 29 スプライトに再スライス
Assets/ScrollAction/Animations/idle.anim               ← base_idle_00..05 (6f, 6fps loop)
Assets/ScrollAction/Animations/walk.anim               ← base_walk_00..06 (7f, 16fps loop)
Assets/ScrollAction/Animations/jumpUp.anim             ← base_jump_00..03 (4f, 10fps)
Assets/ScrollAction/Animations/jumpDown.anim           ← base_jump_04, 05, 01 (3f, 10fps)
Assets/ScrollAction/Animations/Player.controller       ← Walk 状態に speedParameter=Speed を有効化
Assets/Scenes/ScrollAction.unity                       ← Moon マテリアル復旧, ParallaxFollow 再アタッチ, Visual.sprite=base_idle_00
```

---

## 第1部: 背景マテリアル整理

### A. 加算合成資産の削除判断

前回セッションは `background.png` が「黒地に白い月・煙・光が描かれた素材集」だった前提で組み立てたため、

- `_Surface=Transparent` + `_Blend=Additive` のマテリアル
- α=輝度の焼き込み (黒地を擬似透明化)
- 輝度ベースの BFS スライサー
- 月だけ控えめに追従させる `ParallaxFollow`

…の 4 点セットで構成されていた。

ユーザー側で `background.png` を **正規のアルファチャンネルを持つ透過 PNG に差し替え**たため、上記 4 点のうち最初の 3 つは前提ごと消滅。`scene` 内の参照を grep で確認したところ、

- `SpriteAdditive.mat` の guid (`a08d554b...`) → 全シーン/プレハブで 0 件
- `ParallaxFollow.cs` の guid (`d9358ef3...`) → 同 0 件

の状態で、すでに孤児化していた。これらを安全に削除。

### B. 削除に伴う Moon の magenta 表示

**現象**: シーンファイル上は参照ゼロだったにもかかわらず、Unity Editor のメモリ上で開かれていた `BackgroundRoot/Moon` が pink (magenta) で描画されるようになった。

**原因の分解**:
1. `SpriteRenderer.sharedMaterial` が **null** (元は削除した `SpriteAdditive.mat` を参照していた)
2. コンポーネント一覧に **`<missing>`** が 1 件 (削除した `ParallaxFollow` の残骸)

**SpriteRenderer + null マテリアルの罠**: 通常 `sharedMaterial = null` の SpriteRenderer は組込みの "Sprites-Default" にフォールバックして無事に描画される。しかし **URP プロジェクトでは fallback が効かず**、shader が見つからない時の magenta が出る。`Resources.GetBuiltinResource<Material>("Sprites-Default.mat")` を試したがこれも null を返した。

**対処**: `AssetDatabase.FindAssets("Sprite-Unlit-Default t:Material")` で URP 同梱の標準スプライトマテリアルを検索 (`Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat`) し、これを `sharedMaterial` に割り当て。

**Missing script の除去**: `UnityEditor.GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go)` で 1 件削除。

```csharp
var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(
    "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
sr.sharedMaterial = mat;
UnityEditor.GameObjectUtility.RemoveMonoBehavioursWithMissingScript(moon);
```

### C. ParallaxFollow.cs の復活

「Moon が画面右上に常に見え、プレイヤーの位置に応じて少しだけ位置が変わる」機能が消えていた、というユーザー指摘により、削除した `ParallaxFollow.cs` を再作成。

実装は前回と同形:

```csharp
// LateUpdate 内
Vector3 camDelta = cameraTransform.position - baseCameraPos;
Vector3 next = baseWorldPos
    + new Vector3(offset.x, offset.y, 0f)
    + new Vector3(camDelta.x, camDelta.y, 0f) * scrollFactor;
next.z = transform.position.z;
transform.position = next;
```

`scrollFactor` の意味:

| 値 | 振る舞い |
|---|---|
| 0.0 | カメラ無視 (ワールド固定) |
| 0.95 | わずかに動く (今回採用) |
| 1.0 | カメラと完全同期 |

Moon 用に `scrollFactor=0.95` を採用。プロジェクト規約 (`unity-coding-conventions.md`) に従い、コード上にデフォルト値は埋め込まず `[SerializeField]` の構造定義のみ。

---

## 第2部: プレイヤーアニメーション

### A. 既存セットアップの調査

シーンを覗いたところ、すでに最低限の足回りはあった:

```
Player (Rigidbody2D, BoxCollider2D, PlayerController)
├── GroundCheck
└── Visual (SpriteRenderer, Animator, PlayerAnimatorBridge)
```

`PlayerAnimatorBridge` は `PlayerController.IsGrounded` と `Rigidbody2D.linearVelocity` を読んで `Animator` に `Speed / VerticalSpeed / IsGrounded` を流すだけの薄いブリッジ。`flipX` も x 速度の符号でこちらが面倒見ている。

`Player.controller` のステート: `Idle / Walk / Dash / JumpUp / JumpDown / LadderUp / LadderDown`。各クリップは別の素材集 (`20260504Test/action.png`) を参照していた。

今回の作業は **`base-action.png` を新しい素材源として、idle / walk / jumpUp / jumpDown の 4 クリップだけ差し替える** という方針。Dash と Ladder 系はスコープ外。

### B. base-action.png の再スライス

#### 自動スライスの破綻

`base-action.png` (1536×1024) は 4 行レイアウトの素材集:

```
[アイドル(待機)] 01 02 03 04 05 06
[歩き         ] 01 02 03 04 05 06 07
[ジャンプ     ] 01 02 03 04 05 06 07 08
[しゃがみ     ] 01 02 03 04 05 06 07 08
```

各セルにキャラクタのシルエット (黒) + 数字 (黒) + 行頭にラベル (黒文字)。
Unity の自動スライスはアルファ境界しか見ないため、文字も数字もキャラクタも区別なくバウンディングボックス化した結果、**174 個の細切れスプライト** が生成され、ラベル断片や数字 1 文字も `base-action_NN` として混入。アニメに使えない。

#### 独自ピクセル走査スライサー

「4 行の Y バンドはハードコードする / 各バンド内では暗ピクセルの X 方向クラスタを検出する」という方針で再スライサーを書いた。

```csharp
// 4 行の texture-Y (下から上) バンド
int[][] bands = {
    { 820, 1014 },   // idle
    { 580,  745 },   // walk
    { 280,  540 },   // jump   (ジャンプ弧で背の高さが変わるため広め)
    {  20,  260 },   // crouch
};
int xMin = 180, xMax = 1300;  // 左ラベル列・右仕様欄を除外

foreach (band in bands) {
    // X ごとに band 内の暗ピクセル数を計上
    // しきい値で連続区間 (=キャラ幅) を切り出す
    // gapTolerance=6 で白縁の細い隙間を1キャラ扱い
    // minClusterWidth=40, charH>=100 でラベル断片・数字を排除
    // 各キャラ矩形に pad=4px 付与し pivot=(0.5, 0) (BottomCenter)
}
```

結果:

| 行 | クラスタ数 | 命名 |
|---|---|---|
| アイドル | 6 | `base_idle_00..05` |
| 歩き | 7 | `base_walk_00..06` |
| ジャンプ | 8 | `base_jump_00..07` |
| しゃがみ | 8 | `base_crouch_00..07` |

ジャンプ列の検出高さ: 127 → 163 → 195 → 221 → 213 → 166 → 132 → 143 (中盤で背が高い = 滞空ピーク)。前半 4 コマを上昇、後半 4 コマを下降と分割。

#### スライス保存の手順

`spriteImportMode = Multiple` と `isReadable = true` を確実に立てるため、

```
1. importer.spriteImportMode = Multiple
2. importer.isReadable = true
3. AssetDatabase.ImportAsset(path)        ← Multiple モード適用
4. importer.spritesheet = newRects
5. EditorUtility.SetDirty(importer)
6. importer.SaveAndReimport()             ← 矩形反映
```

の順で実施。前回ドキュメントの「2 回 SaveAndReimport」教訓通り。

### C. AnimationClip の参照差し替え

既存 4 本のクリップに対し、`AnimationUtility.SetObjectReferenceCurve` で `SpriteRenderer.m_Sprite` のキーを総入れ替え。

```csharp
var binding = new EditorCurveBinding {
    type = typeof(SpriteRenderer), path = "", propertyName = "m_Sprite"
};
var keys = sprites.Select((s, i) => new ObjectReferenceKeyframe { time = i / fps, value = s }).ToArray();
AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
```

ループ可否は `AnimationClipSettings.loopTime` で別途制御 (`AnimationUtility.GetAnimationClipSettings/SetAnimationClipSettings`)。

#### 最終的なフレーム配列

| クリップ | フレーム | fps | length | loop |
|---|---|---|---|---|
| `idle.anim` | `base_idle_00..05` | 6 | 1.000s | true |
| `walk.anim` | `base_walk_00..06` | 16 | 0.4375s | true |
| `jumpUp.anim` | `base_jump_00, 01, 02, 03` | 10 | 0.4s | false |
| `jumpDown.anim` | `base_jump_04, 05, 01` | 10 | 0.3s | false |

`jumpDown` のユーザー調整経緯: 当初 `04, 05, 06, 07` だったが、

1. 「最後から2つめのコマ (`06`) を消して」 → `04, 05, 07`
2. 「最後のコマ (`07`) もいらない」 → `04, 05`
3. 「`jumpUp` の2個目のコマ (`01`) を最後に入れて」 → `04, 05, 01`

着地直前で `jumpUp_01` (浅くしゃがんだ姿) を再利用することで、「足が地面に触れるタイミングのアンチシペーション」を表現する形に落ち着いた。

### D. 歩行アニメの "カクカク" 問題

ユーザー指摘: walk 中の見た目がカクついて滑って見える。

**診断**: 12fps × 7 コマ = 1 サイクル 583ms。プレイヤー走行速度 (PlayerStats.walkSpeed=5u/s) と歩幅が噛み合わず、キャラが足の運びと無関係に "走らされている" 感が出ていた。

**対処2段構え**:

1. **fps を 12 → 16 に引き上げ**。1 サイクル 583ms → 437ms に短縮し、視覚的に "コマが見える" 印象を緩和。
2. **AnimatorState の speedParameter を有効化**:

```csharp
walkState.speed = 1f / walkSpeed;     // = 1/5 = 0.2 (基準速度の逆数)
walkState.speedParameterActive = true;
walkState.speedParameter = "Speed";   // PlayerAnimatorBridge が毎フレーム書き込む
```

これで再生速度は `state.speed × Speed = 0.2 × |vx|` となり、

| プレイヤー速度 | 再生倍率 | 1 サイクル長 |
|---|---|---|
| 0u/s | 0× (停止) | (Idle 状態に遷移するので関係なし) |
| 5u/s (= walkSpeed) | 1.0× | 437ms |
| 10u/s | 2.0× | 219ms |

…と、**歩幅 (sprite の足の運び) と移動量が常に同期**する。ダッシュ/減速時も連動するため、加減速が滑らかに見えるようになった。

> 重要: `state.speed` を `1 / walkSpeed` に設定する理由は、「Bridge から流れる Speed パラメータがそのまま倍率として使われる」ように正規化するため。`state.speed = 1` のままにすると `walkSpeed=5` の時に 5 倍速で再生されてしまう。

### E. Crouch は未統合

`base_crouch_00..07` のスプライトは生成済みだが、`Player.controller` に Crouch ステートを追加していない (PlayerController 側にもしゃがみ入力ロジックなし)。ニーズが出たら別途。

---

## 嵌った / 学んだこと

### 1. URP の SpriteRenderer は null マテリアル fallback が無い

ビルトインパイプラインでは `SpriteRenderer.sharedMaterial = null` だと `Sprites-Default.mat` に自動 fallback して描画される。しかし URP では fallback が効かず magenta になる。

- `Resources.GetBuiltinResource<Material>("Sprites-Default.mat")` も null を返す (URP 環境)
- 解決: `Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat` を明示的にロードして割り当てる

### 2. ピンクの原因は2系統あり得る

「pink/magenta のスプライト」を見たら、可能性は:

- a. SpriteRenderer の sharedMaterial が **欠落 / null** (URP)
- b. マテリアルは存在するが **shader が欠落** (シェーダの guid が壊れた)
- c. その他: スプライト本体が missing

`MonoBehaviour <missing>` がコンポーネント一覧にあるなら (a) もしくは (b) を疑う。`GetComponents<Component>()` を回して `c == null` チェックすれば missing script は判別できる。

### 3. 自動スライサーは「キャラ + 数字 + 文字ラベル」に弱い

白背景に黒コンテンツが混在する素材集では、`SpriteImportMode.Multiple` の `Automatic` スライスは数字や文字も独立スプライト化してしまう。本作品の `base-action.png` で 174 個に細切れになった原因。

回避策:
- グリッドベースのスライス (セルサイズが既知ならこれが一番速い)
- ピクセル走査の独自スライサー (今回採用、行ごとの Y バンドハードコード + X クラスタ検出)
- 素材生成時にラベル/数字を別レイヤーにし、エクスポート時に外す

### 4. アニメーションの "カクカク" は fps だけでは直らない

fps を上げてもプレイヤー速度との比率が崩れていれば「滑る」「足が泳ぐ」感は残る。Animator のステートには `speedParameter` という機能があり、毎フレーム動的に再生速度を変えられる。

- `state.speed`: 静的倍率 (基準値、`1/参照速度` を入れると Speed が ratio になる)
- `state.speedParameter`: Animator パラメータ名 (毎フレーム評価される動的倍率)
- 実効再生速度 = `state.speed * paramValue`

### 5. CodeDom は local function 不可

`mcp__UnityMCP__execute_code` のデフォルト (CodeDom, C# 6) では method 内の local function (`string GetPath(GameObject g) {...}`) はパースエラーになる。inline 展開するか `delegate` 値で書く。

```csharp
// NG (CodeDom)
void Foo() { void Bar() {...} }

// OK
System.Action bar = delegate () {...};
```

### 6. シーン側の暗化調整は加算合成前提だった

前回セッションで Camera bgColor=暗紺 / Ground=ほぼ黒 / HorizonSmoke 削除 を行ったのは「加算合成の光を映えさせるために下地を暗くする」目的。今回その加算合成は捨てたので、`background.png` (透過素材) との相性は別途確認すべき。今は手付かず。

---

## 将来 ToDo

- **Crouch の統合**: `base_crouch_00..07` を使った `crouch.anim` 作成 + Animator に `Crouch` ステート追加 + 入力 (`Ctrl` か下方向) のハンドリング
- **Dash / Ladder 系の素材源**: 現状は `20260504Test/action.png` を参照しており、`base-action.png` には対応素材がない。デザインが揃ったら同じ手順で差し替え
- **歩行 fps の最終調整**: 16fps + Speed 連動で改善はしたものの、もう一段なめらかにしたい場合は fps=20、または `state.speed` の基準値を `1 / (walkSpeed × 0.8)` にして「実速度より控えめに」回す手もある
- **背景の最終トーン調整**: 加算合成前提で行った Camera/Ground 暗化を、透過 `background.png` 環境に合わせて調整するかどうか
- **Moon の Bloom 検討**: 加算合成を捨てたため Moon は単純な Sprite 描画。グロウ感が欲しければ URP の Bloom + HDR color (`SpriteRenderer.color = (1.5, 1.5, 1.5)`) で代替可
- **`base-action.png` の他キャラ展開**: 同じスライサーが他のキャラ素材集 (`add-action.png`, `add2-action.png`) にも転用できるよう、再利用可能なエディタ拡張化を検討
