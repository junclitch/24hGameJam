using System.Collections.Generic;
using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// ActionInventory の初期値プロファイル。
    /// ゲーム起動時に owned の内容が ActionInventory.owned へコピーされる。
    /// 難易度別 (Easy/Normal/Hard) や章別の初期構成を切替えたい場合は複数の .asset を作って差し替える。
    /// </summary>
    [CreateAssetMenu(fileName = "ActionInventoryDefaults", menuName = "ScrollAction/Action Inventory Defaults")]
    public class ActionInventoryDefaults : ScriptableObject
    {
        [Header("初期所持アクションリスト")]
        public List<OwnedAction> owned = new();

        [Header("初期所持金")]
        public int initialMoney;
    }
}
