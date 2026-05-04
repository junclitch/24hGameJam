using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// 空中回避アクション。所持数 = 1空中滞在中に撃てる回数。着地で消費数リセット。
    /// 地上回避と回避入力 (Shift) を共有するが、IsGrounded が false の時のみ反応する。
    /// 地上回避と同時に発火しないよう、地上回避→空中回避の順に Tick が回ることを前提にしている。
    /// </summary>
    [CreateAssetMenu(fileName = "AirEvasionAction", menuName = "ScrollAction/Actions/Air Evasion")]
    public class AirEvasionAction : PlayerAction
    {
        public override string DisplayName => "空中回避";

        // スタッカブル (上限なし)
        public override int MaxCount => 0;

        // 離地後に消費した回数。OnLanded で 0 にリセット
        [System.NonSerialized] private int airUsed;

        public override void OnFixedTick(PlayerActionContext ctx, int count)
        {
            if (!ctx.evasionRequested || ctx.isGrounded) return;
            if (airUsed >= count) return;

            float dir = Mathf.Abs(ctx.inputX) > 0.01f ? Mathf.Sign(ctx.inputX) : ctx.facingDir;
            ctx.rb.linearVelocity = new Vector2(dir * ctx.stats.evasionSpeed, ctx.rb.linearVelocity.y);
            airUsed++;
        }

        public override void OnLanded() => airUsed = 0;
        public override void OnRespawn() => airUsed = 0;
        public override void OnSessionInit() => airUsed = 0;
    }
}
