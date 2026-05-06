using System;
using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// 壁キックアクション。空中で壁の近くにいる時にスペースを押すと発動。
    /// ウィンドアップ (壁張り付き) → キック実行 (vy=upSpeed, vx=壁反対方向) → アニメ完了まで
    /// フラグ保持の3段階状態機械で動く。キック実行までの間はジャンプ要求を消費するので、
    /// 同フレームで JumpAction が二重発火しないよう WallKickAction は JumpAction より前に Tick されることを前提にする。
    /// 壁検出は PlayerStats.groundLayer を流用 (壁=地形と同レイヤ前提)。
    /// </summary>
    [CreateAssetMenu(fileName = "WallKickAction", menuName = "ScrollAction/Actions/Wall Kick")]
    public class WallKickAction : PlayerAction
    {
        public override string DisplayName => "壁キック";
        public override string HelpText => "[Space] 空中で壁付近";

        /// <summary>壁キックの蹴り出しが実行された瞬間に発火。SE 等が購読する。</summary>
        public static event Action OnWallKicked;

        [Header("壁検出")]
        // プレイヤー中心から左右に伸ばすレイの長さ (units)。bodyCollider 半幅 + 余裕で設定する
        [SerializeField] private float wallCheckDistance;

        [Header("キック速度")]
        // 壁を蹴った瞬間に与える水平方向の速度 (units/sec)。壁とは反対方向に正の値で適用
        [SerializeField] private float kickHorizontalSpeed;

        // 壁を蹴った瞬間に与える上向きの速度 (units/sec)
        [SerializeField] private float kickVerticalSpeed;

        [Header("タイミング")]
        // ウィンドアップ時間 (sec)。この間は壁に張り付き、終了時にキック速度を適用する
        [SerializeField] private float windupDuration;

        // キック後にフラグ (アニメ用) を保持する時間 (sec)。蹴り出し直後〜着地前までを覆う想定
        [SerializeField] private float postKickDuration;

        // 状態。Idle=未発動、Windup=壁張り付き中、Kicked=キック実行済み (アニメ保持中)
        [System.NonSerialized] private Phase phase;
        [System.NonSerialized] private float elapsed;
        // 壁の方向 (-1 = 左壁、+1 = 右壁)。発動時に確定し、終了まで不変
        [System.NonSerialized] private float wallSide;

        private enum Phase { Idle, Windup, Kicked }

        public override void OnFixedTick(PlayerActionContext ctx, int count)
        {
            if (phase != Phase.Idle)
            {
                TickActive(ctx);
                return;
            }

            // 開始判定: 空中 + ジャンプ押下 + 壁の近く
            if (!ctx.jumpRequested) return;
            if (ctx.isGrounded) return;

            float side = DetectWall(ctx);
            if (side == 0f) return;

            StartWallKick(ctx, side);
        }

        public override void OnLanded() => Reset();
        public override void OnRespawn() => Reset();
        public override void OnSessionInit() => Reset();

        /// <summary>
        /// 壁キック発動: 状態を Windup に移し、壁張り付き速度を初期化。
        /// jumpRequested を消費して、同フレームで JumpAction が反応しないようにする。
        /// </summary>
        private void StartWallKick(PlayerActionContext ctx, float side)
        {
            wallSide = side;
            elapsed = 0f;
            phase = Phase.Windup;

            // 壁張り付き: 縦速度を 0 に固定、横は壁側にわずかに押し付けて離れないようにする
            ctx.rb.linearVelocity = new Vector2(wallSide * 0.1f, 0f);

            // 同フレームの JumpAction 二重発火を防止
            ctx.jumpRequested = false;
            ctx.isWallKicking = true;
            ctx.wallKickSide = wallSide;
        }

        /// <summary>
        /// 発動中の Tick: Windup 中は壁に張り付き、windupDuration 経過時にキック速度適用。
        /// Kicked フェーズは postKickDuration 経過 or 着地で終了。
        /// </summary>
        private void TickActive(PlayerActionContext ctx)
        {
            elapsed += Time.fixedDeltaTime;

            if (phase == Phase.Windup)
            {
                // 重力をキャンセルして壁張り付きを維持
                ctx.rb.linearVelocity = new Vector2(wallSide * 0.1f, 0f);

                if (elapsed >= windupDuration)
                {
                    // キック実行: 壁とは反対方向に水平速度、上向きに垂直速度
                    ctx.rb.linearVelocity = new Vector2(-wallSide * kickHorizontalSpeed, kickVerticalSpeed);
                    phase = Phase.Kicked;
                    OnWallKicked?.Invoke();
                }
            }
            else // Kicked
            {
                // 着地 or 規定時間経過で終了 (アニメ保持解除)
                if (ctx.isGrounded || elapsed >= windupDuration + postKickDuration)
                {
                    phase = Phase.Idle;
                    return;
                }
            }

            ctx.isWallKicking = true;
            ctx.wallKickSide = wallSide;
        }

        /// <summary>
        /// プレイヤー左右に Raycast し、壁が当たった側を返す。両側に壁がある場合は右優先。
        /// 戻り値: -1=左壁、+1=右壁、0=壁なし。
        /// </summary>
        private float DetectWall(PlayerActionContext ctx)
        {
            Vector2 origin = ctx.rb.position;
            LayerMask mask = ctx.stats.groundLayer;

            var hitR = Physics2D.Raycast(origin, Vector2.right, wallCheckDistance, mask);
            if (hitR.collider != null) return 1f;

            var hitL = Physics2D.Raycast(origin, Vector2.left, wallCheckDistance, mask);
            if (hitL.collider != null) return -1f;

            return 0f;
        }

        private void Reset()
        {
            phase = Phase.Idle;
            elapsed = 0f;
        }
    }
}
