using System;

namespace ScrollAction
{
    /// <summary>
    /// インベントリ内の1スタック。1アクション種別 + 所持数のペア。
    /// bool 系アクションは count=1 (所持) か リストから消える (未所持) のどちらか。
    /// 整数系アクション (ジャンプ・空中回避) は count に値が入る。
    /// </summary>
    [Serializable]
    public class OwnedAction
    {
        public PlayerAction action;
        public int count;
    }
}
