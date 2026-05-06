using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// 落下鉄球の着地音 (dooon.wav)。FallingBallHazard.OnImpact (位置付き) を購読し、
    /// プレイヤーとの距離でボリュームをスケールして再生する (近いほど大きい、遠いと無音)。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class FallingBallSE : MonoBehaviour
    {
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip clip;
        [SerializeField] private Transform listener;

        [Header("距離減衰")]
        // この距離以内でフルボリューム
        [SerializeField] private float fullVolumeDistance;
        // この距離を超えると無音 (中間は線形減衰)
        [SerializeField] private float silentDistance;
        // フルボリューム時の倍率 (全体音量調整)
        [SerializeField] private float maxVolume;

        void Awake()
        {
            if (source == null) source = GetComponent<AudioSource>();
        }

        void OnEnable() => FallingBallHazard.OnImpact += PlayClipAt;
        void OnDisable() => FallingBallHazard.OnImpact -= PlayClipAt;

        private void PlayClipAt(Vector3 pos)
        {
            if (clip == null || source == null || listener == null) return;
            float dist = Vector3.Distance(pos, listener.position);
            if (dist >= silentDistance) return;

            float t = Mathf.InverseLerp(silentDistance, fullVolumeDistance, dist);
            float vol = Mathf.Clamp01(t) * maxVolume;
            source.PlayOneShot(clip, vol);
        }
    }
}
