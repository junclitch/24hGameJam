using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// グライダーアクション。所持中は F 長押しで「空中横移動の最高速ブースト」+「落下速度上限の引き下げ」が掛かる。
    /// 接地中は何もしない (空中専用)。離した瞬間に通常重力・通常横移動に戻る。
    /// 横は HorizontalMoveAction と同じ方式 (MoveTowards で targetVx に追従) なので、
    /// HorizontalMoveAction より後ろに Tick されることを前提にしている (上書きする側)。
    /// </summary>
    [CreateAssetMenu(fileName = "GliderAction", menuName = "ScrollAction/Actions/Glider")]
    public class GliderAction : PlayerAction
    {
        public override string DisplayName => "グライダー";

        [Header("空中ブースト")]
        // 空中グライド中の walkSpeed 倍率 (1.0 = 強化なし)。少しだけ強くする想定なので 1.2〜1.4 程度
        [SerializeField] private float airSpeedMultiplier;

        [Header("落下抑制")]
        // 落下速度の下限値 (絶対値)。vy がこれより小さく (=より下向きに速く) ならないようクランプ
        [SerializeField] private float maxFallSpeed;

        public override void OnFixedTick(PlayerActionContext ctx, int count)
        {
            // 空中専用: 接地中はノータッチ
            if (!ctx.gliderHeld || ctx.isGrounded) return;

            ctx.isGliding = true;

            // 横移動を再ターゲット。HorizontalMove の上限 (walkSpeed) を倍率分だけ引き上げる
            float targetVx = ctx.inputX * ctx.stats.walkSpeed * airSpeedMultiplier;
            float newVx = Mathf.MoveTowards(
                ctx.rb.linearVelocity.x,
                targetVx,
                ctx.stats.acceleration * Time.fixedDeltaTime);

            // 落下速度を下限クランプ。上昇中 (vy>=0) には介入しない
            float vy = ctx.rb.linearVelocity.y;
            if (vy < -maxFallSpeed) vy = -maxFallSpeed;

            ctx.rb.linearVelocity = new Vector2(newVx, vy);
        }
    }
}
