using System;
using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// 周期的に上下する落下鉄球。topY → bottomY を fallTime で落下、両端で wait。
    /// 物理シミュは使わずスクリプトで位置制御 (位相 phaseOffset で複数体をズラす設計)。
    /// 当たり判定は同 GameObject に取り付ける KillZone (Trigger Collider) に任せる。
    /// </summary>
    public class FallingBallHazard : MonoBehaviour
    {
        [Header("落下範囲 (ワールド y)")]
        [SerializeField] private float topY;
        [SerializeField] private float bottomY;

        [Header("タイミング (sec)")]
        // 落下に要する秒数
        [SerializeField] private float fallTime;
        // 上で待機する秒数 (= 次の落下までの予告時間)
        [SerializeField] private float waitAtTop;
        // 下で待機する秒数 (着地直後の余韻)
        [SerializeField] private float waitAtBottom;
        // 周期の中での開始ズレ。複数体を別タイミングで落とす時に使う
        [SerializeField] private float phaseOffset;

        // Awake 時の Time.time。cycle 計算をインスタンス起動からの経過時間で行うため。
        // Time.time はグローバル累積でリスタートしてもリセットされないので、これを使わないと
        // シーン再読込のたびに球の phase がランダムになってしまう (見えない瞬間が生じる)
        private float startTime;

        // 落下→着地 (bottom wait 入り) の瞬間を 1 回だけ検出するためのフラグ
        private bool wasFalling;

        /// <summary>落下から着地に切替わった瞬間に発火。SE 等が距離ベース音量計算で購読する。位置は球の世界座標。</summary>
        public static event Action<Vector3> OnImpact;

        void Awake()
        {
            startTime = Time.time;
            // 初期表示位置を topY に固定 (Update 1回目までの絵がブレないように)
            var p = transform.position;
            p.y = topY;
            transform.position = p;
        }

        void Update()
        {
            float total = fallTime + waitAtTop + waitAtBottom;
            if (total <= 0f) return;
            float t = (Time.time - startTime + phaseOffset) % total;

            float y;
            bool falling = false;
            bool impacted = false;
            if (t < waitAtTop)
            {
                // 上で待機
                y = topY;
            }
            else if (t < waitAtTop + fallTime)
            {
                // 落下中。t² で重力的な加速感
                float k = (t - waitAtTop) / fallTime;
                y = Mathf.Lerp(topY, bottomY, k * k);
                falling = true;
            }
            else
            {
                // 下で待機 (着地)
                y = bottomY;
                impacted = true;
            }

            // 落下中 → 着地に切替わった瞬間だけ OnImpact を発火 (SE 重複防止)
            if (wasFalling && impacted) OnImpact?.Invoke(transform.position);
            wasFalling = falling;

            var p = transform.position;
            p.y = y;
            transform.position = p;
        }
    }
}
