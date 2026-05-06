using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// OnGUI 系コンポーネント (Shop, ActionHelpOverlay, GameCycleManager, JetpackGaugeUI) が
    /// 共通参照する UI フォント。WebGL ビルドでは Unity 標準の GUI.skin.font に日本語/矢印グリフが
    /// 含まれず文字が消えるため、日本語対応 TTF をここに集約してアセット1点で差し替えられるようにする。
    /// font 未設定なら GUI.skin の既定フォントが使われる (= 既存の挙動)。
    /// </summary>
    [CreateAssetMenu(fileName = "UIFont", menuName = "ScrollAction/UI Font")]
    public class UIFontAsset : ScriptableObject
    {
        [SerializeField] private Font font;

        /// <summary>OnGUI スタイルへ適用する Font。未設定なら null を返し、呼出側でフォールバックさせる。</summary>
        public Font Font => font;
    }
}
