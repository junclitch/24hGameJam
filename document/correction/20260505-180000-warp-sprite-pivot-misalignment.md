# 是正報告書: ワープアニメーションの地面めり込み (再発)

報告日: 2026-05-05
対象: `Assets/ScrollAction/` (ワープアクション実装)
対象セッション: 2026-05-05 17:00 〜 18:00 頃
先行類似事例: [スライディング pivot 不整合](20260505-163000-sliding-sprite-overlap.md)

---

## 1. 事象

「ワープ」アクションの追加実装直後、ユーザーから次の指摘を受けた:

> スライディングと同様、アニメーションが地面にめりこんでいますよ？

ワープ用スプライト (`add-action.png` 4 行目: `_234`, `_235`, `_236`, `_245`, `_246`, `_247`) を 6 フレームの `warp.anim` に組み込んで実装したが、再生時にキャラクターの下半身が地面より下に描画されていた。

最も深刻なのは、**スライディング実装時 (16:30 セッション) に同じ系統の罠で 1 度躓いており、再発を防ぐルールも `feedback_verify_colliders.md` に追記済みだったにもかかわらず再発した点**。

---

## 2. 真の原因

### 2.1 直接原因: pivot.y(rel) の不整合 (再発)

`add-action.png` 内の warp フレーム 6 枚は **alignment=Center (0)** のままで、`pivot.y(rel)=0.5` (= 絵中央) になっていた。

実測値:

```
warp idx=234  pivot=(40.00, 62.50)  bounds.center.y=0.000  bounds.min.y=-0.625
warp idx=235  pivot=(43.50, 62.00)  bounds.center.y=0.000  bounds.min.y=-0.620
warp idx=236  pivot=(50.50, 63.50)  bounds.center.y=0.000  bounds.min.y=-0.635
warp idx=245  pivot=(38.50, 60.50)  bounds.center.y=0.000  bounds.min.y=-0.605
warp idx=246  pivot=(37.50, 48.00)  bounds.center.y=0.000  bounds.min.y=-0.480
warp idx=247  pivot=(31.00, 58.50)  bounds.center.y=0.000  bounds.min.y=-0.585

idle  base_idle_00  pivot=(31.50, 0.00)  bounds.min.y=0  ← 期待値
```

`Visual.localPosition.y = -0.5` で Player の足元 (BoxCollider2D 底) に Visual GameObject が置かれている設計のため、本来は `bounds.min.y == 0` (= スプライト下端 = 足元) でなければならない。warp 側は `bounds.min.y ≈ -0.6` で 60cm 沈み込んでいた。

| スプライト | pivot.y(rel) | bounds.min.y | 結果 |
|---|---|---|---|
| `idle / walk` 系 (`action.png`) | 0 (絵下端中央) | 0 | 接地 ✓ |
| `sliding` (`add-action_156`, 修正後) | 0 | 0 | 接地 ✓ |
| `glider` (`add-action_84-93`) | 0.5 (絵中央) | 約 -0.4〜-0.5 | 沈み込み (空中専用なので顕在化せず) |
| `warp` (`add-action_234-247`, 修正前) | 0.5 (絵中央) | -0.48〜-0.635 | **地面にめり込み** |

### 2.2 間接原因: スライディング修正時に範囲を 1 枚に限定した

スライディング是正セッション (16:30) では `add-action_156` の 1 枚だけ `alignment: 7 (BottomCenter), pivot: {0.5, 0}` に修正した。**同じシート内の他のキャラフレーム (`_84-93` glider, `_234-247` warp 候補) の pivot 設定は手付かず**。

このとき「シート全体の規約整合」という観点を持っておらず、「アクションごとに必要な分だけ直せばよい」という判断のまま放置していた。Glider はそもそも空中で発動するため地面接地比較が起こらず問題が表面化していなかったので、シート全体の問題があることに気付くトリガーが無かった。

### 2.3 根本原因: 教訓を「対象スプライト 1 枚に対する処理」と狭く解釈した

スライディング是正後にメモリへ追記した「`feedback_verify_colliders.md` の追加の罠」項は、内容としては `pivot.y(rel)` 検証の必要性を述べていたが、**「次に新規スプライトを採用するときも事前検証する」というアクション化が抽象的だった**。

具体的には:

- ❌ 「スプライト/Transform を変更したら検証」(現行ルール) → 今回 add-action.png のメタは触っていないので発火しない
- ⭕ 「**新しい sprite を anim に組み込む前に** pivot.y(rel) を比較」 (要追加ルール) → これがあれば warp 6 枚を anim に入れる前に検出できた

---

## 3. 是正措置

### 3.1 即時是正 (本セッション内で実施済み)

**A. warp 6 フレームの pivot を絵下端中央へ変更**

`add-action.png.meta` を編集 (対象: `_234`, `_235`, `_236`, `_245`, `_246`, `_247` の 6 ブロック):

```yaml
# 変更前
      alignment: 0
      pivot: {x: 0, y: 0}

# 変更後
      alignment: 7           # BottomCenter
      pivot: {x: 0.5, y: 0}  # 絵下端中央
```

**B. 数値検証**

ImportAsset(ForceUpdate) 後に Unity API で確認:

```
WARP add-action_234 pivot=(40.00, 0.00) bounds.center.y=0.625 bounds.min.y=0
WARP add-action_235 pivot=(43.50, 0.00) bounds.center.y=0.620 bounds.min.y=0
WARP add-action_236 pivot=(50.50, 0.00) bounds.center.y=0.635 bounds.min.y=0
WARP add-action_245 pivot=(38.50, 0.00) bounds.center.y=0.605 bounds.min.y=0
WARP add-action_246 pivot=(37.50, 0.00) bounds.center.y=0.480 bounds.min.y=0
WARP add-action_247 pivot=(31.00, 0.00) bounds.center.y=0.585 bounds.min.y=0
```

全 6 枚で `bounds.min.y == 0` を確認。idle 系と一致。

### 3.2 運用方針の決定 (ユーザー合意)

「規約自体は変えない (現行 BottomCenter 規約のまま)。`add-action.png` / `add2-action.png` の側を都度直す」方針 (= 方針 A) をユーザー確認:

> A でいく
> 今後、add2-action.png でアクションを作っていく予定

`add-action.png` のキャラフレーム全体を一括修正する案 (方針 B) はコスト/便益の判断で採用しない。

### 3.3 メモリへの追記

新規メモリを 2 件追加:

- **`feedback_action_sprite_pivot.md`** (feedback): add-action.png / add2-action.png から使うキャラフレームは sprite slice 後に `alignment: 7 / pivot: {0.5, 0}` に都度直す。`Sprite.bounds.min.y == 0` で数値検証してから anim に組む
- **`project_future_actions_spritesheet.md`** (project): 今後の新規アクションは `add2-action.png` から作る (`add-action.png` は 4 行 = Jetpack/Glider/Sliding/Warp で使い切り)

`MEMORY.md` のインデックスにも両者を追加。

### 3.4 検証チェックリスト (新規アクション追加時)

スライディング是正報告書に書いた既存チェックリストに、本件の教訓から **「シート全体ではなく今回採用する分だけでよい」** と **「sprite 採用前に pivot 検証」** を明示する形へ改訂:

1. **使用予定の sprite を特定** — anim に入れるフレーム idx をリストアップ
2. **採用前に pivot.y(rel) を確認** — `Sprite.pivot.y / Sprite.rect.height` を Unity API で取得
3. **既存 idle/walk 系と pivot.y(rel) が一致するか確認** — 違えば meta を編集 (`alignment: 7`, `pivot: {0.5, 0}`)
4. **ImportAsset(ForceUpdate) 後に `bounds.min.y == 0` を再測定**
5. **anim 組み込み + Animator Controller 配線**
6. **完了報告に「pivot.y(rel)=0 を確認」を 1 行記載**

---

## 4. 副次的に発見/対処したこと

### 4.1 AnimatorController の fileID で Int64 オーバーフロー

Player.controller に Warp state を追加するとき、当初 `fileID: 9999999999999991001` (19 桁) を使ったが、これが **Int64.MaxValue (9223372036854775807) を超えていた**。Unity 側で次のエラーが出てパース失敗:

```
Could not extract 'FileID' at "9999999999999991001"
```

`7654321987654321001` (Int64 範囲内) に置き換えて解消。既存の sliding (`1234567812345671001`) や glider (`8888888888888881001`) は範囲内だったため気付きにくかった。

**教訓**: 既存の fileID パターンを真似るときは、桁数だけでなく値域 (Int64) も意識する。

### 4.2 Shop.prefab catalog のエントリ表示確認

最初、reimport 直後に Shop.catalog の Warp エントリが NULL 表示されたが、これは上記 4.1 のエラーで AnimatorController のロードが部分失敗していた影響ではなく、Unity 内部キャッシュの遅延だった。`AssetDatabase.ImportAsset(..., ForceUpdate)` を明示呼び出しで解消。

---

## 5. 教訓

### 5.1 「教訓のアクション化」を抽象→具体まで落とす

スライディング是正で `feedback_verify_colliders.md` に追記した内容は「pivot は検証対象」と原則を述べていたが、**「いつ・どの瞬間に・何をする」までのアクション化が薄かった**。今回新たに追加した `feedback_action_sprite_pivot.md` では:

- いつ: 「add-action.png / add2-action.png から sprite を anim に組み込むとき」
- 何を: 「対象スライスの alignment を 7、pivot を {0.5, 0} に直す」
- 確認: 「`bounds.min.y == 0` を Unity API で計測」

まで落として記録した。

### 5.2 「同じシートの未使用 slice にも同じ問題がある」と疑う

スライディング是正で `_156` 1 枚を直したとき、**同じ `add-action.png` 内の他のキャラ slice (glider 行・warp 行候補) も同じ alignment=Center である**ことに気付くべきだった。シート単位の規約不整合という認識が無かったため、「直したのは 1 枚」で済ませた。

今後 add2-action.png に着手する際も、シート初参照の段階で「他のスライスも同じ罠を抱えている可能性」を念頭に置く。

### 5.3 「方針の選択肢を提示してユーザーに任せる」

今回 pivot 不整合を検出した後、Claude 側で勝手に「シート全体一括修正」を決定せず、ユーザーに「都度 / シート内一括 / 全 slice 一括」の 3 案を提示して選んでもらった。結果、ユーザーが選んだのは方針 A (都度) で、これは Claude が単独判断していたら選ばなかった可能性が高い (一括修正で再発防止する方を選びがち)。**コスト・便益の判断は規模感・将来計画を握っているユーザーに任せる**のが正解。

---

## 6. 関連ファイル

### 修正対象 (本是正で実施)
- `Assets/ScrollAction/Sprites/add-action.png.meta` — `_234`, `_235`, `_236`, `_245`, `_246`, `_247` の alignment / pivot

### 追加メモリ
- `feedback_action_sprite_pivot.md` (feedback) — add-action 系シートの pivot 都度修正ルール
- `project_future_actions_spritesheet.md` (project) — 今後は add2-action.png から作る

### 関連メモリ
- `feedback_verify_colliders.md` — 過去のしゃがみ・スライディング pivot 経験の蓄積 (本件で再発)

### 先行ドキュメント
- 是正報告書: `document/correction/20260505-163000-sliding-sprite-overlap.md` (今回再発の元になった事例)
- しゃがみ実装: `document/correction/20260505-114000-crouch-feet-floating.md` (pivot 問題の初出)

---

## 7. ユーザー対応の振り返り

- ユーザーから「スライディングと同様、めり込んでいる」と指摘を受けた時点で **即座に「sliding と同じ pivot 不整合だ」と推測でき**、原因特定に至るまでが速かった (前回経験の活用ができた)
- 一方、**実装中の事前検証 (anim 組込前の pivot 確認) は怠った**ため、結果としてユーザー指摘がなければ release されてしまうところだった
- 是正後、規約変更の要否を Claude が単独判断せず**方針選択をユーザーに渡した**点はよかった (規模感・将来計画の知識はユーザーが持っている)
- メモリ追記時、抽象的な原則ではなく**「いつ・何を・どう確認するか」まで落として書いた**ので、次回の発火確度が上がるはず
