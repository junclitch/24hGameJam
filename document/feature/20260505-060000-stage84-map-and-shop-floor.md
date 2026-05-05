# マリオ8-4 風マップ構築 & Shop シーン適用

実装日: 2026-05-05
対象シーン:
- `Assets/Scenes/ScrollAction.unity`
- `Assets/Scenes/Shop.unity`

対象アセット:
- `Assets/ScrollAction/Sprites/ground.png` (sub-sprite を 1 つ追加)
- `Assets/ScrollAction/Sprites/start-goal.png` (Shop の絵柄として使用)
- `Assets/ScrollAction/Sprites/background.png` (Moon を Shop に複製)
- `Assets/ScrollAction/Prefabs/Marker.prefab` (GoalMarker として使用)

## 概要

ScrollAction シーンに **マリオ 8-4 を参考にした横スクロールマップ** (`Stage84_Map`) を構築し、その後 Shop シーンに同じ床構造・背景・新ショップ絵柄を適用した。

設計→実装→ユーザーフィードバック→修正のサイクルを高密度で繰り返し、最終的に床は `ground_50` ベース、孤立タイル (飛び石/橋) は `ground_51〜56` ランダムという視覚ルールに落ち着いた。途中 Unity 特有の落とし穴を 3 件踏み、それぞれ事後修正した。

---

## 成果物

### ScrollAction.unity (新規ヒエラルキー)

```
Stage84_Map (layer=Ground)               ← 新規。68→37 タイル(再構築で減量)
├── Section1_Floor (9 tiles, ground_50)  ← Rigidbody2D(Static) + CompositeCollider2D
├── Stones (2 tiles, ground_51〜56 ランダム)
├── Section2_Floor (6 tiles, ground_50)  ← Composite
├── WallPillar (1 tile, ground_0)
├── Platform (橋, 2 tiles ground_51〜56)  ← Composite
├── Section3_Floor (14 tiles, ground_50) ← Composite
└── Stair (3 tiles, ground_50)           ← Composite (3 polygon: 段差ゆえ)
GoalMarker (Marker.prefab instance, TextMesh "GOAL")
Ground (既存)                              ← active=false に
```

### Shop.unity (新規ヒエラルキー)

```
Shop_Floor                                ← 新規。15 タイル
└── Floor_0..14 (ground_50)              ← Rigidbody2D(Static) + CompositeCollider2D
BackgroundRoot                            ← 新規 (ScrollAction から複製)
└── Moon (sprite=bg_04, ParallaxFollow scrollFactor=0.95)
Shop                                      ← sprite を start-goal_6 に差替
                                            BoxCollider2D 削除 → PolygonCollider2D(isTrigger)
Ground (既存)                              ← active=false に
```

### アセット改変

| パス | 変更 |
|---|---|
| `ground.png` (spritesheet) | sub-sprite **`ground_122_stair`** (rect 440,585,108×124, pivot 0,0) を追加。後に階段で `ground_50` を採用したため未使用に |

### スクリーンショット (検証用)

`Assets/Screenshots/stage84_v1〜v12_*.png`、`shop_v1〜v3_*.png` (12 + 3 枚)。Unity MCP の screenshot ツールが `Assets/Screenshots/` 固定で出力するため Assets 配下に残存。

---

## 設計: 座標と寸法

### タイル基準
- 床面トップ Y = `-1.5` (元の `Ground` GameObject の上端と一致、Player 既存配置を変えないため)
- 床タイル: `ground_50` を **幅=2.0 / 高さ=1.486 unit** に強制スケール (`scale_x = 2.0/native_w`, `scale_y = 1.486/native_h`)
- 床タイルピボットは中心 (重要、後述の罠を参照) → `transform.position.y = -1.5 - 0.743 = -2.243`
- 壁/ピラー (`ground_0`): scale 5.5 で 1.485×1.43 unit
- 階段 (`ground_50`, scale 0.849×1.100): 1.485×1.43 unit

### Stage84_Map の配置 (タイル中心座標、unit)

| セクション | X 配置 | 用途 |
|---|---|---|
| Section1_Floor | -26..-10 step 2 (9枚) | スタート地帯 |
| Stones | -6, -2 (2枚) | 落下穴1の飛び石 |
| Section2_Floor | 2..12 step 2 (6枚) | 中盤 |
| WallPillar | 8 (床上) | ジャンプ障害物 |
| Platform (橋) | 16, 18 (2枚) | 落下穴2の浮島 |
| Section3_Floor | 22..48 step 2 (14枚) | ゴール手前 |
| Stair | 44, 45.485, 46.97 (右上方向に1段ずつ積む) | ゴール階段 |
| GoalMarker | (47.7, 3.79) | TextMesh "GOAL" |

KillZone は既存 (y=-10, scale (200,2)) をそのまま流用。落下死 → 始点リスポーンは `KillZone.cs` 既存の挙動。

### Shop_Floor の配置

中心 X = -14, -12, ..., 14 (15枚)、Y = -2.243 で 30 unit 幅。元 `Ground` (scale 30) と完全に同じ範囲を覆う。

---

## 実装の進化

### v1〜v3: 基本構成の試行錯誤
- v1: `ground_64` で構築 → R 字状の小さな装飾だった (床素材ではなかった)
- v2: 列ごとの不透明率を測って solid 候補を発掘 (`ground_0` 69%, `ground_129` 71%, `ground_50` 93%, `ground_199〜205` 94%)
- v3: `ground_50` 採用。175×130px → scale 1.143 で 2×1.486 unit に。タイルサイズも 1unit→2unit グリッドに変更しレイアウト全面再計算

### v4〜v5: バリエーションのルール化
- v4: 全床タイルを `ground_50〜56` ランダム化 → 孤立タイルだけでなく連続床も乱雑になった
- v5: ユーザー指示で **基本=`ground_50` 固定 / 単発=`ground_51〜56` ランダム** に整理

### v6: 高さの統一
- アクセントスプライトはアスペクト比が違うため、scale_x/scale_y を独立計算して **2.0×1.486 unit に強制統一**。アスペクト最大 25% 歪むが高さは揃う

### v8〜v10: 階段スプライトの試行
- v8: `ground_120` (アスペクト独自スケール)
- v9: `ground_122` 採用 → 右ブロックを除外したいというユーザー要望 → `SpriteDataProviderFactories` で sub-sprite `ground_122_stair` (rect 440,585,108×124) を ground.png に追加し、`flipX=true` で反映
- v10: 結局 `ground_50` に戻す。step ごと scale 0.849×1.100 で 1.485×1.43 unit

### v11: ピボット問題の修正 (重要)
- 全タイルの transform.y がピボット=左下前提で計算されていたが実際は **中心ピボット**
- 床面トップが y=-2.24 になっていて Player と 0.74 unit ズレ
- 全 38 タイルの y 座標を再計算: 床 y=-2.243, 壁 y=-0.785, 階段 y=-0.785/0.645/2.075, GoalMarker y=3.79

### v12: BoxCollider2D refit (もう 1 つの重要)
- 階段 3 段の `BC2D.size` が `(0.27, 0.26)` のまま。これは初期作成時の `ground_0` の native size。スプライトを差し替えても BC2D は **auto-refit されない**
- scale 0.85×1.10 と掛けて world size 0.23×0.29 unit のほぼ正方形 → 「絵柄の隅に乗ってる小さな当たり判定」状態だった
- 全 37 タイルを点検して 3 件の不一致を `bc.size = sprite.bounds.size` / `bc.offset = sprite.bounds.center` で修正

### Shop シーン適用
- 床: ScrollAction と同じ手法で 15 タイルの `Shop_Floor` 構築 (BC2D は明示的にサイズ指定、auto-fit 信用しない)
- Shop 絵柄: `start-goal_6` (上段右端の店舗、200×225) → `BoxCollider2D` 削除 → `PolygonCollider2D` 追加で sprite outline に自動フィット (5 path / 75 point)
- ユーザーが「物理衝突は不要、UI 起動の検知だけ」と指示 → `polygon.isTrigger = true`
- 背景: `BackgroundRoot/Moon` (bg_04, ParallaxFollow scrollFactor=0.95) を ScrollAction から複製

---

## ハマりポイント (この後の作業者向け)

### 1. ピボットは中心が多い (ground.png / start-goal.png)

```yaml
spriteMode: 2
alignment: 0      # = Center
spritePivot: {x: 0.5, y: 0.5}
```

`Sprite.pivot` が pixel 単位で `(width/2, height/2)` を返す。これは **GameObject.transform.position が sprite center を指す** ことを意味する。

つまり `transform.position = (x, y)` の sprite は world `(x - half_w, y - half_h)〜(x + half_w, y + half_h)` を占める。タイル底辺基準で計算したつもりになっていると **全タイルが半分の高さ分ズレる**。

**対策**: 配置式は必ず

```csharp
float yCenter = FLOOR_TOP - (TILE_H * 0.5f);  // 床面トップから半分下
tile.position = new Vector3(x, yCenter, 0);
```

### 2. BoxCollider2D は sprite 差し替えで refit されない

`sr.sprite = newSprite;` しても `BoxCollider2D.size` は変わらない。最初の `AddComponent<BoxCollider2D>()` で auto-fit された値で固定される。

特に「同じ世界サイズに見えるよう scale も合わせて変えた」場合、絵は正しく差し替わって見えるが当たり判定だけ古いサイズのまま、という事故が起きる。

**対策**: スプライト差し替えとセットで必ず

```csharp
bc.size = sr.sprite.bounds.size;
bc.offset = sr.sprite.bounds.center;
```

を実行する。`Stage84_Map` の最終 refit ではこれを全タイルにループ適用するヘルパを書いた。

### 3. 連続タイルの「内部エッジ」でプレイヤーが詰まる

複数の `BoxCollider2D` を並べた場合、隣接タイルの境界でプレイヤーのコライダーが引っかかる (Unity の Physics2D が境界を別々の壁として処理するため)。

**対策**: `CompositeCollider2D` で結合する。

```
親 GameObject:
  Rigidbody2D (BodyType=Static, simulated=true)
  CompositeCollider2D (geometryType=Polygons, generationType=Synchronous)
子タイル:
  BoxCollider2D (usedByComposite=true)
```

`cc.GenerateGeometry()` を呼ぶと隣接コライダーが 1 ポリゴンに統合される。Section1_Floor 9 タイル → 1 path、点 4 個になる。

階段のように **タイル同士が角でしか接していない** 場合は別ポリゴンのままになるが、ジャンプで登る前提なら問題ない。

### 4. Play モード中の `execute_code` でシーン変更すると Stop で消える

Play モード中に `new GameObject()` などで scene root に追加したオブジェクトは、Stop した瞬間にすべて破棄される。

**対策**: スクリプト先頭で

```csharp
if (UnityEngine.Application.isPlaying) return "play mode, abort";
```

をガードする習慣を付ける。一度これに引っかかって 68 タイル全て作り直しになった。

### 5. spritesheet の sub-sprite を増やすには `UnityEditor.U2D.Sprites.SpriteDataProviderFactories`

レガシー API の `TextureImporter.spritesheet = new SpriteMetaData[]{...}` は **Unity 6 で実体に反映されない**。新 API は

```csharp
var factories = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
factories.Init();
var dp = factories.GetSpriteEditorDataProviderFromObject(importer);
dp.InitSpriteEditorDataProvider();
var rects = new List<UnityEditor.SpriteRect>(dp.GetSpriteRects());
rects.Add(new UnityEditor.SpriteRect {
    name = "...", rect = ..., pivot = ..., alignment = SpriteAlignment.BottomLeft,
    spriteID = UnityEditor.GUID.Generate()
});
dp.SetSpriteRects(rects.ToArray());
dp.Apply();
AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
```

なお `SpriteRect` は `UnityEditor.SpriteRect` (`Unity.2D.Sprite.Editor` アセンブリ)。`UnityEditor.U2D.Sprites.SpriteRect` ではない。

---

## 反省

ピボット問題と BoxCollider2D refit 問題を **見た目スクショだけで完了判定**して進めたためユーザーに 2 度指摘された。GameView 画像は SpriteRenderer の出力しか映さず、コライダー位置・サイズの破綻は見えない。

次回以降は

- スプライト/トランスフォーム変更後は `SR.bounds` と `BC.bounds` を数値比較する
- Scene View で gizmo オンのスクショを撮る
- 「変更して見た目が正しい」≠「コライダーが正しい」を意識する

を徹底する。

---

## 関連ファイル

- スクリプト変更: なし (既存 `KillZone.cs`, `Shop.cs`, `ParallaxFollow.cs` に依存)
- シーン変更: ScrollAction.unity, Shop.unity
- アセット変更: ground.png (sub-sprite +1)
