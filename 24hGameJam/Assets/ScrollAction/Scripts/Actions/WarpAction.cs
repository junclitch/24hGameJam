using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// ワープアクション。所持中に C 押下フレームで発動。
    /// 発動中は当たり判定 (bodyCollider) を無効化し、重力スケール 0・速度 0 に張り付けて落下しないようにする。
    /// teleportFraction 経過時点で進行方向 (facingDir) に warpDistance 先へ位置を瞬間スワップし、
    /// 残り時間でアニメ後半 (出現演出) を見せ切ってから物理状態を復元する。
    /// 物理復元はキャッシュした rb / collider 経由で行うので、
    /// OnRespawn が ctx なしで呼ばれても確実に元に戻せる (放置で衝突無効が残るのを防ぐ)。
    /// </summary>
    [CreateAssetMenu(fileName = "WarpAction", menuName = "ScrollAction/Actions/Warp")]
    public class WarpAction : PlayerAction
    {
        public override string DisplayName => "ワープ";

        [Header("ワープ")]
        // 進行方向に瞬間移動する距離 (units)
        [SerializeField] private float warpDistance;

        // 当たり判定無効・落下禁止が継続する全体時間 (sec)。アニメ全長と合わせる
        [SerializeField] private float warpDuration;

        // テレポート発生タイミング (0..1)。0.5 で中間、前半=出発側演出、後半=到着側演出
        [SerializeField] private float teleportFraction;

        [System.NonSerialized] private bool isActive;
        [System.NonSerialized] private bool teleported;
        [System.NonSerialized] private float elapsed;
        [System.NonSerialized] private float originalGravityScale;
        [System.NonSerialized] private Vector2 targetPosition;

        // ctx 無しで呼ばれる OnRespawn から物理復元するため、開始時に参照を握っておく
        [System.NonSerialized] private Rigidbody2D activeRb;
        [System.NonSerialized] private Collider2D activeCollider;

        public override void OnFixedTick(PlayerActionContext ctx, int count)
        {
            if (!isActive)
            {
                if (ctx.warpRequested) StartWarp(ctx);
                else return;
            }

            ctx.isWarping = true;
            elapsed += Time.fixedDeltaTime;

            // 中間時点で位置スワップ (当たり判定無効中なので壁抜けで出現可能)
            if (!teleported && elapsed >= warpDuration * teleportFraction)
            {
                ctx.rb.position = targetPosition;
                teleported = true;
            }

            // 落下禁止: 毎 Tick 速度を 0 に張り付ける (gravityScale=0 と二重保険)
            ctx.rb.linearVelocity = Vector2.zero;

            if (elapsed >= warpDuration)
                EndWarp();
        }

        public override void OnRespawn()
        {
            if (isActive) RestorePhysics();
            ResetState();
        }

        public override void OnSessionInit() => ResetState();

        /// <summary>ワープ開始。物理状態をスナップショットし、collider/gravity を無効化して target を計算。</summary>
        private void StartWarp(PlayerActionContext ctx)
        {
            activeRb = ctx.rb;
            activeCollider = ctx.bodyCollider;
            originalGravityScale = ctx.rb.gravityScale;

            ctx.rb.gravityScale = 0f;
            ctx.rb.linearVelocity = Vector2.zero;
            if (ctx.bodyCollider != null) ctx.bodyCollider.enabled = false;

            targetPosition = ctx.rb.position + new Vector2(ctx.facingDir * warpDistance, 0f);

            isActive = true;
            teleported = false;
            elapsed = 0f;
            ctx.isWarping = true;
        }

        /// <summary>正常終了: 物理復元 + 内部状態クリア。</summary>
        private void EndWarp()
        {
            RestorePhysics();
            ResetState();
        }

        /// <summary>キャッシュした rb / collider に対して collider 有効化と gravityScale 復元を行う。</summary>
        private void RestorePhysics()
        {
            if (activeRb != null) activeRb.gravityScale = originalGravityScale;
            if (activeCollider != null) activeCollider.enabled = true;
            activeRb = null;
            activeCollider = null;
        }

        private void ResetState()
        {
            isActive = false;
            teleported = false;
            elapsed = 0f;
        }
    }
}
