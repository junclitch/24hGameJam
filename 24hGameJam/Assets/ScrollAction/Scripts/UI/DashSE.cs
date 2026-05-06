using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// 地上/空中ダッシュ発動時に dash.wav を 1 発鳴らす効果音。
    /// GroundEvasionAction.OnDashed と AirEvasionAction.OnDashed を購読する (JumpSE と同パターン)。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class DashSE : MonoBehaviour
    {
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip clip;

        void Awake()
        {
            if (source == null) source = GetComponent<AudioSource>();
        }

        void OnEnable()
        {
            GroundEvasionAction.OnDashed += PlayClip;
            AirEvasionAction.OnDashed += PlayClip;
        }

        void OnDisable()
        {
            GroundEvasionAction.OnDashed -= PlayClip;
            AirEvasionAction.OnDashed -= PlayClip;
        }

        private void PlayClip()
        {
            if (clip == null || source == null) return;
            source.PlayOneShot(clip);
        }
    }
}
