using UnityEngine;
using UnityEngine.InputSystem;

namespace ScrollAction
{
    /// <summary>
    /// プレイヤーのオーケストレータ。新Input System で入力を集約し、
    /// 物理量を ActionInventory が持つ各 PlayerAction に委譲する。
    /// 自身は「接地判定の実行」「コリジョンマスク同期」「リスポーン」「ライフサイクルフックの呼出」しか担当しない。
    /// 各アクション固有のロジック (歩行・ジャンプ・回避) は対応する PlayerAction サブクラスへ。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private PlayerStats stats;
        [SerializeField] private ActionInventory inventory;
        [SerializeField] private Transform groundCheck;
        [SerializeField] private Rigidbody2D rb;
        [SerializeField] private Collider2D bodyCollider;

        // ゲーム開始時の位置。リスポーン時にここへ戻す
        private Vector3 startPosition;

        // 入力 (Update側で集約しFixedUpdateで消化)
        private float inputX;
        private float facingDir = 1f;
        private bool jumpRequested;
        private bool evasionRequested;
        private bool crouchPressed;

        // 一時的な接地猶予。リスポーン時に立ち、ショップから離れた瞬間に解除
        private bool tempGroundCheckGrace;

        // FixedUpdateごとに使い回すコンテキスト (アロケーション抑制)
        private readonly PlayerActionContext ctx = new();

        public bool IsGrounded { get; private set; }

        // CrouchAction が今フレームしゃがみ動作を実行したか。AnimatorBridge が読み出す
        public bool IsCrouching { get; private set; }

        // 接地判定を有効にするか (アクション所持 OR 一時猶予)
        private bool EffectiveHasGroundCheck =>
            (inventory != null && inventory.HasAny<GroundCheckAction>()) || tempGroundCheckGrace;

        void Awake()
        {
            if (rb == null) rb = GetComponent<Rigidbody2D>();
            if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();
            startPosition = transform.position;

            // ゲーム起動時に1度だけ所持品を初期値へ戻す
            if (inventory != null) inventory.EnsureInitializedThisSession();
        }

        void OnEnable()
        {
            if (inventory != null) inventory.OnInventoryChanged += SyncCollisionMask;
            Shop.OnPlayerExitedShop += HandleShopExit;
            SyncCollisionMask();
        }

        void OnDisable()
        {
            if (inventory != null) inventory.OnInventoryChanged -= SyncCollisionMask;
            Shop.OnPlayerExitedShop -= HandleShopExit;
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
            if (Mathf.Abs(h) > 0.01f) facingDir = Mathf.Sign(h);

            if (kb.spaceKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)
                jumpRequested = true;
            if (kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame)
                evasionRequested = true;

            // しゃがみは「押している間」の継続入力 (ジャンプ/回避と違い wasPressedThisFrame ではない)
            crouchPressed = kb.downArrowKey.isPressed || kb.sKey.isPressed;
        }

        /// <summary>
        /// 接地判定 → 着地イベント → 各アクションのTick の順で物理を進める。
        /// 個別ロジックは PlayerAction 側にある。
        /// </summary>
        void FixedUpdate()
        {
            if (stats == null || inventory == null) return;

            bool grounded = EffectiveHasGroundCheck
                && groundCheck != null
                && Physics2D.OverlapCircle(groundCheck.position, stats.groundCheckRadius, stats.groundLayer);

            bool justLanded = grounded && !IsGrounded;
            IsGrounded = grounded;

            // 着地イベントを各アクションへ通知 (空中リソースのリセットなど)
            if (justLanded)
            {
                foreach (var slot in inventory.owned) slot.action?.OnLanded();
            }

            // 共有コンテキストを準備
            ctx.rb = rb;
            ctx.groundCheck = groundCheck;
            ctx.bodyCollider = bodyCollider;
            ctx.stats = stats;
            ctx.inventory = inventory;
            ctx.inputX = inputX;
            ctx.facingDir = facingDir;
            ctx.jumpRequested = jumpRequested;
            ctx.evasionRequested = evasionRequested;
            ctx.crouchPressed = crouchPressed;
            ctx.isGrounded = grounded;
            ctx.justLanded = justLanded;
            // 各 Tick で再判定するので毎フレーム false 起点にする
            ctx.isCrouching = false;

            // 各アクションを順に処理
            foreach (var slot in inventory.owned)
            {
                if (slot?.action == null || slot.count <= 0) continue;
                slot.action.OnFixedTick(ctx, slot.count);
            }

            // CrouchAction が今フレーム発火していれば true。AnimatorBridge から読まれる
            IsCrouching = ctx.isCrouching;

            jumpRequested = false;
            evasionRequested = false;
        }

        /// <summary>
        /// 接地判定アクションの所持 (or 一時猶予) に応じて Player コライダーの地面レイヤ衝突可否を切替える。
        /// 売却+猶予なし時は地面レイヤを excludeLayers に積み、地面をすり抜けるようにする。
        /// </summary>
        private void SyncCollisionMask()
        {
            if (bodyCollider == null || stats == null || inventory == null) return;
            bodyCollider.excludeLayers = EffectiveHasGroundCheck ? default : stats.groundLayer;
        }

        /// <summary>
        /// プレイヤーがショップから離れた時に呼ばれる。リスポーン時に与えた接地猶予をここで解除する。
        /// 接地判定アクションを所持していなければ、以降は地面をすり抜けるようになる (仕様)。
        /// </summary>
        private void HandleShopExit()
        {
            if (!tempGroundCheckGrace) return;
            tempGroundCheckGrace = false;
            SyncCollisionMask();
        }

        /// <summary>
        /// 初期位置へワープし、慣性をリセット。各アクションへも OnRespawn を通知する。
        /// 接地判定アクション未所持の状態でも一時的に地面と衝突するよう猶予を立てる
        /// (Shopまで辿り着いて買い直す動線を確保するため。Shopから離れた瞬間に解除)。
        /// </summary>
        public void RespawnToStart()
        {
            rb.linearVelocity = Vector2.zero;
            transform.position = startPosition;

            if (inventory != null)
                foreach (var slot in inventory.owned) slot.action?.OnRespawn();

            tempGroundCheckGrace = true;
            SyncCollisionMask();
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
