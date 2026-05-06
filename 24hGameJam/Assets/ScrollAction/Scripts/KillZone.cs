using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// 落下死ゾーン。プレイヤーが触れたら RespawnToStart() を呼んで初期位置に戻す。
    /// Trigger Collider を持つ GameObject にアタッチして、レベル下の地獄に伸ばしておく想定。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class KillZone : MonoBehaviour
    {
        /// <summary>
        /// コンポーネント追加時に Collider2D を Trigger 化する。
        /// 非 Trigger だと OnTriggerEnter2D が発火せず、ただの "立てる床" として機能してしまうのを防ぐ。
        /// </summary>
        void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            // PlayerController を持つ相手だけ反応する。Tag比較より型で識別する方が安全
            var player = other.GetComponentInParent<PlayerController>();
            if (player != null) player.RespawnToStart();
        }
    }
}
