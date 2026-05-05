using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// PlayerControllerの物理状態を読み取り、Animatorパラメータへ橋渡しするコンポーネント。
    /// Animator/SpriteRenderer をプレイヤーの子 GameObject に置く想定 (物理ルートの非一様スケールを視覚に持ち込まないため)。
    /// PlayerController 側の責務 (入力集約 + アクション委譲) を侵さないよう、ここは「読むだけ」に徹する。
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimatorBridge : MonoBehaviour
    {
        [SerializeField] private PlayerController controller;
        [SerializeField] private Rigidbody2D rb;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private float idleSpeedThreshold;

        private Animator animator;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");
        private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
        private static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");
        private static readonly int IsGlidingHash = Animator.StringToHash("IsGliding");

        void Awake()
        {
            animator = GetComponent<Animator>();
            if (controller == null) controller = GetComponentInParent<PlayerController>();
            if (rb == null) rb = GetComponentInParent<Rigidbody2D>();
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
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

            // 進行方向に応じて左右反転。微速時はバタつき防止で維持
            if (spriteRenderer != null && speed > idleSpeedThreshold)
            {
                spriteRenderer.flipX = vx < 0f;
            }
        }
    }
}
