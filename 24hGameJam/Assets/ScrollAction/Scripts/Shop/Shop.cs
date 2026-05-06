using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// プレイヤーが近接した時にアクションの売買UIを表示するショップ。
    /// 取扱品目は catalog (List&lt;PlayerAction&gt;) に列挙する。
    /// UI は横2分割: 左=「購入」(catalog 全アイテム + 購入ボタン)、右=「売却」(所持中のみ + 売却ボタン)。
    /// 同時表示することでタブ切替の手間がなく、複数所持可 (MaxCount != 1) のアイテムは "x N" で個数表示。
    /// 現状は OnGUI で実装。本実装では uGUI に置き換える前提のプロトタイプ。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Shop : MonoBehaviour
    {
        [SerializeField] private ActionInventory inventory;
        [SerializeField] private UIFontAsset uiFont;

        [Header("販売アイテム")]
        [SerializeField] private List<PlayerAction> catalog = new();

        // Trigger 範囲内にプレイヤーがいるか。GUI表示の出し入れに使う
        private bool playerInside;

        private GUIStyle labelStyle;
        private GUIStyle countStyle;
        private GUIStyle boxStyle;
        private GUIStyle buttonStyle;
        private GUIStyle sectionHeaderStyle;
        private const int FontSize = 16;
        // ウィンドウ表示でパネルが画面右に欠ける問題を防ぐため、1920x1080 設計を基準に GUI.matrix で縮小する。
        // フルスクリーン (>=1920x1080) のときは scale=1 で従来挙動。
        private const float DesignWidth = 1920f;
        private const float DesignHeight = 1080f;

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
        /// プレイヤー近接中に購入(左) / 売却(右) のサブパネルを横並び描画。
        /// 左 = catalog 全件 + 購入ボタン、右 = owned で count > 0 のみ + 売却ボタン。
        /// MaxCount != 1 のスタッカブルは "x N" で所持数を表示する。
        /// </summary>
        void OnGUI()
        {
            if (!playerInside || inventory == null || catalog == null) return;
            EnsureStyles();

            var prevMatrix = GUI.matrix;
            float scale = Mathf.Min(Screen.width / DesignWidth, Screen.height / DesignHeight, 1f);
            if (scale < 1f)
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            const float halfW = 480f;
            const float gap = 12f;
            const float panelW = halfW * 2f + gap + 24f;
            const float rowH = 32f;
            const float titleH = 32f;
            const float moneyH = 28f;
            const float sectionH = 28f;
            const float padding = 12f;

            // 購入列は所持していないものだけ。売却列とアイテムが重複しないようにする
            int buyRows = 0;
            foreach (var c in catalog)
                if (c != null && inventory.GetCount(c) <= 0) buyRows++;
            int ownedRows = 0;
            foreach (var s in inventory.owned)
                if (s?.action != null && s.count > 0) ownedRows++;
            int maxRows = Mathf.Max(buyRows, ownedRows);

            float panelH = titleH + moneyH + sectionH + rowH * maxRows + padding * 3f;

            float x = 24f;
            float y = 24f;
            GUI.Box(new Rect(x, y, panelW, panelH), GUIContent.none);

            // Title
            GUI.Label(new Rect(x, y + 4f, panelW, titleH), "── SHOP ──", boxStyle);

            // Money (中央)
            float curY = y + titleH + 4f;
            GUI.Label(new Rect(x + padding, curY, panelW - padding * 2f, moneyH),
                $"所持金: {inventory.money} G", labelStyle);
            curY += moneyH + 4f;

            // 左右セクションの x 起点
            float leftX = x + padding;
            float rightX = x + padding + halfW + gap;

            // Section headers
            GUI.Label(new Rect(leftX, curY, halfW, sectionH), "── 購入 ──", sectionHeaderStyle);
            GUI.Label(new Rect(rightX, curY, halfW, sectionH), "── 売却 ──", sectionHeaderStyle);
            curY += sectionH;

            // Left column: 購入 (所持中のアイテムは表示しない、売却列と重複させない)
            float buyY = curY;
            foreach (var action in catalog)
            {
                if (action == null) continue;
                if (inventory.GetCount(action) > 0) continue;
                DrawBuyRow(leftX, ref buyY, rowH, halfW, action);
            }

            // Right column: 売却 (snapshot で iteration 中の Sell に対する例外回避)
            float sellY = curY;
            var snapshot = new List<OwnedAction>(inventory.owned);
            foreach (var slot in snapshot)
            {
                if (slot?.action == null || slot.count <= 0) continue;
                DrawSellRow(rightX, ref sellY, rowH, halfW, slot.action, slot.count);
            }

            GUI.matrix = prevMatrix;
        }

        /// <summary>1 行: 名前 / 購入ボタン。所持していないアイテムだけ呼ばれる前提なので所持数は表示しない。</summary>
        private void DrawBuyRow(float x, ref float y, float h, float w, PlayerAction action)
        {
            GUI.Label(new Rect(x + 4f, y, 180f, h), action.DisplayName, labelStyle);

            bool prev = GUI.enabled;
            GUI.enabled = inventory.CanBuy(action);
            if (GUI.Button(new Rect(x + 190f, y + 3f, w - 198f, h - 6f),
                $"購入 (-{action.buyPrice} G)", buttonStyle))
                inventory.Buy(action);
            GUI.enabled = prev;

            y += h;
        }

        /// <summary>1 行: 名前 / (スタッカブルなら所持数) / 売却ボタン。</summary>
        private void DrawSellRow(float x, ref float y, float h, float w, PlayerAction action, int count)
        {
            bool isToggle = action.MaxCount == 1;

            GUI.Label(new Rect(x + 4f, y, 130f, h), action.DisplayName, labelStyle);
            if (!isToggle)
                GUI.Label(new Rect(x + 138f, y, 56f, h), $"x {count}", countStyle);

            bool prev = GUI.enabled;
            GUI.enabled = inventory.CanSell(action);
            if (GUI.Button(new Rect(x + 200f, y + 3f, w - 208f, h - 6f),
                $"売却 (+{action.sellPrice} G)", buttonStyle))
                inventory.Sell(action);
            GUI.enabled = prev;

            y += h;
        }

        private void EnsureStyles()
        {
            if (labelStyle != null) return;
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = FontSize };
            countStyle = new GUIStyle(GUI.skin.label) { fontSize = FontSize, alignment = TextAnchor.MiddleCenter };
            boxStyle = new GUIStyle(GUI.skin.box) { fontSize = FontSize, alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Bold };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = FontSize };
            sectionHeaderStyle = new GUIStyle(GUI.skin.label) { fontSize = FontSize, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            if (uiFont != null && uiFont.Font != null)
            {
                labelStyle.font = uiFont.Font;
                countStyle.font = uiFont.Font;
                boxStyle.font = uiFont.Font;
                buttonStyle.font = uiFont.Font;
                sectionHeaderStyle.font = uiFont.Font;
            }
        }
    }
}
