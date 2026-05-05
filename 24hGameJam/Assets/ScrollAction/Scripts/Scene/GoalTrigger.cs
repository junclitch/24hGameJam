using System;
using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// ゴール判定用の Trigger Collider2D。プレイヤーが範囲内に入り、かつ地面に着地した時点で
    /// static event を発火し、受信側 (GameCycleManager) がシーン遷移を担当する。
    /// 「ジャンプで触っただけ」での誤発火を防ぐため接地条件を入れている。
    /// 同オブジェクトは1度発火したら再発火しない。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class GoalTrigger : MonoBehaviour
    {
        public static event Action OnGoalReached;

        // Trigger 範囲内に居る Player。Exit でクリアする
        private PlayerController insidePlayer;

        // 同一インスタンスでの再発火を防ぐ。受信側 (GameCycleManager) でも transitioning ガードしている
        private bool fired;

        void OnTriggerEnter2D(Collider2D other)
        {
            if (fired) return;
            // PlayerController を持つ相手だけ反応する。Tag 比較より型で識別する方が安全
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;
            insidePlayer = player;
        }

        void OnTriggerExit2D(Collider2D other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (player != null && insidePlayer == player) insidePlayer = null;
        }

        /// <summary>ゴール範囲内 かつ 接地中 になった瞬間にイベント発火。</summary>
        void Update()
        {
            if (fired || insidePlayer == null) return;
            if (!insidePlayer.IsGrounded) return;
            fired = true;
            OnGoalReached?.Invoke();
        }
    }
}
