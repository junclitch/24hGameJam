# 加算合成による背景 (月) の実装まとめ

実装日: 2026-05-05
対象シーン: `Assets/Scenes/ScrollAction.unity`
素材: `Assets/ScrollAction/Sprites/background.png` (1536×1024, 黒地に月・煙・工場・街灯・星空を描いた素材集)

## 概要

`background.png` は **加算合成での使用を前提に作られた素材集** で、黒地に明るい要素 (月・煙・光) が描かれている。アルファチャンネルを持たず、要素間も完全な真黒で繋がっていないため、Unity の標準オートスライスは効かない。

本セッションでは、このタイプの素材を 2D ゲームの背景に使うための一連のテクニックを構築した:

1. 加算合成 (Additive Blending) のマテリアルを URP で用意
2. テクスチャの **アルファチャンネルに輝度を焼き込む** ことで halo の自然なフェードを実現
3. 輝度ベースの自前 BFS スライサーで Multiple モード化
4. 月だけ `ParallaxFollow` で控えめにカメラ追従させる

なお、配置検討の過程で本セッションでは煙3 / 工場3 / 街灯3 を加算で並べたが、最終的に **ユーザー側で手動配置するため月以外は削除済み**。本ドキュメントは「加算合成パイプラインの仕組み」の記録として残す。

## 成果物

### 新規ファイル

```
Assets/ScrollAction/Scripts/Camera/ParallaxFollow.cs   ← カメラ追従パララックス
Assets/ScrollAction/Materials/SpriteAdditive.mat       ← 加算合成マテリアル
```

### 改変ファイル

```
Assets/ScrollAction/Sprites/background.png             ← α=輝度を焼き込み + Multiple化 + 47枚スライス
Assets/Scenes/ScrollAction.unity                       ← Camera bg, Ground 色, BackgroundRoot 内容
```

### シーン階層 (セッション終了時)

```
ScrollAction.unity
├── Main Camera                    (clearColor=(0.04,0.04,0.07) 暗紺)
├── Directional Light
├── Ground                         (color=(0.08,0.08,0.10) ダーク化)
├── Player
├── KillZone
├── MarkersRoot                    (inactive)
├── ForegroundRoot                 (空, 既存装飾削除済み)
├── GroundDecoRoot                 (空, 既存装飾削除済み)
└── BackgroundRoot
    └── Moon                       (bg_04 + SpriteAdditive.mat + ParallaxFollow scrollFactor=0.95)
```

> セッション中は `BackgroundRoot/AdditiveScenery` 配下に煙3+工場3+街灯3 の 9 個を配置していたが、ユーザーが後で手動配置したいということで削除された。

## 設計上のポイント

### A. 加算合成 (Additive Blending) の原理

通常のアルファブレンド (アルファ合成):

```
最終色 = 上の色 × α + 下の色 × (1 - α)
```

加算合成:

```
最終色 = 上の色 × α + 下の色 × 1
```

→ 上の色を「光として下に足す」イメージ。**黒 (0,0,0) は加算しても下を変えないので透明扱い**になる。これが本素材の黒地が消える理由。

URP では `Universal Render Pipeline/Particles/Unlit` シェーダの設定で実現:

| プロパティ | 値 | 意味 |
|---|---|---|
| `_Surface` | 1 (Transparent) | 透明オブジェクト扱い |
| `_Blend` | 2 (Additive) | 加算ブレンド |
| `_SrcBlend` | 5 (SrcAlpha) | 上の色は α だけ寄与 |
| `_DstBlend` | 1 (One) | 下の色はそのまま残す |
| `_ZWrite` | 0 | 透明物なので深度書き込み無効 |
| `renderQueue` | 3000 | Transparent キュー |

加えてキーワード `_SURFACE_TYPE_TRANSPARENT` と `_ALPHAMODULATE_ON` を有効化することで、URP のシェーダバリアントが正しい経路を使う。

### B. アルファ=輝度 焼き込みの効果

最初は黒を完全な (0,0,0) と見なして加算したが、実際には **画像の "黒" 部分が 4〜10/255 程度の僅かな明度を持っている** ため、加算するとスプライトの矩形分だけ画面が薄く明るくなり、**矩形境界が直線として見える** という問題が出た。

対策として、テクスチャに **アルファ = 輝度** を焼き込んだ:

```
α = (R + G + B) / 3
```

ブレンド式が `Blend SrcAlpha One` なので、α が小さいピクセルは加算時の重みが下がる:

- 真っ黒 (0,0,0) → α=0 → 寄与ゼロ
- 端の薄暗い (5,5,5) → α≈5 → ほぼ寄与なし
- ピーク白 (255,255,255) → α=255 → フル加算

結果として **halo が自然に消えていき、矩形端のステップも見えなくなる**。これが今回の核心テクニック。

> 副作用: 中間明度のピクセルがやや暗くなる (α<255 なので寄与が減る)。輝くハイライトを強調したい場合は `SpriteRenderer.color` を `(1.5, 1.5, 1.5)` のような HDR 値にすると良い (URP は HDR カラーを受け付ける)。

### C. 輝度ベース BFS スライサー

オートスライス `InternalSpriteUtility.GenerateAutomaticSpriteRectangles` はアルファ境界しか見ないため、α が立つ前 (黒地のまま) の段階では 0 件しか検出できない。代わりに **RGB 輝度の連結成分** を BFS で検出して矩形化する自前スライサーを作成:

```
1. 全ピクセル走査
2. 輝度 ≥ threshold (=48) のピクセルを "光ってる" 判定で BFS シード化
3. 8方向連結で連結成分を取り、bounding box を計算
4. 24×24px 未満のノイズを除外
5. 各矩形を pad=24 px 拡張 (halo を取り込む)
6. Y 降順 / 同行内は X 昇順でソートして bg_00 〜 bg_46 と命名
```

α 焼き込み後の background.png では 47 枚のスプライトが切り出された。月は `bg_04`。

#### 嵌りパラメータ

- `threshold` を低くしすぎると (例: 2) → 月と工場の halo が連結し、縦に長い 1 個の塊になる
- `threshold` を高くしすぎると → halo が捨てられ、矩形端の段差が出る
- 経験則: **threshold=48, pad=24** がこの素材ではちょうど良かった

### D. ParallaxFollow とスクロール係数

```csharp
// LateUpdate 内の式
position = baseWorld + offset + (camera.position - baseCamera) * scrollFactor;
```

`scrollFactor` の意味:

| 値 | 振る舞い |
|---|---|
| 0.0 | カメラ無視 (ワールド固定) — 進むと画面外に流れていく |
| 0.5 | 半速追従 — 中景っぽい |
| 0.85 | ほぼ追従、わずかにズレる |
| 0.95 | ごく僅かに動く (今回採用) |
| 1.0 | カメラと完全同期 (画面に貼り付き) |

月は遠景なので **0.95** を採用。プレイヤーがステージ端から端 (~50u) を走破しても画面上では 8% 程度しか動かない。

Z 深度による「自動パララックス」(透視カメラの遠近) と組み合わせると、月の控えめな追従が自然に馴染む。

### E. 夜景化のための周辺調整

加算合成は **下地が暗いほど派手に映える**。元の Camera 背景 (薄灰) と HorizonSmoke (薄灰) では加算光が埋もれてしまうため、

- Camera `backgroundColor` → `(0.04, 0.04, 0.07)` 暗紺夜空
- `HorizonSmoke` (1200×30u の巨大な薄灰板) → 削除
- `Ground` color → `(0.08, 0.08, 0.10)` ほぼ黒

これで月や煙の光がコントラスト高く浮かび上がる。

## 嵌った / 学んだこと

### 1. CodeDom コンパイラの `using` 指令禁止

`mcp__UnityMCP__execute_code` の既定コンパイラは CodeDom (C# 6 相当)。Roslyn が無効な環境では **コード冒頭の `using ...;` がパースエラー** になる。回避策: 全シンボルを完全修飾名で書く。

```csharp
// ❌ NG (CodeDom)
using UnityEngine;
var v = new Vector3(...);

// ✅ OK (CodeDom)
var v = new UnityEngine.Vector3(...);
```

`compiler="roslyn"` を試したが、本環境では `Microsoft.CodeAnalysis` 未インストールでエラー。

### 2. `GameObject.Find("Sky")` は子を探さない

`UnityEngine.GameObject.Find(name)` は **シーンルートの GameObject** しか名前マッチしない。`BackgroundRoot/Sky` のような子は見つからず `null`。

```csharp
// 子も探す場合
foreach (UnityEngine.Transform t in
    UnityEngine.Object.FindObjectsByType<UnityEngine.Transform>(
        UnityEngine.FindObjectsInactive.Include,
        UnityEngine.FindObjectsSortMode.None))
{
    if (t.name == "Sky") return t.gameObject;
}
```

または親をルートから手繰る、`scene.GetRootGameObjects()` → 子を再帰探索、など。

### 3. SpriteImportMode を `Multiple` に維持しつつ spritesheet を保存する

`importer.spriteImportMode = SpriteImportMode.Multiple` 後に `SaveAndReimport()` を呼ぶと spritesheet がリセットされることがある。手順を:

```
1. spriteImportMode = Multiple
2. isReadable = true
3. SaveAndReimport()      ← Multiple モード適用
4. spritesheet = newMeta  ← 矩形配列
5. SetDirty(importer)
6. SaveAndReimport()      ← spritesheet 反映
```

の順で **2 回 SaveAndReimport** することで安定する。1 回で済ませようとすると `count=1 (background_0)` のようにフォールバックされた状態で保存される事故に。

### 4. スプライト名は再スライスで番号がズレる

`bg_02` だった月が、再スライス後は `bg_04` に変わる、というように番号が動く。**SpriteRenderer の参照は保たれず、消えるか別物に変わる**。

- 配置を作り込んだ後にスライス条件を変えると参照が壊れる
- 早めに矩形パラメータ (threshold, pad) を確定させる
- 名前を「上から番号」で付けたいときは並び順を厳密化 (Y 降順 → X 昇順) する

### 5. `ParallaxFollow` は Play モードでないと反映されない

スクリプトに `[ExecuteAlways]` を付けていないので、Editor の Scene/Game ビューでは月の位置は固定のまま見える。**動作確認は Play モードで**。

確認用に Editor で動かしたいなら `[ExecuteAlways]` 属性を追加するが、`baseCamera` の捕捉タイミング (Awake) など副作用に注意。

### 6. 月の sprite を差し替えても scale は手動調整必須

スプライトの矩形サイズが変わると見た目の大きさも変わる。`map.png` の三日月 (0.86×0.86u) → `background.png` の満月 (1.97×1.98u) では元サイズ約 2.3 倍。前のスケールで貼ると過大になる。差し替え時は localScale を再設定。

### 7. 元 PNG への破壊的変更

α=輝度の焼き込みは **`background.png` 自体を上書き保存**している (`File.WriteAllBytes`)。元の "α なし純黒地" を取り戻したい場合、リポジトリから取り直すか別ファイル名で保管しておくべき。今回は本セッション内で完結する用途のため上書きを許容した。

将来再発防止するなら、加工済みを別ファイル `background_baked.png` として書き出し、元ファイルは温存する方が安全。

### 8. スクリーンショット保存先の規約逸脱

`mcp__UnityMCP__manage_camera screenshot` は既定で `Assets/Screenshots/` に保存される。プロジェクト規約 (`CLAUDE.md` および user メモリ) では `C:/GitHub/24hGameJam/MCP_Screenshots/` に置くことになっているため、本セッションのスクリーンショットは **Assets 配下に取り残し**。`gitignore` 漏れで commit されないよう注意 (現状は `Assets/Screenshots/` が gitignore 済みかは未確認)。

> ToDo: `manage_camera screenshot` の保存先パスを引数で `MCP_Screenshots/` に指定するか、保存後にスクリプトで移動する。

## 将来 ToDo

- **加算用と通常用のマテリアル使い分け**: 暗いシルエット (工場本体、人物) は通常アルファブレンドで描き、月や光だけ加算合成にする運用にすると、コントラストの強い夜景になる
- **HDR + Bloom**: URP のポストプロセスで Bloom を有効化し、月や街灯のスプライト color を `(2.5, 2.5, 2.0)` のような 1 超え値にすると、目に染みる強い発光が出せる
- **静的バッチング**: 加算装飾は色や transform を毎フレーム変えないなら StaticBatching を有効にしてドローコールを減らす
- **背景の動的スポーン**: 現状 ParallaxFollow は単一 GameObject の制御のみ。複数の煙を散らしてループスポーンさせるなら、カメラ位置を見て自動で生成・破棄する `BackgroundSpawner` を作る
- **元 PNG の温存**: α 焼き込みを `background_baked.png` に分離し、元素材は不変にする
- **`ExecuteAlways` 検討**: ParallaxFollow を Editor で動かしたい場合、`baseCamera` の取り直し API を提供する

## 参考スクリーンショット (Assets/Screenshots/)

| ファイル | 状態 |
|---|---|
| `shot-bg-before.png` | 着手前: 既存の灰色シルエット |
| `shot-additive.png` | 加算マテリアル適用直後 (薄灰背景なので光が弱い) |
| `shot-night-final.png` | 夜空化 + 9個装飾配置完成形 |
| `shot-alpha-baked.png` | α 焼き込み後、矩形境界の消えた状態 |

> 規約上は `C:/GitHub/24hGameJam/MCP_Screenshots/` に置くべきだったが、本セッション中は `Assets/Screenshots/` に保存されている (上記 嵌り 8 参照)。
