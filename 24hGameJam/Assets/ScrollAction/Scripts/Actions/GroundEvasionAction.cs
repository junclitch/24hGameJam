using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// 地上回避アクション。所持していて接地中なら、Shift入力のたびに水平方向へ瞬間加速。
    /// 回数制限なし (CLAUDE.md 採用案A: 地上は所持していれば連発可)。
    /// </summary>
    [CreateAssetMenu(fileName = "GroundEvasionAction", menuName = "ScrollAction/Actions/Ground Evasion")]
    public class GroundEvasionAction : PlayerAction
    {
        public override string DisplayName => "地上回避";
        public override string HelpText => "地上で Shift";

        public override void OnFixedTick(PlayerActionContext ctx, int count)
        {
            if (!ctx.evasionRequested || !ctx.isGrounded) return;
            ApplyEvasion(ctx);
        }

        /// <summary>水平速度を回避速度で上書き。方向は入力 or 最後に向いた方向。</summary>
        private static void ApplyEvasion(PlayerActionContext ctx)
        {
            float dir = Mathf.Abs(ctx.inputX) > 0.01f ? Mathf.Sign(ctx.inputX) : ctx.facingDir;
            ctx.rb.linearVelocity = new Vector2(dir * ctx.stats.evasionSpeed, ctx.rb.linearVelocity.y);
        }
    }
}
