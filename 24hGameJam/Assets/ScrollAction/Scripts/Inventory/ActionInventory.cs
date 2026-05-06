using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// プレイヤーの所持アクションリストを保持する ScriptableObject (RAM相当)。
    /// 中身は List&lt;OwnedAction&gt;: 1要素 = 1アクション種別 + 所持数。
    /// シーンを跨いで状態を保持し、売買で要素が増減する。
    /// 起動時に EnsureInitializedThisSession() を呼ぶことで Defaults SO の内容で初期化される。
    /// </summary>
    [CreateAssetMenu(fileName = "ActionInventory", menuName = "ScrollAction/Action Inventory")]
    public class ActionInventory : ScriptableObject
    {
        [Header("初期値プロファイル (ゲーム開始時にここから現在値へコピー)")]
        [SerializeField] private ActionInventoryDefaults defaults;

        [Header("現在の所持アクション (ランタイムで売買により変動)")]
        public List<OwnedAction> owned = new();

        [Header("所持金 (ランタイムで売買により変動)")]
        public int money;

        /// <summary>所持状態が変化した時に発火。Shop UI / PlayerController が再同期に使う。</summary>
        public event Action OnInventoryChanged;

        /// <summary>購入が成立した瞬間に発火。SE 等が購読する。</summary>
        public static event Action OnPurchased;

        /// <summary>売却が成立した瞬間に発火。SE 等が購読する。</summary>
        public static event Action OnSold;

        // 同セッション内で既に初期化済みかを示すフラグ。Domain Reload (Play押下) でクリアされる
        private static bool sessionInitialized;

        public void NotifyChanged() => OnInventoryChanged?.Invoke();

        /// <summary>指定アクションの所持数。未所持なら 0。</summary>
        public int GetCount(PlayerAction action)
        {
            if (action == null) return 0;
            for (int i = 0; i < owned.Count; i++)
                if (owned[i].action == action) return owned[i].count;
            return 0;
        }

        /// <summary>指定型のアクションを1つ以上所持しているか。GroundCheckAction の所持判定などに使う。</summary>
        public bool HasAny<T>() where T : PlayerAction
        {
            for (int i = 0; i < owned.Count; i++)
                if (owned[i].count > 0 && owned[i].action is T) return true;
            return false;
        }

        /// <summary>
        /// 指定型のアクションを 1 つ消費する (count を 1 減らす)。所持していなければ false を返す。
        /// 残機消費など「リトライ用リソース」の減算に使う。0 になった枠は owned から削除される。
        /// </summary>
        public bool ConsumeOne<T>() where T : PlayerAction
        {
            for (int i = 0; i < owned.Count; i++)
            {
                if (owned[i].action is T && owned[i].count > 0)
                {
                    owned[i].count--;
                    if (owned[i].count <= 0) owned.RemoveAt(i);
                    NotifyChanged();
                    return true;
                }
            }
            return false;
        }

        /// <summary>購入可能か (所持金が足りているか + 上限未達か) を判定。</summary>
        public bool CanBuy(PlayerAction action)
        {
            if (action == null) return false;
            if (money < action.buyPrice) return false;
            int max = action.MaxCount;
            if (max > 0 && GetCount(action) >= max) return false;
            return true;
        }

        /// <summary>売却可能か (1個以上所持しているか) を判定。</summary>
        public bool CanSell(PlayerAction action) => action != null && GetCount(action) > 0;

        /// <summary>
        /// 所持数を1増やす。所持金から buyPrice を差し引く。
        /// 不足や上限到達時は何もしない (UI側で CanBuy を見てボタン無効化することを想定)。
        /// </summary>
        public void Buy(PlayerAction action)
        {
            if (!CanBuy(action)) return;
            money -= action.buyPrice;
            var slot = FindSlot(action);
            if (slot == null)
            {
                slot = new OwnedAction { action = action, count = 0 };
                owned.Add(slot);
            }
            slot.count++;
            // 購入直後の状態リセット (例: Jetpack の燃料を満タンへ)
            action.OnPurchased();
            NotifyChanged();
            OnPurchased?.Invoke();
        }

        /// <summary>所持数を1減らす。所持金に sellPrice を加算。0以下になったらリストから除去。</summary>
        public void Sell(PlayerAction action)
        {
            if (!CanSell(action)) return;
            money += action.sellPrice;
            var slot = FindSlot(action);
            slot.count--;
            if (slot.count <= 0) owned.Remove(slot);
            NotifyChanged();
            OnSold?.Invoke();
        }

        private OwnedAction FindSlot(PlayerAction action)
        {
            for (int i = 0; i < owned.Count; i++)
                if (owned[i].action == action) return owned[i];
            return null;
        }

        /// <summary>初期値プロファイルの内容を現在値リストと所持金へコピーし直す。</summary>
        public void ResetToDefaults()
        {
            owned.Clear();
            money = 0;
            if (defaults != null)
            {
                foreach (var d in defaults.owned)
                {
                    if (d?.action == null || d.count <= 0) continue;
                    owned.Add(new OwnedAction { action = d.action, count = d.count });
                }
                money = defaults.initialMoney;
            }
            // 各アクションの NonSerialized ランタイム状態も初期化
            foreach (var slot in owned) slot.action?.OnSessionInit();
            NotifyChanged();
        }

        /// <summary>
        /// ゲーム起動時に1度だけ ResetToDefaults() を実行。シーン遷移を跨いでも2回目以降は何もしない。
        /// PlayerController.Awake から呼ぶ前提。
        /// </summary>
        public void EnsureInitializedThisSession()
        {
            if (sessionInitialized) return;
            sessionInitialized = true;
            ResetToDefaults();
        }
    }
}
