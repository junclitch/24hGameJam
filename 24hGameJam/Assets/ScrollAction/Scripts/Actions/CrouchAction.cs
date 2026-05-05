using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// しゃがみアクション。所持中かつ接地中に下方向入力で発動。
    /// 水平速度を 0 に減衰しつつ、BoxCollider2D を縦方向に縮める (底面=足元は固定)。
    /// 入力が離れた瞬間に元のサイズへ復元する。
    /// HorizontalMoveAction の後に Tick されることを前提にしている (上書きで止める)。
    /// 所持していなければ呼ばれないので、未所持時は通常移動のまま。
    /// </summary>
    [CreateAssetMenu(fileName = "CrouchAction", menuName = "ScrollAction/Actions/Crouch")]
    public class CrouchAction : PlayerAction
    {
        public override string DisplayName => "しゃがみ";

        [Header("しゃがみ時の当たり判定 (BoxCollider2D の高さ・ローカル単位)")]
        // 縮小後のローカル高さ。.asset で設定する (例: 元1.0 → 0.5 で半分)
        [SerializeField] private float crouchedSizeY;

        // 元の BoxCollider2D サイズ・オフセット。初回 Tick でキャッシュし、復元時に使う。
        // SO は使い回されるので NonSerialized でランタイム保持
        [System.NonSerialized] private float originalSizeY = -1f;
        [System.NonSerialized] private float originalOffsetY;
        [System.NonSerialized] private bool currentlyShrunk;

        public override void OnFixedTick(PlayerActionContext ctx, int count)
        {
            var box = ctx.bodyCollider as BoxCollider2D;
            if (box == null) return;

            // 初回のみ元サイズを記憶
            if (originalSizeY < 0f)
            {
                originalSizeY = box.size.y;
                originalOffsetY = box.offset.y;
            }

            bool wantCrouch = ctx.crouchPressed && ctx.isGrounded;

            if (wantCrouch)
            {
                ApplyShrunkCollider(box);
                ctx.rb.linearVelocity = new Vector2(0f, ctx.rb.linearVelocity.y);
                ctx.isCrouching = true;
            }
            else if (currentlyShrunk)
            {
                RestoreCollider(box);
            }
        }

        public override void OnRespawn()
        {
            // 物理コライダーの復元はスナップショットを持ってないと出来ないので、フラグだけ落とす。
            // 次の Tick で wantCrouch=false になれば RestoreCollider が走る
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
            box.size = new Vector2(box.size.x, crouchedSizeY);
            box.offset = new Vector2(box.offset.x, bottomLocal + crouchedSizeY * 0.5f);
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
