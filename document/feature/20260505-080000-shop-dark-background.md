# Shop シーンの背景色を暗く揃える

実装日: 2026-05-05
対象シーン:
- `Assets/Scenes/Shop.unity`

## 概要

Shop シーンの Camera backgroundColor が明るい青 (RGB ≈ 0.192, 0.302, 0.475) で、夜のショップ (闇金) という雰囲気と合わなかった。
ScrollAction シーンと同じ濃紺 (RGB ≈ 0.04, 0.04, 0.07) に揃え、Moon と看板が映える夜空の見た目に統一した。

## 変更点

| 項目 | Before | After |
|---|---|---|
| `Main Camera.Camera.backgroundColor` | (0.192, 0.302, 0.475, 0) ※明るい青 | (0.04, 0.04, 0.07, 1) ※ScrollAction と同値 |

それ以外（`clearFlags`, ライト, `BackgroundRoot/Moon` の SpriteRenderer 等）は変更なし。

## 経緯

1. ScrollAction の Main Camera の `backgroundColor` を確認 → RGB(0.04, 0.04, 0.07) と判明
2. Shop の Main Camera の同プロパティを取得 → 明るい青で値が違うことが原因と特定
3. Shop の `BackgroundRoot` 配下を確認 → 全画面背景スプライトはなく、Moon (`bg_04`) のみ。Camera 色変更で素直に背景全体が暗くなる構成
4. `manage_components.set_property` で Camera.backgroundColor を ScrollAction と同じ値に設定し、シーン保存

## 検証スクショ

- `Assets/Screenshots/shop_dark_bg.png` (game view, 800×450)
  - 黒に近い濃紺の夜空に Moon が浮かび、闇金の看板と提灯が映える状態を確認

## 関連ファイル

- スクリプト変更: なし
- シーン変更: `Assets/Scenes/Shop.unity`
- アセット変更: なし
