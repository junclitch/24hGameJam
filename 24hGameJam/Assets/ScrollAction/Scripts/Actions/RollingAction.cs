using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// 転がるアクション。所持中かつ接地中に Q キー長押しで発動。
    /// しゃがみと同じ高さに BoxCollider2D を縮め、入力方向に rollingSpeed (歩きより少し速い) で水平移動する。
    /// 押している間継続し、離した瞬間にコライダー復元。
    /// HorizontalMoveAction による速度を上書きする想定なので、HorizontalMove より後に Tick されること。
    /// </summary>
    [CreateAssetMenu(fileName = "RollingAction", menuName = "ScrollAction/Actions/Rolling")]
    public class RollingAction : PlayerAction
    {
        public override string DisplayName => "転がる";
        public override string HelpText => "地上で Q 長押し";

        [Header("移動")]
        // 転がり中の水平移動速度 (units/sec)。歩きより少し速い値を SO で設定する
        [SerializeField] private float rollingSpeed;

        [Header("当たり判定")]
        // 転がり中の BoxCollider2D 高さ (ローカル単位)。底面 (足元) を固定して縦だけ縮める。
        // しゃがみと同じ値を入れて運用する想定
        [SerializeField] private float rollingSizeY;

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

            // 接地中のみ発動。空中では転がれない (絵柄が地面ロールのため)
            bool wantRoll = ctx.rollingHeld && ctx.isGrounded;

            if (wantRoll)
            {
                ApplyShrunkCollider(box);
                // 入力方向 × rollingSpeed で水平速度を上書き。HorizontalMoveAction の加減速を打ち消す
                ctx.rb.linearVelocity = new Vector2(ctx.inputX * rollingSpeed, ctx.rb.linearVelocity.y);
                ctx.isRolling = true;
            }
            else if (currentlyShrunk)
            {
                RestoreCollider(box);
            }
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
