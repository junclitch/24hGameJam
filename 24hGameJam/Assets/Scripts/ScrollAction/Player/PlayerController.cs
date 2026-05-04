using UnityEngine;
using UnityEngine.InputSystem;

namespace ScrollAction
{
    /// <summary>
    /// プレイヤー操作担当。新Input Systemで矢印/WASDの横入力とSpaceでのジャンプを扱う。
    /// 数値はすべて PlayerStats から読み、本クラスは数値を持たない。
    /// 落下死は外部の KillZone から RespawnToStart() を呼んでもらう（疎結合）。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private PlayerStats stats;

        // 接地判定の中心点。Player足元に置いた子Transformを差す
        [SerializeField] private Transform groundCheck;

        [SerializeField] private Rigidbody2D rb;

        // ゲーム開始時の位置。リスポーン時にここへ戻す
        private Vector3 startPosition;

        // 入力された水平方向 (-1..1)。FixedUpdate で物理に反映
        private float inputX;

        // 1フレーム内に押されたジャンプ要求。FixedUpdate で消化する
        private bool jumpRequested;

        // 現在接地しているか
        public bool IsGrounded { get; private set; }

        void Awake()
        {
            if (rb == null) rb = GetComponent<Rigidbody2D>();
            startPosition = transform.position;
        }

        /// <summary>毎フレーム入力を読む。物理は FixedUpdate に任せる。</summary>
        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) { inputX = 0f; return; }

            float h = 0f;
            if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) h -= 1f;
            if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) h += 1f;
            inputX = h;

            // wasPressedThisFrame は Update で見ないと取りこぼす
            if (kb.spaceKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)
                jumpRequested = true;
        }

        /// <summary>接地判定→水平速度の更新→ジャンプ消化の順に物理を進める。</summary>
        void FixedUpdate()
        {
            if (stats == null) return;

            IsGrounded = groundCheck != null
                && Physics2D.OverlapCircle(groundCheck.position, stats.groundCheckRadius, stats.groundLayer);

            // 目標速度に向けて加減速。空中でも操作を許す（マリオ的フィール）
            float targetVx = inputX * stats.walkSpeed;
            float newVx = Mathf.MoveTowards(rb.linearVelocity.x, targetVx, stats.acceleration * Time.fixedDeltaTime);
            rb.linearVelocity = new Vector2(newVx, rb.linearVelocity.y);

            if (jumpRequested && IsGrounded)
            {
                // y速度を上書きすることで、落下中の踏切でも常に同じジャンプ高になる
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, stats.jumpForce);
            }
            jumpRequested = false;
        }

        /// <summary>初期位置へワープし、慣性をリセットする。落下死などから呼ぶ。</summary>
        public void RespawnToStart()
        {
            rb.linearVelocity = Vector2.zero;
            transform.position = startPosition;
        }

        // 接地判定の可視化（エディタ用）
        void OnDrawGizmosSelected()
        {
            if (groundCheck == null || stats == null) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(groundCheck.position, stats.groundCheckRadius);
        }
    }
}
