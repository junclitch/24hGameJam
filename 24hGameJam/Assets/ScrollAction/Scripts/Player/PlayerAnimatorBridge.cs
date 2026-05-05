using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// PlayerControllerの物理状態を読み取り、Animatorパラメータへ橋渡しするコンポーネント。
    /// Animator はプレイヤーの子 GameObject (Visual) に置く想定。SpriteRenderer はさらにその子 (Spinner) に置く。
    /// Visual に counter-scale を寄せ、回転は Spinner (単位スケール) で行うことで、
    /// 親 (Player) の非一様スケールが回転と合成されてシアー (絵の比率変化) を起こすのを避ける。
    /// PlayerController 側の責務 (入力集約 + アクション委譲) を侵さないよう、ここは「読むだけ」に徹する。
    /// 例外: RollingLoop ステート中は目の位置を不動点にする回転を LateUpdate で Spinner に適用する
    /// (.anim には盛り込まないので、左右反転 (flipX) に応じて回転方向と x 補正の符号を切り替えられる)。
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimatorBridge : MonoBehaviour
    {
        [SerializeField] private PlayerController controller;
        [SerializeField] private Rigidbody2D rb;
        // SpriteRenderer は Spinner 子オブジェクト側にある。flipX のためここで参照を握る
        [SerializeField] private SpriteRenderer spriteRenderer;
        // Spinner Transform (回転と位置補正の対象)。Awake で子から自動取得
        [SerializeField] private Transform spinner;
        [SerializeField] private float idleSpeedThreshold;

        [Header("転がり回転 (RollingLoop ステート専用)")]
        // 1 周にかける時間 (sec)。アニメ側 stopTime と合わせて運用する想定
        [SerializeField] private float rollingLoopDuration;
        // 不動点 (目) の sprite-local オフセット (pivot からの距離、world units)。
        // x: 右向き時の目位置の x オフセット。flipX=true (左向き) のとき符号が反転して扱われる。
        // y: 目の高さ (pivot 足元からの距離)。
        [SerializeField] private Vector2 rollingEyeOffsetSprite;

        private Animator animator;
        // 前フレームの IsRolling 値。立ち上がり (false→true) の検出に使う
        private bool prevIsRolling;
        private static readonly int RollingLoopStateHash = Animator.StringToHash("RollingLoop");

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");
        private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
        private static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");
        private static readonly int IsGlidingHash = Animator.StringToHash("IsGliding");
        private static readonly int IsSlidingHash = Animator.StringToHash("IsSliding");
        private static readonly int IsWallKickingHash = Animator.StringToHash("IsWallKicking");
        private static readonly int IsWarpingHash = Animator.StringToHash("IsWarping");
        private static readonly int IsRollingHash = Animator.StringToHash("IsRolling");
        private static readonly int RollingTriggerHash = Animator.StringToHash("RollingTrigger");

        void Awake()
        {
            animator = GetComponent<Animator>();
            if (controller == null) controller = GetComponentInParent<PlayerController>();
            if (rb == null) rb = GetComponentInParent<Rigidbody2D>();
            if (spinner == null)
            {
                var t = transform.Find("Spinner");
                if (t != null) spinner = t;
            }
            if (spriteRenderer == null && spinner != null) spriteRenderer = spinner.GetComponent<SpriteRenderer>();
        }

        void Update()
        {
            if (controller == null || rb == null) return;

            float vx = rb.linearVelocity.x;
            float vy = rb.linearVelocity.y;
            float speed = Mathf.Abs(vx);

            animator.SetFloat(SpeedHash, speed);
            animator.SetFloat(VerticalSpeedHash, vy);
            animator.SetBool(IsGroundedHash, controller.IsGrounded);
            animator.SetBool(IsCrouchingHash, controller.IsCrouching);
            animator.SetBool(IsGlidingHash, controller.IsGliding);
            animator.SetBool(IsSlidingHash, controller.IsSliding);
            animator.SetBool(IsWallKickingHash, controller.IsWallKicking);
            animator.SetBool(IsWarpingHash, controller.IsWarping);
            animator.SetBool(IsRollingHash, controller.IsRolling);

            // RollingStart へは立ち上がりエッジのみ発火させる Trigger 経由で入る。
            // Bool だと Any State 遷移が IsRolling=true の間ずっと再発火し、Loop に着いた瞬間 Start に戻されてしまうため。
            if (controller.IsRolling && !prevIsRolling)
            {
                animator.SetTrigger(RollingTriggerHash);
            }
            prevIsRolling = controller.IsRolling;

            // 壁キック中は wallKickSide で flipX を決める。
            // wall-jump 原画は「右壁を蹴って左に飛ぶ」向きなので、左壁時 (wallKickSide<0) のみ反転して右向きにする。
            // 通常時は進行方向ベース。微速時はバタつき防止で維持
            if (spriteRenderer != null)
            {
                if (controller.IsWallKicking)
                    spriteRenderer.flipX = controller.WallKickSide < 0f;
                else if (speed > idleSpeedThreshold)
                    spriteRenderer.flipX = vx < 0f;
            }
        }

        /// <summary>
        /// Spinner の transform を毎フレーム支配する。
        /// RollingLoop ステート中: 目の位置を世界座標で不動点にしたまま回転させる (facing に応じて方向反転)。
        /// それ以外: rotation/position を 0 にリセット。Animator 任せだと遷移後に値が残ったままになるので script が確実に戻す。
        /// </summary>
        void LateUpdate()
        {
            if (animator == null || spinner == null) return;
            var current = animator.GetCurrentAnimatorStateInfo(0);
            bool inLoop = current.shortNameHash == RollingLoopStateHash;

            // 抜け遷移中 (Loop → End) は Loop ではないとみなしてリセット側に倒す。
            // 入り遷移中 (Start → Loop) で「次が Loop」なら回転を始めてもよい。
            if (inLoop && animator.IsInTransition(0))
            {
                var next = animator.GetNextAnimatorStateInfo(0);
                if (next.shortNameHash != RollingLoopStateHash) inLoop = false;
            }

            if (!inLoop)
            {
                spinner.localPosition = Vector3.zero;
                spinner.localEulerAngles = Vector3.zero;
                return;
            }

            float normTime = current.normalizedTime;
            normTime -= Mathf.Floor(normTime); // [0, 1) にラップ
            float psiDeg = normTime * 360f;
            float psiRad = psiDeg * Mathf.Deg2Rad;

            // facing right なら sign=+1 (時計回り)、left なら sign=-1 (反時計回り)
            float sign = (spriteRenderer != null && spriteRenderer.flipX) ? -1f : +1f;
            float ex = rollingEyeOffsetSprite.x;
            float ey = rollingEyeOffsetSprite.y;
            float cos = Mathf.Cos(psiRad);
            float sin = Mathf.Sin(psiRad);

            // 右向き時の補正式: vx = ex(1-cos) - ey sin, vy = ey(1-cos) + ex sin, rotZ = -psi
            // (Spinner はデフォルト localPos=(0,0) なので vyConst = ey になる)
            // 左向き (flipX): eye の x 反転 + 回転方向反転で vx の符号反転。vy は不変。
            float locX = sign * (ex * (1f - cos) - ey * sin);
            float locY = ey * (1f - cos) + ex * sin;
            spinner.localPosition = new Vector3(locX, locY, 0f);
            spinner.localEulerAngles = new Vector3(0f, 0f, -sign * psiDeg);
        }
    }
}
