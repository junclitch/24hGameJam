using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// スライディング開始の瞬間に Sliding.wav を 1 回鳴らす。IsSliding の false→true 立上がりエッジ検出。
    /// ループ系にせず PlayOneShot にしている理由: スライドは短い 1 アクションのため、
    /// ループ再生だと終了時に音が途切れて違和感が出やすい。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class SlidingSE : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip clip;

        // 前フレームのスライド状態。立上がりエッジ検出に使う
        private bool wasSliding;

        void Awake()
        {
            if (source == null) source = GetComponent<AudioSource>();
        }

        void Update()
        {
            if (player == null || source == null || clip == null) return;
            bool sliding = player.IsSliding;
            if (sliding && !wasSliding) source.PlayOneShot(clip);
            wasSliding = sliding;
        }
    }
}
