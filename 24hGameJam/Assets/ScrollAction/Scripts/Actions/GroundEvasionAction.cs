using System;
using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// 地上ダッシュアクション。所持していて接地中なら、Shift入力のたびに水平方向へ瞬間加速。
    /// 回数制限なし (CLAUDE.md 採用案A: 地上は所持していれば連発可)。
    /// </summary>
    [CreateAssetMenu(fileName = "GroundEvasionAction", menuName = "ScrollAction/Actions/Ground Evasion")]
    public class GroundEvasionAction : PlayerAction
    {
        public override string DisplayName => "地上ダッシュ";
        public override string HelpText => "[Shift] 地上";

        /// <summary>地上ダッシュが実際に発動した瞬間に発火。SE 等が購読する。</summary>
        public static event Action OnDashed;

        public override void OnFixedTick(PlayerActionContext ctx, int count)
        {
            if (!ctx.evasionRequested || !ctx.isGrounded) return;
            ApplyEvasion(ctx);
            OnDashed?.Invoke();
        }

        /// <summary>水平速度をダッシュ速度で上書き。方向は入力 or 最後に向いた方向。</summary>
        private static void ApplyEvasion(PlayerActionContext ctx)
        {
            float dir = Mathf.Abs(ctx.inputX) > 0.01f ? Mathf.Sign(ctx.inputX) : ctx.facingDir;
            ctx.rb.linearVelocity = new Vector2(dir * ctx.stats.evasionSpeed, ctx.rb.linearVelocity.y);
        }
    }
}
