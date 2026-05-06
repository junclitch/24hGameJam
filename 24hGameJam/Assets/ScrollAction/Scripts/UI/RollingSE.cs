using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// 転がり中だけ rolling.wav をループ再生。JetpackSE と同じパターン。
    /// AudioSource は loop=true、playOnAwake=false 前提でインスペクタで設定する。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class RollingSE : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private AudioSource source;

        void Awake()
        {
            if (source == null) source = GetComponent<AudioSource>();
        }

        void Update()
        {
            if (player == null || source == null) return;
            if (player.IsRolling)
            {
                if (!source.isPlaying) source.Play();
            }
            else if (source.isPlaying)
            {
                source.Stop();
            }
        }
    }
}
