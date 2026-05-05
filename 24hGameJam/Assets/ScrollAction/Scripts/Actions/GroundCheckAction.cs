namespace ScrollAction
{
    /// <summary>
    /// 接地判定アクション。所持しているとプレイヤーが地面と衝突できる + IsGrounded が機能する。
    /// 売却すると地面をすり抜け、ジャンプ回数や空中回避のリセットも発生しなくなる。
    /// このアクションは "マーカー" 役割で OnFixedTick はノーオペ。
    /// PlayerController 側が inventory.HasAny&lt;GroundCheckAction&gt;() を直接見て物理セットアップを切り替える。
    /// </summary>
    [UnityEngine.CreateAssetMenu(fileName = "GroundCheckAction", menuName = "ScrollAction/Actions/Ground Check")]
    public class GroundCheckAction : PlayerAction
    {
        public override string DisplayName => "接地判定";
        public override string HelpText => "(常時ON)";
    }
}
