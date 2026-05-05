using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// Shop シーンが開かれた時にプレイヤーの所持アクションを Defaults へ戻す。
    /// Title・GameOver・GameClear いずれの導線から来ても Shop で装備を買い直す前提にするため、
    /// Inventory の現在値を毎回リセットする (リトライで GroundCheckAction が抜けて
    /// 地面をすり抜ける問題への対策も兼ねる)。
    /// </summary>
    public class ShopSessionResetter : MonoBehaviour
    {
        [SerializeField] private ActionInventory inventory;

        void Awake()
        {
            if (inventory != null) inventory.ResetToDefaults();
        }
    }
}
