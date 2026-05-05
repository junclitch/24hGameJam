using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// クリア演出用に効果音を連発再生するコンポーネント。GameClear シーンに置き、
    /// clip.length 相当の間隔で繰り返し PlayOneShot する。
    /// 「音が重ならない程度に大量」を実現するため、interval が 0 以下なら clip.length を採用する。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class CoinShowerSE : MonoBehaviour
    {
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip coinClip;

        // 連発間隔 (sec)。0 以下なら clip.length を自動採用して重ならない最短間隔で鳴らす
        [SerializeField] private float interval;

        private float timer;

        void Awake()
        {
            if (source == null) source = GetComponent<AudioSource>();
        }

        void Update()
        {
            if (coinClip == null || source == null) return;
            timer -= Time.deltaTime;
            if (timer > 0f) return;

            source.PlayOneShot(coinClip);
            timer = interval > 0f ? interval : coinClip.length;
        }
    }
}
