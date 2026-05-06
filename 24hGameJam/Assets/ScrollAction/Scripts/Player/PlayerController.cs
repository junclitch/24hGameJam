using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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

        [Header("残機消費0時の遷移先 (Build Settings 登録済みであること)")]
        [SerializeField] private string gameOverSceneName;

        /// <summary>RespawnToStart が完了した直後に発火。CrumbleTile などのリセット先が購読する。</summary>
        public static event Action OnPlayerRespawned;

        // ゲーム開始時の位置。lastSafePosition の初期値 + フェールセーフ
        private Vector3 startPosition;

        // 直近で「幅のある地面に立っていた」位置。死亡時のリスポーン地点として使う
        private Vector3 lastSafePosition;

        // 入力 (Update側で集約しFixedUpdateで消化)
        private float inputX;
        private float facingDir = 1f;
        private bool jumpRequested;
        private bool evasionRequested;
        private bool crouchPressed;
        // ジャンプ系キーが押され続けているか (JetpackAction が読む長押し状態)
        private bool jetpackHeld;
        // F キー長押し状態 (GliderAction が読む)
        private bool gliderHeld;
        // Z キー押下フレーム (SlidingAction が読む。1フレームのみ true、FixedUpdate末尾でクリア)
        private bool slidingRequested;
        // C キー押下フレーム (WarpAction が読む。1フレームのみ true、FixedUpdate末尾でクリア)
        private bool warpRequested;
        // Q キー長押し継続入力 (RollingAction が読む)
        private bool rollingHeld;

        // 一時的な接地猶予。リスポーン時に立ち、ショップから離れた瞬間に解除
        private bool tempGroundCheckGrace;

        // FixedUpdateごとに使い回すコンテキスト (アロケーション抑制)
        private readonly PlayerActionContext ctx = new();

        public bool IsGrounded { get; private set; }

        // CrouchAction が今フレームしゃがみ動作を実行したか。AnimatorBridge が読み出す
        public bool IsCrouching { get; private set; }

        // GliderAction が今フレームグライド動作を実行したか。AnimatorBridge が読み出す
        public bool IsGliding { get; private set; }

        // SlidingAction が今フレーム滑走中か。AnimatorBridge が読み出す
        public bool IsSliding { get; private set; }

        // WallKickAction が今フレーム壁キック中 (ウィンドアップ〜キック後アニメ保持中) か。AnimatorBridge が読み出す
        public bool IsWallKicking { get; private set; }

        // 壁キック中の壁向き (-1=左壁、+1=右壁、0=非実行中)。AnimatorBridge が flipX 決定に使う
        public float WallKickSide { get; private set; }
        // WarpAction が今フレームワープ中か。AnimatorBridge が読み出す
        public bool IsWarping { get; private set; }

        // RollingAction が今フレーム転がり中か。AnimatorBridge が読み出す
        public bool IsRolling { get; private set; }

        // JetpackAction が今フレーム噴射中か。JetpackSE が Jet.wav の再生/停止判定に使う
        public bool IsJetpacking { get; private set; }

        // 接地判定を有効にするか (アクション所持 OR 一時猶予)
        private bool EffectiveHasGroundCheck =>
            (inventory != null && inventory.HasAny<GroundCheckAction>()) || tempGroundCheckGrace;

        void Awake()
        {
            if (rb == null) rb = GetComponent<Rigidbody2D>();
            if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();
            startPosition = transform.position;
            lastSafePosition = startPosition;

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

            // ジェットパック用の長押し継続入力。ジャンプキーと同経路だが押下フレームではなく isPressed で読む
            jetpackHeld = kb.spaceKey.isPressed || kb.upArrowKey.isPressed || kb.wKey.isPressed;

            // グライダー長押し入力 (F キー)
            gliderHeld = kb.fKey.isPressed;

            // スライディング発動入力 (Z キー押下フレーム)
            if (kb.zKey.wasPressedThisFrame)
                slidingRequested = true;

            // ワープ発動入力 (C キー押下フレーム)
            if (kb.cKey.wasPressedThisFrame)
                warpRequested = true;

            // 転がる長押し継続入力 (Q キー)
            rollingHeld = kb.qKey.isPressed;
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
            ctx.jetpackHeld = jetpackHeld;
            ctx.gliderHeld = gliderHeld;
            ctx.slidingRequested = slidingRequested;
            ctx.warpRequested = warpRequested;
            ctx.rollingHeld = rollingHeld;
            ctx.isGrounded = grounded;
            ctx.justLanded = justLanded;
            // 各 Tick で再判定するので毎フレーム false 起点にする
            ctx.isCrouching = false;
            ctx.isGliding = false;
            ctx.isSliding = false;
            ctx.isWallKicking = false;
            ctx.wallKickSide = 0f;
            ctx.isWarping = false;
            ctx.isRolling = false;
            ctx.isJetpacking = false;

            // 各アクションを順に処理
            foreach (var slot in inventory.owned)
            {
                if (slot?.action == null || slot.count <= 0) continue;
                slot.action.OnFixedTick(ctx, slot.count);
            }

            // CrouchAction が今フレーム発火していれば true。AnimatorBridge から読まれる
            IsCrouching = ctx.isCrouching;
            IsGliding = ctx.isGliding;
            IsSliding = ctx.isSliding;
            IsWallKicking = ctx.isWallKicking;
            WallKickSide = ctx.wallKickSide;
            IsWarping = ctx.isWarping;
            IsRolling = ctx.isRolling;
            IsJetpacking = ctx.isJetpacking;

            jumpRequested = false;
            evasionRequested = false;
            slidingRequested = false;
            warpRequested = false;
        }

        /// <summary>
        /// CheckpointTrigger から呼ばれる。リスポーン地点を上書きする。
        /// 進行方向に到達した時のみ呼び出されることを CheckpointTrigger 側が保証する想定。
        /// </summary>
        public void SetRespawnPoint(Vector3 worldPos)
        {
            lastSafePosition = worldPos;
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
        /// 残機を 1 消費して初期位置へリスポーン。残機が無く gameOverSceneName が設定されていれば GameOver シーンへ遷移。
        /// gameOverSceneName 未設定なら残機 0 でも従来通り無限リトライ (旧ステージとの後方互換)。
        /// 各アクションには OnRespawn を通知。接地判定アクション未所持時も一時的に地面と衝突
        /// できるよう猶予を立てる (Shopまで辿り着いて買い直す動線を確保するため、Shopから離れた瞬間に解除)。
        /// </summary>
        public void RespawnToStart()
        {
            bool consumed = inventory != null && inventory.ConsumeOne<LifeAction>();

            // 残機 0 + GameOver シーン設定済み → 遷移して終了
            if (!consumed && !string.IsNullOrEmpty(gameOverSceneName))
            {
                SceneManager.LoadScene(gameOverSceneName);
                return;
            }

            // 通常リスポーン (残機消費成功 or GameOver シーン未設定のフォールバック)
            // 直近の "安全地帯" に戻す。一度も安全地帯を通過していなければ Awake で startPosition が入っている
            rb.linearVelocity = Vector2.zero;
            transform.position = lastSafePosition;

            if (inventory != null)
                foreach (var slot in inventory.owned) slot.action?.OnRespawn();

            tempGroundCheckGrace = true;
            SyncCollisionMask();

            OnPlayerRespawned?.Invoke();
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
