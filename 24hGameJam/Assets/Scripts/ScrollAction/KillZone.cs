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
        void OnTriggerEnter2D(Collider2D other)
        {
            // PlayerController を持つ相手だけ反応する。Tag比較より型で識別する方が安全
            var player = other.GetComponentInParent<PlayerController>();
            if (player != null) player.RespawnToStart();
        }
    }
}
