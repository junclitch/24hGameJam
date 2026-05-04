using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// ジャンプアクション。所持数 = 1空中滞在中に撃てる回数。
    /// 着地で消費数がリセットされる。空中回避と並ぶ "空中リソース" 扱い。
    /// y速度を上書きする方式なので、落下中の踏切でも常に同じジャンプ高になる。
    /// </summary>
    [CreateAssetMenu(fileName = "JumpAction", menuName = "ScrollAction/Actions/Jump")]
    public class JumpAction : PlayerAction
    {
        public override string DisplayName => "ジャンプ";

        // スタッカブル (上限なし)。0 を返すと Shop が +/- カウンタ表示にする
        public override int MaxCount => 0;

        // 着地後に消費したジャンプ回数。OnLanded で 0 に戻る
        [System.NonSerialized] private int jumpsUsed;

        public override void OnFixedTick(PlayerActionContext ctx, int count)
        {
            if (!ctx.jumpRequested) return;
            if (jumpsUsed >= count) return;

            ctx.rb.linearVelocity = new Vector2(ctx.rb.linearVelocity.x, ctx.stats.jumpForce);
            jumpsUsed++;
        }

        public override void OnLanded() => jumpsUsed = 0;
        public override void OnRespawn() => jumpsUsed = 0;
        public override void OnSessionInit() => jumpsUsed = 0;
    }
}
