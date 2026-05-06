using System;
using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// ゴール判定用の Trigger Collider2D。プレイヤーが Trigger 範囲に入った瞬間に
    /// static event を発火し、受信側 (GameCycleManager) がシーン遷移を担当する。
    /// 元々は「接地している時のみ発火」というガードを入れていたが、
    /// WebGL ビルドで FPS / 物理ステップの差により接地フラグが立たないフレームを跨いで通過してしまい
    /// クリア判定が出ない事故が発生したため、シンプルに OnTriggerEnter2D で即発火する仕様に変更した。
    /// 同一インスタンスは 1 度だけ発火する (fired ガード)。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class GoalTrigger : MonoBehaviour
    {
        public static event Action OnGoalReached;

        // 同一インスタンスでの再発火を防ぐ。受信側 (GameCycleManager) でも transitioning ガードしている
        private bool fired;

        void OnTriggerEnter2D(Collider2D other)
        {
            if (fired) return;
            // PlayerController を持つ相手だけ反応する。Tag 比較より型で識別する方が安全
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;
            fired = true;
            OnGoalReached?.Invoke();
        }
    }
}
