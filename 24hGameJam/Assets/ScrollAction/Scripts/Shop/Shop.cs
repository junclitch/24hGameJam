using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// プレイヤーが近接した時にアクションの売買UIを表示するショップ。
    /// 取扱品目は catalog (List&lt;PlayerAction&gt;) に列挙する。アクション追加時はここにドロップするだけで対応できる。
    /// 売買は所持金 (inventory.money) の増減で成立し、不足/上限到達時は購入ボタンが無効化される。
    /// 各品目は MaxCount=1 ならトグル、それ以外は +/- カウンタで描画する。
    /// 現状は OnGUI で実装。本実装では uGUI に置き換える前提のプロトタイプ。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Shop : MonoBehaviour
    {
        [SerializeField] private ActionInventory inventory;

        [Header("販売アイテム")]
        [SerializeField] private List<PlayerAction> catalog = new();

        // Trigger 範囲内にプレイヤーがいるか。GUI表示の出し入れに使う
        private bool playerInside;

        // OnGUI 用のフォント拡大スタイル。GUI.skin を直接書き換えると他の OnGUI に漏れるので自前で保持
        private GUIStyle labelStyle;
        private GUIStyle boxStyle;
        private GUIStyle buttonStyle;
        private const int FontSize = 18;

        /// <summary>
        /// プレイヤーがショップ範囲から出た時に発火する。
        /// PlayerController がリスポーン時の接地猶予を解除するために購読する想定 (疎結合)。
        /// </summary>
        public static event Action OnPlayerExitedShop;

        void Reset()
        {
            // 自身の Collider2D を Trigger に強制
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController>() != null)
                playerInside = true;
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController>() != null)
            {
                playerInside = false;
                OnPlayerExitedShop?.Invoke();
            }
        }

        /// <summary>
        /// プレイヤーが範囲内のときだけ売買メニューを描画。所持金ヘッダ + catalog 各行。
        /// MaxCount==1 → トグル、それ以外 → +/- カウンタ。所持金が足りない購入ボタンは GUI.enabled で無効化する。
        /// </summary>
        void OnGUI()
        {
            if (!playerInside || inventory == null || catalog == null) return;
            EnsureStyles();

            const float panelW = 560f;
            const float rowH = 40f;
            // ヘッダ(タイトル) + 所持金行 + 各アクション行
            float panelH = rowH * (catalog.Count + 2) + 24f;

            float x = 24f;
            float y = 24f;
            GUI.Box(new Rect(x, y, panelW, panelH), "SHOP (近接中: 売買)", boxStyle);

            // 所持金表示
            float rowY = y + rowH;
            GUI.Label(new Rect(x + 16f, rowY, panelW - 32f, rowH), $"所持金: {inventory.money} G", labelStyle);
            rowY += rowH;

            foreach (var action in catalog)
            {
                if (action == null) continue;
                int count = inventory.GetCount(action);
                if (action.MaxCount == 1) DrawToggleRow(x, ref rowY, rowH, action, count);
                else DrawCounterRow(x, ref rowY, rowH, action, count);
            }
        }

        private void EnsureStyles()
        {
            if (labelStyle != null) return;
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = FontSize };
            boxStyle = new GUIStyle(GUI.skin.box) { fontSize = FontSize, alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Bold };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = FontSize };
        }

        /// <summary>bool型(MaxCount==1)アクションの行: 能力名 / 状態 / 価格付きトグルボタン。</summary>
        private void DrawToggleRow(float x, ref float y, float h, PlayerAction action, int count)
        {
            bool owned = count > 0;
            GUI.Label(new Rect(x + 16f, y, 170f, h), action.DisplayName, labelStyle);
            GUI.Label(new Rect(x + 195f, y, 90f, h), owned ? "所持中" : "未所持", labelStyle);

            string label = owned ? $"売却 (+{action.sellPrice})" : $"購入 (-{action.buyPrice})";
            bool prevEnabled = GUI.enabled;
            GUI.enabled = owned ? inventory.CanSell(action) : inventory.CanBuy(action);
            if (GUI.Button(new Rect(x + 290f, y + 4f, 250f, h - 8f), label, buttonStyle))
            {
                if (owned) inventory.Sell(action);
                else inventory.Buy(action);
            }
            GUI.enabled = prevEnabled;
            y += h;
        }

        /// <summary>整数カウントアクションの行: 能力名 / 所持数 / 売却(-1, +sell) / 購入(+1, -buy)。</summary>
        private void DrawCounterRow(float x, ref float y, float h, PlayerAction action, int count)
        {
            GUI.Label(new Rect(x + 16f, y, 170f, h), action.DisplayName, labelStyle);
            GUI.Label(new Rect(x + 195f, y, 90f, h), $"x {count}", labelStyle);

            bool prevEnabled = GUI.enabled;

            GUI.enabled = inventory.CanSell(action);
            if (GUI.Button(new Rect(x + 290f, y + 4f, 120f, h - 8f), $"売却 (+{action.sellPrice})", buttonStyle))
                inventory.Sell(action);

            GUI.enabled = inventory.CanBuy(action);
            if (GUI.Button(new Rect(x + 420f, y + 4f, 120f, h - 8f), $"購入 (-{action.buyPrice})", buttonStyle))
                inventory.Buy(action);

            GUI.enabled = prevEnabled;
            y += h;
        }
    }
}
