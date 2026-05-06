using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// 空中限定の横移動制御。空中 (isGrounded=false) で入力方向×airSpeed へ加減速する。
    /// 地上は HorizontalMoveAction が担当。両者が同時に vx を上書きすることはない (互いに排他ガード)。
    /// パラメータを SO 側で個別設定できるので、地上と空中で「速度」「加速度」を別チューニング可能
    /// (例: 空中は地上より滑る、空中の方が遅い、など)。
    /// </summary>
    [CreateAssetMenu(fileName = "AirControlAction", menuName = "ScrollAction/Actions/Air Control")]
    public class AirControlAction : PlayerAction
    {
        public override string DisplayName => "空中制御";
        public override string HelpText => "[← → / A / D] 空中";

        [Header("空中横移動")]
        // 空中での横方向最大速度 (units/sec)。.asset で設定。地上 walkSpeed と独立
        [SerializeField] private float airSpeed;

        // 空中での加減速 (units/sec^2)。.asset で設定。地上 acceleration より低めにすると "滑る" 感が出る
        [SerializeField] private float airAcceleration;

        // GetContacts 結果バッファ (アロケーション抑制)
        private static readonly ContactPoint2D[] ContactsBuffer = new ContactPoint2D[16];

        // 法線が壁と見なせる水平度の閾値 (|normal.x| がこれ以上なら "壁")。45° 弱までを壁扱い
        private const float WallNormalThreshold = 0.7f;

        public override void OnFixedTick(PlayerActionContext ctx, int count)
        {
            if (ctx.isGrounded) return;

            float targetVx = ctx.inputX * airSpeed;

            // 壁に押し付け続けると、解決される法線力 × 既定摩擦 (μ=0.4) で縦速度が削られて "引っかかる"。
            // 物理エンジンが既に解決済みの "実接触" を見て、入力方向側に壁接触があれば targetVx=0 にする。
            // 法線力を立てなければ摩擦は発生しない
            if (targetVx != 0f && IsTouchingWallInDirection(ctx, Mathf.Sign(targetVx)))
                targetVx = 0f;

            float newVx = Mathf.MoveTowards(ctx.rb.linearVelocity.x, targetVx, airAcceleration * Time.fixedDeltaTime);
            ctx.rb.linearVelocity = new Vector2(newVx, ctx.rb.linearVelocity.y);
        }

        /// <summary>
        /// Rigidbody2D の現在接触点を走査し、入力方向に壁との接触があれば true を返す。
        /// ContactPoint2D.normal は「相手→自分」向きなので、右壁との接触なら normal.x &lt; 0、
        /// 左壁との接触なら normal.x &gt; 0。すなわち normal.x の符号と入力符号が逆の時その方向に壁あり。
        /// レイヤー指定や raycast 精度に依存せず、物理エンジンが解決した接触をそのまま使うので最も確実。
        /// </summary>
        private bool IsTouchingWallInDirection(PlayerActionContext ctx, float sign)
        {
            int n = ctx.rb.GetContacts(ContactsBuffer);
            for (int i = 0; i < n; i++)
            {
                Vector2 normal = ContactsBuffer[i].normal;
                if (Mathf.Abs(normal.x) < WallNormalThreshold) continue;
                if (Mathf.Sign(normal.x) != Mathf.Sign(sign)) return true;
            }
            return false;
        }
    }
}
