using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// 空中限定の横移動制御。空中 (isGrounded=false) で入力方向×airSpeed へ加減速する。
    /// 地上は HorizontalMoveAction が担当。両者が同時に vx を上書きすることはない (互いに排他ガード)。
    /// パラメータを SO 側で個別設定できるので、地上と空中で「速度」「加速度」を別チューニング可能
    /// (例: 空中は地上より滑る、空中の方が遅い、など)。
    /// </summary>
    [CreateAssetMenu(fileName = "AirControlAction", menuName = "ScrollAction/Actions/Air Control")]
    public class AirControlAction : PlayerAction
    {
        public override string DisplayName => "空中制御";
        public override string HelpText => "[← → / A / D] 空中";

        [Header("空中横移動")]
        // 空中での横方向最大速度 (units/sec)。.asset で設定。地上 walkSpeed と独立
        [SerializeField] private float airSpeed;

        // 空中での加減速 (units/sec^2)。.asset で設定。地上 acceleration より低めにすると "滑る" 感が出る
        [SerializeField] private float airAcceleration;

        public override void OnFixedTick(PlayerActionContext ctx, int count)
        {
            if (ctx.isGrounded) return;
            float targetVx = ctx.inputX * airSpeed;
            float newVx = Mathf.MoveTowards(ctx.rb.linearVelocity.x, targetVx, airAcceleration * Time.fixedDeltaTime);
            ctx.rb.linearVelocity = new Vector2(newVx, ctx.rb.linearVelocity.y);
        }
    }
}
