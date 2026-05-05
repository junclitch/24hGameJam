# 古いブランチを main にマージした事故の修復まとめ

実施日: 2026-05-05 (22:00 頃)
対象: `feature/AddWallKick` を main にマージした PR #14 の事後修復

## 概要

`feature/AddWallKick` が main から数コミット遅れた状態でマージしたため、コンフリクトを手動解消する過程と、解消後にも気づきにくい形で 3 種の事故が混在した。Unity を起動した時点では `"AnyState -> Warp" transitions to state 'Warp' which does not exist in controller"` というエラー 1 件しか表面化していなかったが、調べると実害がある故障が他に 2 件あった。本ドキュメントは原因と修復手順を残し、今後同様のマージ時に何を見るべきかを共有する。

直接の発端は「main では Warp / Rolling アクションが先に追加されて Player.controller・Shop.prefab・Player Visual ヒエラルキーが拡張されていた」のに対し、ブランチ側はそれを知らずに古い構造で WallKick を追加していたこと。

## 事故 A: Player.controller の `m_ChildStates` の YAML 破損

### 症状
Unity Console:
```
Asset 'Player': Transition 'AnyState -> Warp' in state 'AnyState' transitions to state 'Warp' which does not exist in controller.
```

### 原因
コンフリクト解消時に、`m_ChildStates` リスト内で **Warp エントリの先頭区切り行 (`- serializedVersion: 1`) が抜け落ち**、直前の WallKick エントリと一塊の dict になっていた。

破損後の YAML（before）:
```yaml
  - serializedVersion: 1
    m_State: {fileID: 7834108500174439788}   # WallKick
    m_Position: {x: 550, y: 650, z: 0}
    m_State: {fileID: 7654321987654321001}   # Warp（区切り行が消えている）
    m_Position: {x: 550, y: 650, z: 0}
```

YAML として重複キーになり、Unity 側では Warp ステートが `m_ChildStates` 未登録扱いになる。`AnyState → Warp` の遷移は遷移オブジェクトとして残っているので、「存在しない state へ遷移している」というエラーになった。

### 修復
区切り行を補い、視覚的に重ならない座標に分けた。

```yaml
  - serializedVersion: 1
    m_State: {fileID: 7834108500174439788}
    m_Position: {x: 550, y: 650, z: 0}
  - serializedVersion: 1
    m_State: {fileID: 7654321987654321001}
    m_Position: {x: 700, y: 650, z: 0}
```

Reimport 後にエラー消滅。

## 事故 B: wallKick.anim の sprite curve が古い GameObject パスを指していた

### 症状
壁キック発動時にアニメ自体は再生されるが、画面に出るのは **Player のデフォルト sprite (idle で最後にセットされた絵)** で、wall-jump_02〜11 が表示されない。

### 原因
Rolling PR (main 側) で Player の Visual ヒエラルキーが
```
Player → Visual (Animator)
```
から
```
Player → Visual (Animator) → Spinner (SpriteRenderer)
```
に変わり、SpriteRenderer が孫オブジェクト Spinner に移動していた。

正常な `sliding.anim` / `rolling-loop.anim` は `path: Spinner` を持っているのに対し、`wallKick.anim` は **古い構造前提のまま path が空 (= Animator と同じ GameObject = Visual)** だった。Visual には SpriteRenderer がないので curve が誰にも届かず、Spinner の SpriteRenderer は idle 終了時の sprite を保持し続けた。これが「デフォルト Player 画像が出る」の正体。

| anim | path (curve target) | path hash |
|---|---|---|
| `wallKick.anim`（破損時） | （空 = Visual 自体） | `0` |
| `sliding.anim`（正常参考） | `Spinner` | `794877249` |
| `rolling-loop.anim`（正常参考） | `Spinner` | `794877249` |

### 修復
`wallKick.anim` の curve ターゲットを 2 か所書き換え:

1. `m_PPtrCurves[].path: ` → `Spinner`
2. `m_ClipBindingConstant.genericBindings[].path: 0` → `794877249`

### 学び
**他者がヒエラルキーを変更した期間にまたがるブランチをマージしたとき、自分の anim クリップの `path` が古いままになっていないか必ず確認する**。Animator パラメータと state 配置だけ確認しても気づけない。比較対象に同じターゲットを編集する「動いている既存 anim」を 1 本選んで `path` を grep するのが速い。

## 事故 C: Shop.unity のプレハブオーバーライドが古い catalog 長前提のまま残っていた

### 症状
Shop に WallKick は出るが、**Warp と Rolling が並ばない**。

### 原因
ブランチを切った当時の構成:
- `Shop.prefab` の `catalog` は **8 個** (HM, GC, Jump, GE, AE, Jetpack, Glider, Sliding)
- WallKick を Shop に並べるため、`Shop.unity` 上で次の prefab override を作っていた:
  ```
  catalog.Array.size       = 9
  catalog.Array.data[8]    = WallKick
  ```

その後 main 側で Warp と Rolling が prefab に追加され、`catalog` が **10 個** に成長:
```
[0] HM [1] GC [2] Jump [3] GE [4] AE [5] Jetpack [6] Glider [7] Sliding [8] Warp [9] Rolling
```

scene の override (`size=9`, `data[8]=WallKick`) はそのまま残っていたので、ランタイムでは:
- size override で 9 個に切り詰め → **Rolling が消える**
- `data[8]` の override で Warp が WallKick に置換 → **Warp が消える**

結果、ランタイムの catalog は `[HM, GC, Jump, GE, AE, Jetpack, Glider, Sliding, WallKick]` の 9 個になっていた。

### 修復
1. `Shop.prefab` の `catalog` 末尾に `WallKick.asset` を追加（11 個に）
   - MCP for Unity の `manage_prefabs.modify_contents` で `catalog` を全 11 件の guid 配列で上書き
2. `Shop.unity` の Shop オブジェクトの `catalog` プロパティ override を **完全に Revert**
   - `PrefabUtility.RevertPropertyOverride` を `execute_code` で呼び出し
   - これで `Shop.unity` から `catalog.Array.size` と `catalog.Array.data[8]` の両 override が消滅

ランタイム catalog（修復後・11 個）:
```
[0] HorizontalMove   [4] AirEvasion       [8] Warp
[1] GroundCheck      [5] Jetpack          [9] Rolling
[2] Jump             [6] Glider           [10] WallKick
[3] GroundEvasion    [7] Sliding
```

prefab を真の source of truth にしたので、今後アクションを追加するときは prefab の catalog にだけ append すれば良い。

### 学び
**シーン側の prefab override は「prefab がその後も同じ shape のまま」を暗黙の前提にしている**。catalog のような長さが伸びる配列に対して `Array.size` と `data[N]` の override をかけてしまうと、prefab が拡張されたとき override が黙って prefab の末尾要素を上書き／切り捨てる。

この種のオーバーライドは将来事故になりやすいので、**「シーン固有の都合がない限り、override ではなく prefab 側に直接追加する」を原則とする**。Shop の catalog のように共通的な定義は特に。

## マージ事故を早期発見するためのチェックリスト

main 取り込み直後 (Unity 起動前 or 起動直後) に以下を順に見る:

1. **Console を一通り読む** — `does not exist in controller` 系のエラーは AnimatorController の YAML 破損のサイン
2. **コンフリクト解消した `.asset` / `.controller` / `.unity` / `.prefab` の `git diff` を見て、リスト系 (`m_ChildStates`, `catalog` 等) の `- serializedVersion:` 区切り行が正しい個数あるか目視**
3. **自分のブランチで作った anim クリップが、main 側のヒエラルキー変更に追従しているか**を、既存 anim の `path:` と grep 比較
4. **`.unity` の `m_Modifications` 内に、`Array.size` や `data[N]` の override がないか確認**。あれば、対応する prefab の同じ配列が伸びていないか確認
5. Unity 上で実機テスト前に、対応する Animator graph を開いて全 state ノードが見えること、各 transition が孤児になっていないことを目視

## 参考: 修正コミット候補

このセッションで触った差分:
- `24hGameJam/Assets/ScrollAction/Animations/Player.controller` (m_ChildStates 区切り行修復)
- `24hGameJam/Assets/ScrollAction/Animations/wallKick.anim` (sprite curve target を Spinner に修正)
- `24hGameJam/Assets/ScrollAction/Prefabs/Shop.prefab` (catalog 末尾に WallKick 追加)
- `24hGameJam/Assets/Scenes/Shop.unity` (Shop 配下の catalog override を全 revert)
