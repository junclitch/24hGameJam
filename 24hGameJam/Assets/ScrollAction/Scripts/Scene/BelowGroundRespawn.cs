using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// プレイヤーのY座標が threshold を下回ったらリスポーンさせる。
    /// 固定カメラの Shop シーン専用。地面をすり抜けて画面下に消えた時の救済として、
    /// KillZone Trigger コライダの代わりに Y座標の数値判定だけで完結させる。
    /// </summary>
    public class BelowGroundRespawn : MonoBehaviour
    {
        [SerializeField] private PlayerController player;

        // この Y 値を下回ったらリスポーン (地面より大きく下を想定)
        [SerializeField] private float respawnYThreshold;

        void Update()
        {
            if (player == null) return;
            if (player.transform.position.y < respawnYThreshold)
                player.RespawnToStart();
        }
    }
}
