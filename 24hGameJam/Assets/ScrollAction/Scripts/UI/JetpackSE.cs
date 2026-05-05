using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// ジェットパック噴射中だけ Jet.wav をループ再生する効果音コンポーネント。
    /// PlayerController.IsJetpacking が true の間 Play、false に戻った瞬間に Stop する。
    /// AudioSource は loop=true、playOnAwake=false 前提。インスペクタで設定する。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class JetpackSE : MonoBehaviour
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

            if (player.IsJetpacking)
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
