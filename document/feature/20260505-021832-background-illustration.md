# 背景イラスト読み込みと配置 実装まとめ

実装日: 2026-05-05
対象シーン: `Assets/Scenes/ScrollAction.unity`
素材: `Assets/ScrollAction/Sprites/map.png` (1536×1024, 工場/廃墟/枯木のシルエット集)

## 概要

`map.png` は「前景 (FOREGROUND)」「地面 (GROUND)」「背景 (BACKGROUND)」の 3 区分が 1 枚に詰まった素材集で、各区分にカテゴリラベルの日本語テキストも一緒に印字されている。これを Unity 側で `Multiple` モードでスライスし、

- 透視カメラの **Z 深度を使った 3 レイヤー (遠景/中景/近景)** + 空 + 地面 + 前景プロップ
- レイヤーごとに **ランダム配置** + **明度を段階的に**変えて空気遠近感

を作るのが本作業の目的。最終的に 75 個の背景 GO + 21 個の地面タイル + 14 個の前景プロップが配置される構成になった。

## 成果物

### 1. スプライトシートの準備

`Assets/ScrollAction/Sprites/map.png` のインポート設定:

| 項目 | 値 |
|---|---|
| Texture Type | Sprite (2D and UI) |
| Sprite Mode | Multiple |
| Pixels Per Unit | 100 |
| Filter Mode | Point (no filter) |
| Compression | Uncompressed |
| Read/Write | Enabled (スライス処理に必要) |
| Pivot | Bottom-Center (足元基準で配置するため) |

スライスは `UnityEditorInternal.InternalSpriteUtility.GenerateAutomaticSpriteRectangles` を `minRectSize=16, extrudeSize=0` で呼び、自動検出 → 各矩形の Y 座標で 3 区分に振り分けて `map_bg_01..28` / `map_ground_01..16` / `map_fg_01..28` の 72 サブスプライトに命名。

```
y < 410         → map_bg_xx     (背景)
410 ≤ y < 720   → map_ground_xx (地面)
y ≥ 720         → map_fg_xx     (前景)
```

### 2. シーン階層

```
ScrollAction.unity (root)
├── Main Camera                 (z=-10, 透視 FOV60)
├── Directional Light
├── Ground                      (物理: BoxCollider2D 60×1, 既存)
├── Player                      (既存)
├── KillZone                    (既存)
├── MarkersRoot                 (既存)
├── BackgroundRoot              (新規, 75 children)
│   ├── Sky                     (1200×200u, sortingOrder -50, z=35)
│   ├── HorizonSmoke            (1200×6u, sortingOrder -45, z=28)
│   ├── Far_xx (×24)            (sortingOrder -40, z=20)
│   ├── Mid_xx (×30)            (sortingOrder -30, z=12)
│   └── Near_xx (×17)           (sortingOrder -20, z=5)
├── GroundDecoRoot              (新規, 21 children)
│   └── GroundTile_-10..10      (map_ground_10 を横並び, sortingOrder -1)
└── ForegroundRoot              (新規, 14 children)
    ├── Prop_xx (×11)           (sortingOrder 0, z=0, シルエット小物)
    └── BigProp_xx (×3)         (sortingOrder -25, z=8, 大型クレーン)
```

### 3. レイヤー設計表

| レイヤー | sprite プール (例) | sortingOrder | z | scale 範囲 | y 範囲 | 色 (RGB) |
|---|---|---|---|---|---|---|
| Sky | (Square単色) | -50 | 35 | 1200×200 | 5 | (0.85, 0.86, 0.88) |
| HorizonSmoke | (Square単色) | -45 | 28 | 1200×6 | -1.0 | (0.72, 0.74, 0.78) |
| Far | bg_01,02,03,05,06,07,08,20,21,23,24,25,28 | -40 | 20 | 2.0〜3.0 | 0.2〜1.2 | (0.62, 0.66, 0.72) |
| Mid | bg_12,14,17,18,19,24,25,28,04,06 | -30 | 12 | 1.5〜2.4 | -0.4〜0.6 | (0.40, 0.44, 0.50) |
| Near | bg_09,10,11,13,15,16,22,26,27 | -20 | 5 | 0.9〜1.6 | -0.7〜-0.4 | (0.18, 0.20, 0.24) |
| GroundTile | ground_10 | -1 | 0.5 | (1.0) | -2.0 | (0.10, 0.11, 0.13) |
| Prop (前景小物) | fg_04,05,06,07,08,13,15,19,20,21,22 | 0 | 0 | 0.8〜1.0 | -1.5 | (0.05, 0.05, 0.08) |
| BigProp (前景大型) | fg_02,03 | -25 | 8 | 0.7〜0.8 | -1.5 | (0.22, 0.24, 0.28) |

> Player の sortingOrder=1 に対し、装飾は全て ≤ 0 に揃えてあるため、シルエットがどこに来てもプレイヤーが必ず最前面に描画される。

### 4. カメラ背景色

`Main Camera.backgroundColor` を曇天グレー `(0.78, 0.80, 0.83)` に変更。
ただし Sky GO が画面全体を完全に覆うため実際にこの色が画面に出ることはない (バックアップとしての役割)。

## 設計上のポイント

### A. 透視カメラ × Z 深度で「無料パララックス」

カメラは `orthographic=false`, `FOV=60`, `transform.z=-10` の透視。Z 軸でオブジェクトを離すと自動的に小さく描画されるので、

- 遠景 z=20 (距離 30) → 見かけサイズが 1/3
- 中景 z=12 → 約 1/2.2
- 近景 z=5 → 約 2/3
- プレイヤー z=0 → 等倍

スケール値で見た目サイズを補正する代わりに、Z を活用することで **カメラの横移動時に自動的に視差スクロール**が起きる。スクリプト不要。

### B. sortingOrder と Z の役割分離

- 描画順序は `sortingOrder` で完全に決める (sortingOrder が同値のときだけ Z 距離が補助)
- `Z` 値は描画順序のためではなく、**透視による見かけサイズと視差**だけのために使う
- これにより「Player を必ず最前面」「装飾 GO の上下関係を Z で並べたい」を両立できる

### C. 同じスプライトを連続使用しない選択ロジック

直前と異なるスプライトをプールから引くだけで、ループ感が大幅に減る:

```csharp
string lastFar = "";
while (xCursor < endX) {
    string name;
    do { name = farPool[rng.Next(farPool.Length)]; } while (name == lastFar);
    lastFar = name;
    // ... 配置 ...
}
```

これに **scale を毎回ランダム化** + **Y を ±0.5 程度ジッタ** + **flipX をランダム** を加えると、3 種程度のスプライトでも繰り返し感は気にならなくなる。

### D. 隙間を入れて空が見える疎配置

`xCursor += w + Rand(2, 6)` のように **配置幅 w にランダムなギャップを足す**ことで、シルエット間に空が抜ける。

- 「ぎっしり並ぶ工業地帯」は迫力はあるが圧迫感が出る
- 隙間があるほうが空気感・寂寥感が出る (本素材のテーマと合致)

### E. 自動スライスは bounding-box ベース

`InternalSpriteUtility.GenerateAutomaticSpriteRectangles` は、不透明ピクセルの **連結成分 + bounding box** を返す。`minRectSize` 内のギャップは同じ矩形に統合される。`map.png` ではこの仕様が裏目に出て、

- 地面ストリップ (y=686-687, 1510×2px) と
- すぐ下に印字されたラベル「地面 (GROUND)」(y=633-672)

が **同じ rect (13, 633, 1510, 55) として 1 枚のスプライト** にまとめられた。下記「嵌ったこと」参照。

## 嵌った / 学んだこと

### 1. 背景の使い回しが目立つ問題 → 配置ロジックで解決

最初は `for (int i; ...) sprites[i % len]` のような剰余ループで配置していて、同じパターンが等間隔で出現するため遠目にもループが見えた。

**改善: ランダム選択 + 直前回避 + ギャップ + scale/Y/flip ジッタ。**
プールサイズが小さくても、4 軸の揺らぎを掛け合わせると体感の繰り返し感は消える。

### 2. グレー背景は「見える範囲」を広く

ユーザフィードバックで「グレーの背景はもっと広範囲のほうがいい」。
最初 Sky を 800×80u にしていたが、下記 2 点で調整:

- **シーンを跨ぐカメラ移動でも切れない**ように 1200×200u まで拡大
- **シルエットを疎にする**ことで Sky が画面に出る面積を物理的に増やす

「壁紙を広げる」だけでなく「前景を抜く」のも空を見せる手段。

### 3. スプライトシートに混入するカテゴリラベル

`map_ground_01` (1510×55px) として自動検出された矩形は、ピクセル単位で見ると、

```
y=633-672 : ラベル「地面 (GROUND)」テキスト (~134-184 不透明px)
y=673-685 : 完全透明 (空白)
y=686-687 : 1510px 全幅の実際の地面ストリップ
```

の 3 区間で構成されていた。`minRectSize=16` のため空白 13px が「ノイズ」として無視され、ラベルとストリップが 1 枚のスプライトに統合されてしまっていた。

最初これを地面ラインの装飾としてシーンに敷き詰めた結果、画面全体に「地面 (GROUND)」「背景 (BACKGROUND)」のラベルが繰り返し表示される事故が発生。

**最終解: ラベル混入のない `map_ground_10` (実際の土塊テクスチャ部分) を採用**。これは ground 区分の中で唯一、独立した「地面ブロック」として描かれており、ラベル文字が混じっていなかった。

ピクセル走査で各候補を検証してから採用するのが確実 (`Texture2D.GetPixel` で y 行ごとの不透明ピクセル数を集計する手法を使った)。

### 4. アクティブシーンが意図せず切り替わる事故

作業途中、`MCP` で操作していたシーンが `ScrollAction` から `Shop` に切り替わっており、`BackgroundRoot` を Shop シーン側に作ってしまっていた。

**学び: 大量配置や破壊操作を行う前には必ず `manage_scene get_active` で確認する**。あるいは `manage_scene load` を冒頭で明示的に呼ぶ。Shop 側に作った GO は `DestroyImmediate` で消去 → ScrollAction を再ロードして配置し直し。

### 5. `transform.localScale` が SpriteRenderer.drawMode 切替で書き変わる

`SpriteRenderer.drawMode` を `Tiled` にして `size` を設定すると、内部的に `transform.localScale` が触られるらしく、`Simple` に戻したあとも `localScale = (238.41, 2.18, 1)` のような半端な値が残った。

**学び: drawMode を切り替えたあとは明示的に `localScale = Vector3.one` でリセット**。今回は地面の物理 GO で `scale=(60,1,1)` を維持したかったので、Tiled ではなく **複数 GO を並べる** 方式 (GroundTile_-10..10) に切り替えた。

### 6. CodeDom コンパイラ (C# 6) ではローカル関数とタプル不可

`mcp__UnityMCP__execute_code` は既定で CodeDom (C# 6) コンパイラを使う。Roslyn が無いと、

- **ローカル関数** (`void Foo() {}` を関数の中で定義) → 不可
- **タプル** `(string, float, bool)` → 不可
- **`dynamic`** → 不可

回避策として `System.Func<...>` のラムダ + `(string n, float x) p` の **field tuples は OK だが ValueTuple 推論は怪しい** ので、配列＋名前付きクラスで持つのが無難。今回は `Func<...>` で十分回せた。

## 既知の追加ToDo (将来)

- **真のパララックススクロール**: Z 深度差で「ある程度」視差は出るが、より誇張したパララックスにするなら `BackgroundLayer` 系のスクリプトでカメラ x に係数 (0.3, 0.5, 0.8 など) を掛けて追従させる方が表現の自由度が高い
- **背景の生成範囲**: 現状 x=-90〜+90 に固定生成。もし Stage が長くなる場合は **カメラ追従で動的にスポーン/デスポーン**する仕組みが必要
- **Foreground を Player より前面に出す表現**: 現状はすべて Player より背面。手前を Player が通り抜けるようなプロップ (例: 工場の柵) を表現したいなら sortingOrder ≥ 2 で別レイヤーを作る
- **Shop シーンへの転用**: 同じ素材を Shop シーンに適用してもいいが、Shop は店内の屋内シーンなので別素材を用意する方が自然
- **MEMORY/CLAUDE.md への記載**: 「`map.png` のラベル混入問題」と「ラベル混入確認の `GetPixel` ピクセル走査ワザ」は再発防止のため共有資産化する価値あり (今回はドキュメント記載のみ)
