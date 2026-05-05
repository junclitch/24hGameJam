using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// ジェットパック残燃料を画面左上に横バーで描画するデバッグ/プロト用UI。
    /// 所持していない時は何も描画しない (購入で出現、売却で消える)。
    /// Shop と同じく OnGUI ベース。本実装で uGUI に置き換える前提のプロトタイプ。
    /// </summary>
    public class JetpackGaugeUI : MonoBehaviour
    {
        [SerializeField] private ActionInventory inventory;
        [SerializeField] private JetpackAction jetpackAction;

        // バーの描画矩形 (左上原点、ピクセル)。
        // 未設定 (width or height <= 0) のままだと見えなくなるので、Reset() と OnGUI フォールバックで保険する
        [SerializeField] private Rect barRect;

        // 未設定時の初期表示位置。コンポーネント追加直後でも見える状態にするための保険
        private static readonly Rect DefaultBarRect = new(24f, 24f, 320f, 40f);

        /// <summary>コンポーネント新規追加時 (or インスペクタ Reset時) にデフォルト矩形を入れる。</summary>
        void Reset()
        {
            barRect = DefaultBarRect;
        }

        void OnGUI()
        {
            if (inventory == null || jetpackAction == null) return;
            if (inventory.GetCount(jetpackAction) <= 0) return;

            // 既存インスタンスで barRect 未設定 (= (0,0,0,0)) の場合はフォールバックで描く
            Rect rect = (barRect.width > 0f && barRect.height > 0f) ? barRect : DefaultBarRect;

            float fuel = jetpackAction.Fuel01;

            // 背景パネル
            GUI.Box(rect, GUIContent.none);

            // 充填部 (シアンで残量を可視化)
            var fillRect = new Rect(rect.x + 2f, rect.y + 2f, (rect.width - 4f) * fuel, rect.height - 4f);
            if (fuel > 0f)
            {
                Color prev = GUI.color;
                GUI.color = fuel > 0.25f ? Color.cyan : Color.red;
                GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
                GUI.color = prev;
            }

            // ラベル (バー上に重ねる)
            var labelStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, fontSize = 20 };
            labelStyle.normal.textColor = Color.white;
            GUI.Label(rect, $"JETPACK {Mathf.RoundToInt(fuel * 100f)}%", labelStyle);
        }
    }
}
