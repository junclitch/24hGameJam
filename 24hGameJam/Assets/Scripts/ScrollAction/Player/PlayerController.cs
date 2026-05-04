using UnityEngine;
using UnityEngine.InputSystem;

namespace ScrollAction
{
    /// <summary>
    /// プレイヤー操作担当。新Input Systemで矢印/WASDの横入力、Spaceでジャンプ、左Shiftでダッシュ。
    /// 各能力は ActionInventory のフラグで個別にON/OFFできる。売却すると対応能力が即座に効かなくなる。
    /// 数値はすべて PlayerStats から読み、本クラスは数値を持たない。
    /// 落下死は外部の KillZone から RespawnToStart() を呼んでもらう (疎結合)。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private PlayerStats stats;

        // 所持アクションの参照。シーンを跨いで共有されるアセットを差す
        [SerializeField] private ActionInventory inventory;

        // 接地判定の中心点。Player足元に置いた子Transformを差す
        [SerializeField] private Transform groundCheck;

        [SerializeField] private Rigidbody2D rb;

        // ゲーム開始時の位置。リスポーン時にここへ戻す
        private Vector3 startPosition;

        // 入力された水平方向 (-1..1)。FixedUpdate で物理に反映
        private float inputX;

        // 1フレーム内に押されたジャンプ要求。FixedUpdate で消化する
        private bool jumpRequested;

        // 1フレーム内に押されたダッシュ要求。FixedUpdate で消化する
        private bool dashRequested;

        // 最後に向いていた方向 (-1 か 1)。入力ゼロ時のダッシュ方向に使う
        private float facingDir = 1f;

        // 着地後に消費したジャンプ回数。接地で 0 にリセット
        private int jumpsUsed;

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

            // 入力自体は常に読む。歩行に反映するかは hasHorizontalMove で後段ガード
            // (ダッシュ方向には左右移動を持っていなくても使えるようにしたいため)
            float h = 0f;
            if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) h -= 1f;
            if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) h += 1f;
            inputX = h;

            if (Mathf.Abs(h) > 0.01f) facingDir = Mathf.Sign(h);

            // wasPressedThisFrame は Update で見ないと取りこぼす
            if (kb.spaceKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)
                jumpRequested = true;

            if (kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame)
                dashRequested = true;
        }

        /// <summary>
        /// 接地判定→水平速度の更新→ジャンプ/ダッシュ消化の順に物理を進める。
        /// 接地判定アクションを売却している場合は IsGrounded を常に false に固定し、
        /// ジャンプ回数のリセットも発生しなくなる (=空中リソース管理が必要なゲーム性になる)。
        /// </summary>
        void FixedUpdate()
        {
            if (stats == null || inventory == null) return;

            bool grounded = inventory.hasGroundCheck
                && groundCheck != null
                && Physics2D.OverlapCircle(groundCheck.position, stats.groundCheckRadius, stats.groundLayer);

            if (grounded && !IsGrounded) jumpsUsed = 0;
            IsGrounded = grounded;

            UpdateHorizontalVelocity();
            TryConsumeJump();
            TryConsumeDash();

            jumpRequested = false;
            dashRequested = false;
        }

        /// <summary>
        /// 入力方向への横移動を物理に反映。hasHorizontalMove が無ければ自発的な歩行はしない。
        /// hasAccelDecel が無い場合は加減速曲線を捨てて目標速度に即セットする (慣性が消えてカクッとした動きになる)。
        /// </summary>
        private void UpdateHorizontalVelocity()
        {
            if (!inventory.hasHorizontalMove) return;

            float targetVx = inputX * stats.walkSpeed;
            float newVx = inventory.hasAccelDecel
                ? Mathf.MoveTowards(rb.linearVelocity.x, targetVx, stats.acceleration * Time.fixedDeltaTime)
                : targetVx;
            rb.linearVelocity = new Vector2(newVx, rb.linearVelocity.y);
        }

        /// <summary>
        /// ジャンプ要求を消化する。総ジャンプ可能回数 = hasJump + hasDoubleJump の真偽値合計。
        /// 例: ジャンプを売却して "ジャンプ+1" だけ買うと、合計1回ジャンプできる。
        /// </summary>
        private void TryConsumeJump()
        {
            if (!jumpRequested) return;

            int maxJumps = (inventory.hasJump ? 1 : 0) + (inventory.hasDoubleJump ? 1 : 0);
            if (jumpsUsed >= maxJumps) return;

            // y速度を上書きすることで、落下中の踏切でも常に同じジャンプ高になる
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, stats.jumpForce);
            jumpsUsed++;
        }

        /// <summary>
        /// ダッシュ要求を消化する。入力方向が0の時は最後に向いていた方向へ撃つ。
        /// 左右移動アクションを持っていなくてもダッシュ単体で水平移動が可能 (仕様)。
        /// </summary>
        private void TryConsumeDash()
        {
            if (!dashRequested || !inventory.hasDash) return;

            float dir = Mathf.Abs(inputX) > 0.01f ? Mathf.Sign(inputX) : facingDir;
            rb.linearVelocity = new Vector2(dir * stats.dashSpeed, rb.linearVelocity.y);
        }

        /// <summary>初期位置へワープし、慣性とジャンプ消費数をリセットする。落下死などから呼ぶ。</summary>
        public void RespawnToStart()
        {
            rb.linearVelocity = Vector2.zero;
            transform.position = startPosition;
            jumpsUsed = 0;
        }

        // 接地判定の可視化 (エディタ用)
        void OnDrawGizmosSelected()
        {
            if (groundCheck == null || stats == null) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(groundCheck.position, stats.groundCheckRadius);
        }
    }
}
