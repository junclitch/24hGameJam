using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// ジャンプおよび壁キックの発動時に Jump.wav を一発鳴らす効果音コンポーネント。
    /// JumpAction.OnJumped と WallKickAction.OnWallKicked を購読し、
    /// アクション実装側に AudioSource を持ち込まずに済むよう疎結合 (event 駆動) にしている。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class JumpSE : MonoBehaviour
    {
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip clip;

        void Awake()
        {
            if (source == null) source = GetComponent<AudioSource>();
        }

        void OnEnable()
        {
            JumpAction.OnJumped += PlayClip;
            WallKickAction.OnWallKicked += PlayClip;
        }

        void OnDisable()
        {
            JumpAction.OnJumped -= PlayClip;
            WallKickAction.OnWallKicked -= PlayClip;
        }

        private void PlayClip()
        {
            if (clip == null || source == null) return;
            source.PlayOneShot(clip);
        }
    }
}
