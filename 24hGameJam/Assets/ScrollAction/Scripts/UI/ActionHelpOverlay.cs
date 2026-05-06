using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// 所持中のアクションの操作説明を画面下中央に常時表示するオーバーレイ。
    /// inventory.owned を毎フレーム走査して count > 0 のスロットだけ列挙する。
    /// 自前 GUIStyle を使い、Shop など他の OnGUI コンポーネントへフォント設定を漏らさない。
    /// </summary>
    public class ActionHelpOverlay : MonoBehaviour
    {
        [SerializeField] private ActionInventory inventory;
        [SerializeField] private UIFontAsset uiFont;

        // 画面下中央レイアウト用の固定値 (UIの見栄え調整なので const として保持)
        private const float PanelWidth = 460f;
        private const float ScreenMargin = 24f;
        private const float InnerPadding = 16f;
        private const int FontSize = 18;
        // ウィンドウ表示時にレイアウトが端で欠けるのを防ぐため、1920x1080 設計に正規化して GUI.matrix で縮小する。
        // フルスクリーン (>=1920x1080) のときは scale=1 で従来挙動。
        private const float DesignWidth = 1920f;
        private const float DesignHeight = 1080f;

        private GUIStyle labelStyle;
        private GUIStyle titleStyle;

        void OnGUI()
        {
            if (inventory == null || inventory.owned == null) return;
            int rows = CountVisibleRows();
            if (rows <= 0) return;

            EnsureStyles();

            var prevMatrix = GUI.matrix;
            float scale = Mathf.Min(Screen.width / DesignWidth, Screen.height / DesignHeight, 1f);
            if (scale < 1f)
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            float logicalW = Screen.width / scale;
            float logicalH = Screen.height / scale;

            float rowH = FontSize + 12f;
            float panelH = rowH * (rows + 1) + 16f;
            float x = (logicalW - PanelWidth) * 0.5f;
            float y = logicalH - panelH - ScreenMargin;

            GUI.Box(new Rect(x, y, PanelWidth, panelH), GUIContent.none);

            GUI.Label(new Rect(x + InnerPadding, y + 6f, PanelWidth - InnerPadding * 2f, rowH),
                "操作 (所持中)", titleStyle);

            float rowY = y + rowH + 6f;
            foreach (var slot in inventory.owned)
            {
                if (slot?.action == null || slot.count <= 0) continue;
                GUI.Label(new Rect(x + InnerPadding, rowY, PanelWidth - InnerPadding * 2f, rowH),
                    $"{slot.action.DisplayName}: {slot.action.HelpText}", labelStyle);
                rowY += rowH;
            }

            GUI.matrix = prevMatrix;
        }

        private void EnsureStyles()
        {
            if (labelStyle != null) return;
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = FontSize };
            if (uiFont != null && uiFont.Font != null) labelStyle.font = uiFont.Font;
            labelStyle.normal.textColor = Color.white;
            titleStyle = new GUIStyle(labelStyle) { fontStyle = FontStyle.Bold };
        }

        private int CountVisibleRows()
        {
            int n = 0;
            foreach (var slot in inventory.owned)
                if (slot?.action != null && slot.count > 0) n++;
            return n;
        }
    }
}
