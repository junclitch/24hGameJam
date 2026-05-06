using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// 安全靴。所持中、地上で移動キーを離した瞬間に水平速度を 0 にスナップ (即停止)。
    /// 通常は HorizontalMoveAction が acceleration で滑らかに減速するが、安全靴があると
    /// その減速フェーズを飛ばす感触になる。
    /// |vx| が walkSpeed を超える状況 (スライディング等の慣性) は対象外として保護する。
    /// HorizontalMoveAction より後に Tick されること (上書き側) を前提にする。
    /// </summary>
    [CreateAssetMenu(fileName = "SafetyShoesAction", menuName = "ScrollAction/Actions/Safety Shoes")]
    public class SafetyShoesAction : PlayerAction
    {
        public override string DisplayName => "安全靴";
        public override string HelpText => "(自動) 移動キー離して即停止";

        // 所持/未所持のトグル。Shop UI が toggle 行で描画する
        public override int MaxCount => 1;

        public override void OnFixedTick(PlayerActionContext ctx, int count)
        {
            if (!ctx.isGrounded) return;
            if (Mathf.Abs(ctx.inputX) >= 0.01f) return;
            // walkSpeed 超 = スライディング等の慣性中。干渉せず温存する
            if (Mathf.Abs(ctx.rb.linearVelocity.x) > ctx.stats.walkSpeed + 0.5f) return;
            ctx.rb.linearVelocity = new Vector2(0f, ctx.rb.linearVelocity.y);
        }
    }
}
