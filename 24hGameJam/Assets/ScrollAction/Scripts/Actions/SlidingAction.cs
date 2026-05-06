using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// スライディングアクション。所持中に Z キーを押すと接地中のみ発動。
    /// 発動時の |vx| に倍率を掛けたスピードで滑り出し、地面摩擦で線形減速する。
    /// 線形減速のため停止距離は速度の2乗に比例 (d = v0^2 / (2 * decel))、
    /// 「横方向にスピードが出ているほど移動距離もスピードも伸びる」要件を満たす。
    /// 静止時 (|vx| ≈ 0) は staticBoostSpeed を初速としてブーストし、その場からも滑り出せる。
    /// 滑走中は BoxCollider2D を縦縮小し、横入力は無視する。
    /// 終了条件: 時間切れ / 速度下限割れ / 離地 (ジャンプ・段差落下を含む)。
    /// 横速度を毎 Tick 上書きするので、HorizontalMoveAction より後に Tick されることを前提とする。
    /// </summary>
    [CreateAssetMenu(fileName = "SlidingAction", menuName = "ScrollAction/Actions/Sliding")]
    public class SlidingAction : PlayerAction
    {
        public override string DisplayName => "スライディング";
        public override string HelpText => "[Z] 地上";

        [Header("初速")]
        // 移動中の発動時に |vx| に掛ける倍率 (慣性ブースト)
        [SerializeField] private float speedMultiplier;

        // 最低保証初速 (units/sec)。慣性ブーストがこの値より小さいときも常にこの速度で滑り出す
        [SerializeField] private float staticBoostSpeed;

        [Header("減速")]
        // 滑走中の水平減速度 (units/sec^2)。MoveTowards で 0 に寄せる
        [SerializeField] private float deceleration;

        // この速度を下回ったら終了 (units/sec)
        [SerializeField] private float minSpeedToContinue;

        [Header("継続時間")]
        // 最大滑走時間 (sec)。これを超えたら強制終了
        [SerializeField] private float maxDuration;

        [Header("当たり判定")]
        // 滑走中の BoxCollider2D 高さ (ローカル単位)。底面 (足元) を固定して縦だけ縮める
        [SerializeField] private float slidingSizeY;

        // 元のコライダー寸法。SO は使い回されるため NonSerialized で保持
        [System.NonSerialized] private float originalSizeY = -1f;
        [System.NonSerialized] private float originalOffsetY;
        [System.NonSerialized] private bool isActive;
        [System.NonSerialized] private float elapsed;
        // 開始時の初速 (絶対値)。OnFixedTick で elapsed と組み合わせて毎 Tick の速度を絶対計算
        [System.NonSerialized] private float startSpeed;
        // スライド方向 (-1 / +1)。StartSliding で確定し、滑走中は不変
        [System.NonSerialized] private float slideDir;

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

            if (!isActive)
            {
                if (ctx.slidingRequested && ctx.isGrounded)
                    StartSliding(ctx, box);
                else
                    return;
            }

            elapsed += Time.fixedDeltaTime;
            // 線形減速の絶対計算: HorizontalMoveAction による加減速の影響を毎 Tick 上書きで消す
            float currentSpeed = startSpeed - deceleration * elapsed;
            // 離地は段差落下とジャンプ両方をカバーする終了条件
            if (!ctx.isGrounded || elapsed >= maxDuration || currentSpeed < minSpeedToContinue)
            {
                EndSliding(box);
                return;
            }

            ctx.rb.linearVelocity = new Vector2(slideDir * currentSpeed, ctx.rb.linearVelocity.y);
            ctx.isSliding = true;
        }

        public override void OnRespawn()
        {
            // 物理コライダーは復元せず、フラグだけ落とす。
            // 次の Tick で StartSliding しなければ縮小もされないので問題ない (CrouchAction と同方針)
            isActive = false;
            elapsed = 0f;
        }

        public override void OnSessionInit()
        {
            originalSizeY = -1f;
            isActive = false;
            elapsed = 0f;
        }

        /// <summary>
        /// 滑走開始: 方向は「入力 → 慣性 → facingDir」の優先順で決定。
        /// 初速は慣性ブースト (|vx|×倍率) と staticBoostSpeed の大きい方を採用するので、
        /// Z+方向キー同時押しで HorizontalMove が一瞬だけ低速加速した状態でも
        /// 最低保証初速で滑り出せる。Z 単独 (入力なし・vx=0) なら facingDir に staticBoostSpeed。
        /// </summary>
        private void StartSliding(PlayerActionContext ctx, BoxCollider2D box)
        {
            float vx = ctx.rb.linearVelocity.x;

            if (Mathf.Abs(ctx.inputX) > 0.01f)
                slideDir = Mathf.Sign(ctx.inputX);
            else if (Mathf.Abs(vx) > 0.01f)
                slideDir = Mathf.Sign(vx);
            else
                slideDir = ctx.facingDir;

            startSpeed = Mathf.Max(Mathf.Abs(vx) * speedMultiplier, staticBoostSpeed);
            ctx.rb.linearVelocity = new Vector2(slideDir * startSpeed, ctx.rb.linearVelocity.y);

            ApplyShrunkCollider(box);
            elapsed = 0f;
            isActive = true;
            ctx.isSliding = true;
        }

        /// <summary>滑走終了: コライダー復元と内部状態クリア。</summary>
        private void EndSliding(BoxCollider2D box)
        {
            RestoreCollider(box);
            isActive = false;
            elapsed = 0f;
        }

        private void ApplyShrunkCollider(BoxCollider2D box)
        {
            float bottomLocal = originalOffsetY - originalSizeY * 0.5f;
            box.size = new Vector2(box.size.x, slidingSizeY);
            box.offset = new Vector2(box.offset.x, bottomLocal + slidingSizeY * 0.5f);
        }

        private void RestoreCollider(BoxCollider2D box)
        {
            box.size = new Vector2(box.size.x, originalSizeY);
            box.offset = new Vector2(box.offset.x, originalOffsetY);
        }
    }
}
