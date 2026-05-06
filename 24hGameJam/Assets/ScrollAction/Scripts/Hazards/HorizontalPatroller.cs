using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// 2点間を水平往復させる汎用コンポーネント。
    /// 床上を滑る台車ノコのような「水平に動くだけのハザード」に使う。
    /// 当たり判定は別途 KillZone 等に任せる (このクラスは関知しない)。
    /// patrolStartX &lt; patrolEndX を想定し、両端で方向反転する。
    /// </summary>
    public class HorizontalPatroller : MonoBehaviour
    {
        [Header("水平往復 (ワールド x)")]
        [SerializeField] private float patrolStartX;
        [SerializeField] private float patrolEndX;

        [Header("往復速度 (units/sec)")]
        [SerializeField] private float patrolSpeed;

        // 進行方向 (+1=右、-1=左)。初期は右へ進む想定
        private int dir = 1;

        void Update()
        {
            var p = transform.position;
            p.x += dir * patrolSpeed * Time.deltaTime;
            if (p.x >= patrolEndX) { p.x = patrolEndX; dir = -1; }
            else if (p.x <= patrolStartX) { p.x = patrolStartX; dir = 1; }
            transform.position = p;
        }
    }
}
