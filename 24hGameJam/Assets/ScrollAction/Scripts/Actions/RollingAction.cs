using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// 転がるアクション。所持中かつ接地中に Q キー長押しで発動。
    /// しゃがみと同じ高さに BoxCollider2D を縮め、入力方向に rollingSpeed (歩きより少し速い) で水平移動する。
    /// 押している間継続し、離した瞬間にコライダー復元。
    /// 小穴ブリッジ: 既に転がり中なら、接地外でも進行方向 maxGapWidth 以内に着地できる地面があれば、
    /// vy=0 に張り付けて空中を渡る (= 落ちずに小穴を踏み越える)。maxGapWidth=0 なら従来挙動。
    /// HorizontalMoveAction による速度を上書きする想定なので、HorizontalMove より後に Tick されること。
    /// </summary>
    [CreateAssetMenu(fileName = "RollingAction", menuName = "ScrollAction/Actions/Rolling")]
    public class RollingAction : PlayerAction
    {
        public override string DisplayName => "転がる";
        public override string HelpText => "[Q 長押し] 地上";

        [Header("移動")]
        // 転がり中の水平移動速度 (units/sec)。歩きより少し速い値を SO で設定する
        [SerializeField] private float rollingSpeed;

        [Header("当たり判定")]
        // 転がり中の BoxCollider2D 高さ (ローカル単位)。底面 (足元) を固定して縦だけ縮める。
        // しゃがみと同じ値を入れて運用する想定
        [SerializeField] private float rollingSizeY;

        [Header("小穴ブリッジ")]
        // 接地外でもこの距離以内 (前方) に地面があれば、転がりを継続して落下しない (units)。
        // 0 なら無効 (従来通り、空中で即解除)
        [SerializeField] private float maxGapWidth;

        // 元のコライダー寸法。SO は使い回されるため NonSerialized で保持
        [System.NonSerialized] private float originalSizeY = -1f;
        [System.NonSerialized] private float originalOffsetY;
        [System.NonSerialized] private bool currentlyShrunk;

        public override void OnFixedTick(PlayerActionContext ctx, int count)
        {
            var box = ctx.bodyCollider as BoxCollider2D;
            if (box == null) return;

            // 初回 Tick で元サイズを記憶 (CrouchAction と同パターン)
            if (originalSizeY < 0f)
            {
                originalSizeY = box.size.y;
                originalOffsetY = box.offset.y;
            }

            // 接地中は通常の転がり。空中でも、既に転がり中で前方に近接地面があれば「小穴ブリッジ」継続
            bool wantRoll;
            bool bridging = false;
            if (!ctx.rollingHeld)
            {
                wantRoll = false;
            }
            else if (ctx.isGrounded)
            {
                wantRoll = true;
            }
            else if (currentlyShrunk && CanBridgeSmallGap(ctx))
            {
                wantRoll = true;
                bridging = true;
            }
            else
            {
                wantRoll = false;
            }

            if (wantRoll)
            {
                ApplyShrunkCollider(box);
                // ブリッジ中は vy=0 で落下を抑止 (重力・先行落下を打ち消し、水平に渡らせる)
                float vy = bridging ? 0f : ctx.rb.linearVelocity.y;
                ctx.rb.linearVelocity = new Vector2(ctx.inputX * rollingSpeed, vy);
                ctx.isRolling = true;
            }
            else if (currentlyShrunk)
            {
                RestoreCollider(box);
            }
        }

        /// <summary>
        /// 進行方向の前方 maxGapWidth 地点に、足元判定半径内で地面があるかを返す。
        /// maxGapWidth=0 や 入力ゼロでは false (=ブリッジしない)。
        /// </summary>
        private bool CanBridgeSmallGap(PlayerActionContext ctx)
        {
            if (maxGapWidth <= 0f) return false;
            if (Mathf.Abs(ctx.inputX) < 0.1f) return false;
            float dir = Mathf.Sign(ctx.inputX);
            Vector2 probe = (Vector2)ctx.groundCheck.position + new Vector2(dir * maxGapWidth, 0f);
            return Physics2D.OverlapCircle(probe, ctx.stats.groundCheckRadius, ctx.stats.groundLayer) != null;
        }

        public override void OnRespawn()
        {
            // 物理コライダー復元はスナップショットを持ってないと出来ないので、フラグだけ落とす。
            // 次の Tick で wantRoll=false なら RestoreCollider が走る (CrouchAction と同方針)
            currentlyShrunk = false;
        }

        public override void OnSessionInit()
        {
            originalSizeY = -1f;
            currentlyShrunk = false;
        }

        /// <summary>底面 (足元) を保ったまま高さだけ縮める。</summary>
        private void ApplyShrunkCollider(BoxCollider2D box)
        {
            float bottomLocal = originalOffsetY - originalSizeY * 0.5f;
            box.size = new Vector2(box.size.x, rollingSizeY);
            box.offset = new Vector2(box.offset.x, bottomLocal + rollingSizeY * 0.5f);
            currentlyShrunk = true;
        }

        private void RestoreCollider(BoxCollider2D box)
        {
            box.size = new Vector2(box.size.x, originalSizeY);
            box.offset = new Vector2(box.offset.x, originalOffsetY);
            currentlyShrunk = false;
        }
    }
}
