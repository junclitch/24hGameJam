using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// 地上限定の左右移動アクション (歩き)。接地中のみ入力方向×walkSpeedへ加減速して水平速度を更新する。
    /// 空中の横移動は AirControlAction が独立して担当する (このアクションは isGrounded=false で無視される)。
    /// 所持していなければ呼ばれない (= 地上ですら横移動できない)。
    /// </summary>
    [CreateAssetMenu(fileName = "HorizontalMoveAction", menuName = "ScrollAction/Actions/Horizontal Move")]
    public class HorizontalMoveAction : PlayerAction
    {
        public override string DisplayName => "歩き";
        public override string HelpText => "[← → / A / D] 地上";

        public override void OnFixedTick(PlayerActionContext ctx, int count)
        {
            if (!ctx.isGrounded) return;
            float targetVx = ctx.inputX * ctx.stats.walkSpeed;
            float newVx = Mathf.MoveTowards(ctx.rb.linearVelocity.x, targetVx, ctx.stats.acceleration * Time.fixedDeltaTime);
            ctx.rb.linearVelocity = new Vector2(newVx, ctx.rb.linearVelocity.y);
        }
    }
}
