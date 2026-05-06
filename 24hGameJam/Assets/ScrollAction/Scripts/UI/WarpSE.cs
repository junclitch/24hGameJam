using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// ワープ開始の瞬間に worp.wav を 1 回鳴らす。IsWarping の false→true 立上がりエッジ検出。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class WarpSE : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip clip;

        // 前フレームのワープ状態。立上がりエッジ検出に使う
        private bool wasWarping;

        void Awake()
        {
            if (source == null) source = GetComponent<AudioSource>();
        }

        void Update()
        {
            if (player == null || source == null || clip == null) return;
            bool warping = player.IsWarping;
            if (warping && !wasWarping) source.PlayOneShot(clip);
            wasWarping = warping;
        }
    }
}
