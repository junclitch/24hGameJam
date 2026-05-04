using System;
using UnityEngine;

namespace MCPTest
{
    /// <summary>
    /// ブロック1個分のコンポーネント。
    /// ボールに当たると static イベント OnAnyDestroyed を発火し自分を破棄する。
    /// GameManager のことは知らない（イベント駆動のため）。
    /// </summary>
    public class Brick : MonoBehaviour
    {
        [SerializeField] private BrickType type;

        // どのブロックが壊れたかは関心外で、「何点入る」だけが必要なので int を渡す static event
        public static event Action<int> OnAnyDestroyed;

        // 全ブロックの生存数。ゼロになるとクリア判定に使われる
        public static int AliveCount { get; private set; }

        void Awake()
        {
            // 静的カウンタなので、生成時に+1、破棄時に-1 で増減を管理
            AliveCount++;

            // BrickType の色をスプライトに反映
            if (type != null && TryGetComponent<SpriteRenderer>(out var sr))
            {
                sr.color = type.color;
            }
        }

        void OnDestroy()
        {
            AliveCount--;
        }

        /// <summary>
        /// 衝突時に呼ばれるコールバック。
        /// Ball タグの相手と衝突したときだけ、得点イベントを発火して自分を破棄する。
        /// </summary>
        void OnCollisionEnter2D(Collision2D collision)
        {
            // FSMによるゲート: プレイ中以外はブロックは反応しない
            if (BreakoutGameManager.State != GameState.Playing) return;

            // Ball タグ以外は無視（パドルや壁の衝突は反応しない）
            if (!collision.gameObject.CompareTag("Ball")) return;

            // BrickType 未設定でもとりあえず100点で動くフォールバック
            int s = type != null ? type.score : 100;
            OnAnyDestroyed?.Invoke(s);
            Destroy(gameObject);
        }
    }
}
