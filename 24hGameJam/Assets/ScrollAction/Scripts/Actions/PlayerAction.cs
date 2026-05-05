using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// 売買可能な1アクションの抽象基底 (ScriptableObject)。
    /// 各具象アクションが「表示名・最大所持数・物理処理・ライフサイクルフック」を自前で持つ。
    /// アクション追加 = このクラスを継承した SO を新規作成 → Shop の catalog にドロップで完了する設計。
    /// </summary>
    public abstract class PlayerAction : ScriptableObject
    {
        /// <summary>ショップUIなどに表示される能力名。</summary>
        public abstract string DisplayName { get; }

        /// <summary>操作説明。ActionHelpOverlay が画面に常時表示する。受動アクションは空文字でよい。</summary>
        public virtual string HelpText => "";

        /// <summary>
        /// 最大所持数。1 = bool型 (所持/未所持)、2以上 = 上限付きスタック、0 = 無制限スタック。
        /// Shop の UI 形式 (トグルボタン or +/-) もこの値で分岐する。
        /// </summary>
        public virtual int MaxCount => 1;

        [Header("価格")]
        // 購入価格。所持金がこれ以上ないと買えない
        public int buyPrice;

        // 売却価格。1スタック売ると所持金にこの額が加算される
        public int sellPrice;

        /// <summary>毎FixedUpdateで所持していれば呼ばれる。count = 現在の所持数。</summary>
        public virtual void OnFixedTick(PlayerActionContext ctx, int count) { }

        /// <summary>着地した瞬間 (justLanded フレーム) に呼ばれる。空中リソースのリセット用。</summary>
        public virtual void OnLanded() { }

        /// <summary>RespawnToStart で呼ばれる。リスポーン時に初期状態へ戻したい状態をリセット。</summary>
        public virtual void OnRespawn() { }

        /// <summary>ゲーム起動時に1度だけ呼ばれる。SO に NonSerialized 状態を持つ場合の初期化用。</summary>
        public virtual void OnSessionInit() { }

        /// <summary>Shop で購入が成立した瞬間に呼ばれる。例: Jetpack を買い直した時に燃料を満タンへ戻す用。</summary>
        public virtual void OnPurchased() { }
    }
}
