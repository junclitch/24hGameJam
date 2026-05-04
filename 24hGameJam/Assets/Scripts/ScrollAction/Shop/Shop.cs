using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// プレイヤーが近接した時にアクションの売買UIを表示するショップ。
    /// 見た目は "とりあえず黒い四角" の SpriteRenderer 想定。Trigger Collider2D を子として持つ。
    /// 現状はプロトタイプのため OnGUI で売買トグルを描画する (CLAUDE.md 規約上 OnGUI は本番UI不可だが、
    /// 黒い四角プレースホルダのうちは暫定実装として割り切る。本実装では uGUI に置き換える前提)。
    /// 売買はお金の概念を持たず、所持/未所持のトグル切り替えのみ行う。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Shop : MonoBehaviour
    {
        [SerializeField] private ActionInventory inventory;

        // Trigger 範囲内にプレイヤーがいるか。GUI表示の出し入れに使う
        private bool playerInside;

        void Reset()
        {
            // 自身の Collider2D を Trigger に強制する。設定し忘れを防ぐ
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
                playerInside = false;
        }

        /// <summary>
        /// プレイヤーが範囲内のときだけ売買メニューを描画する。
        /// 各行は「能力名 / 状態 / トグルボタン」の3列構成。クリックで所持状態を反転する。
        /// </summary>
        void OnGUI()
        {
            if (!playerInside || inventory == null) return;

            const float panelW = 360f;
            const float rowH = 32f;
            const int rowCount = 6;
            float panelH = rowH * (rowCount + 1) + 24f;

            float x = 24f;
            float y = 24f;
            GUI.Box(new Rect(x, y, panelW, panelH), "SHOP (近接中: 売買)");

            float rowY = y + rowH;
            DrawRow(x, ref rowY, panelW, rowH, "左右移動", ref inventory.hasHorizontalMove);
            DrawRow(x, ref rowY, panelW, rowH, "加減速", ref inventory.hasAccelDecel);
            DrawRow(x, ref rowY, panelW, rowH, "ジャンプ", ref inventory.hasJump);
            DrawRow(x, ref rowY, panelW, rowH, "接地判定", ref inventory.hasGroundCheck);
            DrawRow(x, ref rowY, panelW, rowH, "ジャンプ+1", ref inventory.hasDoubleJump);
            DrawRow(x, ref rowY, panelW, rowH, "ダッシュ", ref inventory.hasDash);
        }

        /// <summary>1行分のラベルとトグルボタンを描画。クリックされたら値を反転して通知する。</summary>
        private void DrawRow(float x, ref float y, float w, float h, string label, ref bool owned)
        {
            GUI.Label(new Rect(x + 12f, y, 140f, h), label);
            GUI.Label(new Rect(x + 160f, y, 80f, h), owned ? "所持中" : "未所持");
            string btn = owned ? "売却" : "購入";
            if (GUI.Button(new Rect(x + 250f, y + 4f, 90f, h - 8f), btn))
            {
                owned = !owned;
                inventory.NotifyChanged();
            }
            y += h;
        }
    }
}
