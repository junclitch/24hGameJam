using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// シーン遷移を伴うボタン押下時に決定SEを鳴らすための DontDestroyOnLoad なヘルパー。
    /// PlayOneShot 直後に LoadScene を呼ぶと音が即時カットされてしまうので、
    /// 音用 GameObject だけシーンを跨いで生き残らせて鳴り切らせる。
    /// </summary>
    public static class DecideSoundPlayer
    {
        private static AudioSource source;

        public static void Play(AudioClip clip)
        {
            if (clip == null) return;
            EnsureSource();
            source.PlayOneShot(clip);
        }

        private static void EnsureSource()
        {
            if (source != null) return;
            var go = new GameObject("DecideSoundPlayer");
            Object.DontDestroyOnLoad(go);
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.volume = 0.5f;
        }
    }
}
