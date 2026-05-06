# IMGUI Overlay UI 設計指針 (このプロジェクト固有)

このプロジェクトの HUD・タイマー・残機・ショップ・操作ヘルプ等は OnGUI (IMGUI) ベースで実装されている。**本ドキュメントは IMGUI で「文字+背景枠」を描く時に踏みやすい罠と、その回避方針**をまとめる。是正報告書 `document/correction/20260507-000650-timer-lives-imgui-overflow.md` の事故を受けて起こした。

---

## 1. 結論 (まずこれを守る)

OnGUI で「テキスト + 背景の四角ブロック」を出す時:

| やること | やらないこと |
|---|---|
| `GUI.DrawTexture(rect, Texture2D.whiteTexture)` で背景を**自前で塗る** | `GUI.Box(rect, GUIContent.none)` で背景を出す |
| ブロック高は `fontSize × 2.0` 等の**固定式**で確保する | `GUIStyle.CalcSize().y` をそのまま rect 高に使う |
| 背景 rect と Label rect を**完全同一の Rect** にする | label rect と box rect をオフセット (`-4f, +8f` 等) で別に設計する |
| 完成判定は**複数の動的値**で確認する | 1 ショット 1 状態のスクショで「直った」と報告する |

**TL;DR**: `GUI.Box` を使わない。背景は `DrawTexture(white)`。高さは `fontSize × 2.0`。同じ Rect に背景と Label を重ねる。

---

## 2. なぜ `GUI.Box` を使うとはまるか

`GUI.Box` ウィジェットは `GUI.skin.box` GUISkin スタイルを使って描画される。このスタイルは**外形 rect の内側に padding と border (薄い灰色グラデーション枠)** を持つ:

```
GUI.Box(new Rect(0, 0, 100, 50))   // 外形は 100×50
  ├── padding (~4–6px) ← 内側コンテンツ域がここで狭まる
  ├── border (薄い枠グラデーション) ← 描画される
  └── 実コンテンツ描画域 ← 100×50 より一回り狭い
```

なので、「label rect = (x, y, w, h)、box rect = (x-4, y-4, w+8, h+8)」という素朴な「上下 4px 余裕」設計では、**box の内側 padding にその 4px が食われて実質マージン 0**。glyph が rect 縁ギリギリで描画される。

CJK ダイナミックフォント (NotoSansJP-VF 等) は glyph 高が `fontSize × 1.5〜1.7` あり、`GUIStyle.CalcSize().y` がそれより小さい値を返しがちなので、ここに上記の box 内側 padding 問題が乗ると上端がはみ出す。

---

## 3. 推奨パターン: `DrawLabelWithBackdrop`

`Assets/ScrollAction/Scripts/Scene/GameCycleManager.cs` で採用しているパターンを汎用形で示す:

```csharp
/// <summary>
/// fontSize から「Box+Label ブロックの安全高さ」を返す。
/// CJK ダイナミックフォント (NotoSansJP-VF 等) は glyph 高が fontSize×1.5〜1.7 に達し、
/// IMGUI の GUIStyle.CalcSize はそれを不正確に返しがちなため、fontSize×2.0 を固定で使う。
/// </summary>
private static float LabelBlockHeight(int fontSize) => fontSize * 2.0f;

/// <summary>
/// 背景黒 → テキスト の順で描画。GUI.Box の内側パディング/ボーダーに依存せず
/// テキストを厳密に中央揃えする。
/// w は CalcSize().x、style は alignment=MiddleCenter 前提。
/// </summary>
private static void DrawLabelWithBackdrop(float x, float y, float w, int fontSize, string text, GUIStyle style)
{
    float h = LabelBlockHeight(fontSize);
    var prev = GUI.color;
    GUI.color = new Color(0f, 0f, 0f, 0.6f);
    GUI.DrawTexture(new Rect(x - 16f, y, w + 32f, h), Texture2D.whiteTexture);
    GUI.color = prev;
    GUI.Label(new Rect(x - 16f, y, w + 32f, h), text, style);
}
```

ポイント:
- **背景と Label が完全に同じ Rect**。padding ズレが発生しない
- 背景は `Texture2D.whiteTexture` を `GUI.color` で着色して塗る (どこにでも使える素手の塗り)
- 高さは `fontSize × 2.0`。glyph (`fontSize × 1.5〜1.7`) との差 30〜50% が上下マージンになる
- 横幅は `CalcSize().x` でテキスト幅を取り、左右に 16px 余裕を入れて装飾感を出している

---

## 4. デザイン解像度ベースのスケール (UnityRoom 対応)

`GameCycleManager` / `Shop` / `ActionHelpOverlay` の OnGUI は、UnityRoom 等の小さな iframe でも崩れないよう **`GUI.matrix` で 1920×1080 設計を基準にスケール**する:

```csharp
private const float DesignWidth = 1920f;
private const float DesignHeight = 1080f;

void OnGUI()
{
    EnsureStyles();

    var prevMatrix = GUI.matrix;
    float scale = Mathf.Min(Screen.width / DesignWidth, Screen.height / DesignHeight, 1f);
    if (scale < 1f)
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
    float logicalW = Screen.width / scale;
    float logicalH = Screen.height / scale;

    // ... logicalW / logicalH ベースで配置 ...

    GUI.matrix = prevMatrix;
}
```

ルール:
- `scale` は **`Min(..., 1f)` で 1 を上限**にする (フルスクリーン時に拡大しない)
- 画面サイズ参照は `Screen.width / Screen.height` の代わりに **`logicalW = Screen.width / scale`** を使う
- 終端で必ず `GUI.matrix = prevMatrix` で復元する (他の OnGUI コンポーネントへの汚染を避ける)

---

## 5. CJK ダイナミックフォントの扱い

WebGL ビルドで日本語/矢印グリフを表示するため、NotoSansJP-VF を `Assets/ScrollAction/Fonts/NotoSansJP-VF.ttf` に同梱し、`UIFontAsset` SO (`Assets/ScrollAction/Data/UIFont.asset`) 経由で各 OnGUI コンポーネントに注入している。詳細は `Assets/ScrollAction/Scripts/UI/UIFontAsset.cs` 参照。

CJK ダイナミックフォントを IMGUI で使う時の注意:

- **`GUIStyle.CalcSize().y` を rect 高として信用しない**。`fontSize × 2.0` を固定で使う
- フォントサイズを変える時は `LabelBlockHeight` 経由でブロック高を取り直す
- フォントを差し替える時は最低 3 種類のテキスト (短い数字・長い漢字混じり・記号) で確認する

---

## 6. 検証プロトコル

「直った」と報告する前に必ず行う:

### 6.1 動的 UI (秒数・スコア・残機など値が変わるもの)
- 最低 **3 種類の値** で目視確認 (短い文字列・長い文字列・漢字の縦が高い文字列)
- play mode で時間経過させて、**連続フレームで観測**する (1 ショットで判定しない)

### 6.2 解像度違いの確認
- 設計解像度 (1920×1080) で正常表示
- 半分の解像度 (960×540) でも比例縮小されて崩れない
- editor の Game view 解像度を切り替えて検証可

### 6.3 数値検証 (見た目に頼らない)
- box の rect と label の rect を `Debug.Log` で出して、想定通りに重なっているか確認
- glyph 高が見えない時は `GUI.color = Color.red` で背景を一時的に赤く塗ると rect 境界が分かる

---

## 7. 関連ドキュメント・メモリ

- `document/correction/20260507-000650-timer-lives-imgui-overflow.md` — このドキュメントを起こした事故の是正報告
- `document/unity-coding-conventions.md` の「推奨・設定変更の規律」 — 機構を 1 行で説明できないものは推奨しない
- 自動メモリ `feedback_animator_e2e_verify.md` — 動的なものは end-to-end で観測する
- 自動メモリ `feedback_verify_colliders.md` — 見た目だけで完了判定しない
- 自動メモリ `feedback_unityroom_decompression_fallback.md` — UnityRoom 公開時の WebGL 設定

---

## 8. 既存の OnGUI 実装ファイル

このプロジェクトで OnGUI を持つコンポーネント (本指針の適用対象):

| ファイル | 役割 |
|---|---|
| `Assets/ScrollAction/Scripts/Scene/GameCycleManager.cs` | タイマー/残機/リスタートボタン (本指針の参照実装) |
| `Assets/ScrollAction/Scripts/Shop/Shop.cs` | ショップの売買 UI |
| `Assets/ScrollAction/Scripts/UI/ActionHelpOverlay.cs` | 操作ヘルプ常時表示 |
| `Assets/ScrollAction/Scripts/UI/JetpackGaugeUI.cs` | ジェットパック残燃料バー (ASCII のみなので CJK 罠は無関係) |

新たに OnGUI コンポーネントを起こす時は本指針に沿うこと。本実装に合流させる場合は uGUI への移行も検討する (Shop.cs / JetpackGaugeUI.cs のクラスコメントに「uGUI に置き換える前提のプロトタイプ」と明記済)。
