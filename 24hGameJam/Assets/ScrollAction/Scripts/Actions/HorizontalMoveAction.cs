using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// 左右移動アクション。所持中は入力方向×walkSpeedへ加減速して水平速度を更新する。
    /// 所持していない場合は呼ばれないので、何もしない (入力は他アクション=回避で読まれている)。
    /// </summary>
    [CreateAssetMenu(fileName = "HorizontalMoveAction", menuName = "ScrollAction/Actions/Horizontal Move")]
    public class HorizontalMoveAction : PlayerAction
    {
        public override string DisplayName => "歩き";
        public override string HelpText => "← → / A D";

        public override void OnFixedTick(PlayerActionContext ctx, int count)
        {
            float targetVx = ctx.inputX * ctx.stats.walkSpeed;
            float newVx = Mathf.MoveTowards(ctx.rb.linearVelocity.x, targetVx, ctx.stats.acceleration * Time.fixedDeltaTime);
            ctx.rb.linearVelocity = new Vector2(newVx, ctx.rb.linearVelocity.y);
        }
    }
}
