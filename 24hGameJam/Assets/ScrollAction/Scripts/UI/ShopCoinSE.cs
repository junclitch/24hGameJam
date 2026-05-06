using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// ショップで購入・売却が成立した瞬間に Coinv001.wav を鳴らす。
    /// ActionInventory.OnPurchased / OnSold 静的イベントを購読。両方同じ音で OK な前提。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class ShopCoinSE : MonoBehaviour
    {
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip clip;

        void Awake()
        {
            if (source == null) source = GetComponent<AudioSource>();
        }

        void OnEnable()
        {
            ActionInventory.OnPurchased += PlayClip;
            ActionInventory.OnSold += PlayClip;
        }

        void OnDisable()
        {
            ActionInventory.OnPurchased -= PlayClip;
            ActionInventory.OnSold -= PlayClip;
        }

        private void PlayClip()
        {
            if (clip == null || source == null) return;
            source.PlayOneShot(clip);
        }
    }
}
