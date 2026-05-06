using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// 残機。所持数=リトライ回数。死亡 (RespawnToStart) のたびに 1 消費される。
    /// PlayerAction を継承することで Shop の +/- カウンタ UI と売買フローをそのまま流用できる
    /// (能力ではなく「リソース」だが、売買可能アイテムという意味で同じ抽象に乗せる方が安いため)。
    /// 物理的な挙動は持たない (OnFixedTick は基底の no-op を使用)。
    /// </summary>
    [CreateAssetMenu(fileName = "LifeAction", menuName = "ScrollAction/Actions/Life")]
    public class LifeAction : PlayerAction
    {
        public override string DisplayName => "残機";
        public override string HelpText => "(自動) 被弾でリスポーン消費";

        // Shop で +/- カウンタ表示にするため 0 (無制限スタック)
        public override int MaxCount => 0;
    }
}
