# Prefab リファクタリング 実装まとめ

実装日: 2026-05-05
対象シーン: `Assets/Scenes/ScrollAction.unity`, `Assets/Scenes/Shop.unity`

## 概要

このプロジェクトには Prefab がひとつも無く、シーン直下に GameObject を直接配置していた。とくに Player は `ScrollAction.unity` と `Shop.unity` に**独立した実体**として2つ存在し、片方を変更しても他方に反映されない構造だった。`PlayerAnimatorBridge` 追加 (前作業 `20260505-010205-action-animation.md`) で Player の構造が複雑化したことで、二重メンテのコストが現実化していたため、ここで Prefab 化に踏み切った。

合わせて、同型オブジェクトのコピペ (Marker_01〜11) と、フォルダ命名の不統一 (`Sprite/` 単数形) も同時に整理した。

## 成果物

### 1. 新設フォルダ
- `Assets/ScrollAction/Prefabs/` を新設

### 2. Prefab 4種

| Prefab | 用途 | インスタンス配置先 |
|---|---|---|
| `Player.prefab` | Rigidbody2D + BoxCollider2D + PlayerController + 子の `GroundCheck` + 子の `Visual` (Animator + SpriteRenderer + PlayerAnimatorBridge) を一括化 | `ScrollAction.unity`, `Shop.unity` |
| `Marker.prefab` | TextMesh + MeshRenderer の Legacy 3D Text 1個分 (位置マーカー) | `ScrollAction.unity` の `MarkersRoot` 配下に 11 個 |
| `KillZone.prefab` | BoxCollider2D (Trigger) + KillZone スクリプト | `ScrollAction.unity` |
| `Shop.prefab` | BoxCollider2D (Trigger) + SpriteRenderer + Shop スクリプト | `Shop.unity` |

### 3. シーン側の変更
- `ScrollAction.unity`
  - Player / KillZone / Marker_01～11 すべてが Prefab インスタンスにリンク
  - Marker_02～11 は再生成。`text` だけ Override (位置はインスタンス Transform で保持)
- `Shop.unity`
  - 古い Player (`Visual` 子なしの旧構造) を削除し、Player Prefab を `(-7, 1, 0)` にインスタンス化
  - `BelowGroundRespawn.player`, `Main Camera.CameraExitTransition.target` の参照を新インスタンスに再リンク
  - Shop GameObject を Prefab 化

### 4. フォルダ命名統一
- `Assets/ScrollAction/Sprite/` → `Assets/ScrollAction/Sprites/` (CLAUDE.md 推奨命名)
- スプライト参照は GUID ベースなので、シーン・SO 側に追従作業は不要

## 設計上のポイント

### A. Player を最優先で Prefab 化した理由
- 2シーンに独立配置されており、`PlayerAnimatorBridge` 追加で構造が複雑化したことで二重メンテのコストが顕在化
- Prefab 化前は `Shop.unity` の Player に `Visual` 子が無く、見た目も挙動 (Animator) も `ScrollAction.unity` と乖離していた
- Prefab 化後は1箇所の編集で両シーンに反映される

### B. Marker は Variant ではなく素の Prefab + 個別 Override
- 11 個の差分は「テキスト数字」と「位置」のみで、構造的なバリエーションは無い
- Prefab Variant を切るほどでもなく、素の Prefab にして TextMesh.text を Override する方が運用が単純
- 将来、フォントサイズや色など全体方針を変えたいときは元 Prefab を1箇所いじればよい

### C. 参照の再リンクは GUID ではなく instanceID で
- MCP 経由で再リンクする際、`set_property` に新インスタンスの instanceID を直接渡した
- Prefab 内部の component (PlayerController) を Scene 側コンポーネント (BelowGroundRespawn) から参照しているため、Prefab 化しても参照型は同じ MonoBehaviour 参照のままで成立

### D. KillZone / Shop は単発でも Prefab 化した
- 現状は1シーンずつにしかないが、テンプレ的な役割で**他レベル/章を作るときに一番複製したくなる候補**
- 数分の作業なので、本流から逸れすぎない範囲で先回りした

## 嵌った / 学んだこと

1. **`create_from_gameobject` で生成した Prefab の元GameObjectは名前を保持する**
   - 一見 `instanceName: "Marker"` と返ってくるが、これは prefab 名であってシーン側 GameObject 名は元のまま (今回は `Marker_01`)
   - 念のため `find_gameobjects` で再確認した

2. **MCP 越しに作った GameObject の instanceID は負数になる**
   - Prefab を `manage_gameobject create` で配置したインスタンスの ID が `-77558` などとして返ってくる
   - 同セッション内なら、参照の再リンク時にそのまま渡せる (今回 BelowGroundRespawn / CameraExitTransition で実際に通った)

3. **`manage_asset rename` がエラーで返るのに移動は完了することがある**
   - `Sprite/` → `Sprites/` リネームで `MoveAsset call failed unexpectedly` が返ってきたが、実ファイルシステムでは正しくリネーム済みだった
   - 失敗ログを見て即やり直しに走らず、まず実状態を確認するのが安全

4. **Prefab 化作業はシーン直書きの「コピペ重複」を可視化する**
   - 11個コピペされていた Marker、2シーンに独立していた Player、いずれも Prefab 化のタイミングで初めて「この差分は意図したものか／単なるコピペか」を仕分けできた
   - 今後新しい同型オブジェクトを足すときは、最初から Prefab で始める方針でいく

## 既知の追加ToDo (将来)

- 前作業のドキュメント (`20260505-010205-action-animation.md`) 内の `Assets/ScrollAction/Sprite/...` 表記は、今回の `Sprites/` リネームによりパスが古くなっている。歴史的記録として残すか追従更新するかは未判断
- Player Prefab の Player 直下 `SpriteRenderer` (赤いプレースホルダ、`enabled=false`) は前作業で残置となっているが、Prefab 化により削除しても両シーンに同時反映できるようになった。次回の整理タイミングで判断
- `MCPTest/` 配下のブロック崩しサンプルにも Prefab は無い。CLAUDE.md で「A+B+C 雛形」として参照されているため今回は触らずに残置
