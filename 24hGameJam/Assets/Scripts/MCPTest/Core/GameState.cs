namespace MCPTest
{
    /// <summary>
    /// ゲーム全体の進行状態を表すFSM。
    /// BreakoutGameManager.State から参照し、各コントローラはこの値で動作可否を判定する。
    /// </summary>
    public enum GameState
    {
        Title,    // タイトル画面（未使用、将来用）
        Playing,  // 通常プレイ中。入力・物理・衝突処理が有効
        Paused,   // 一時停止（未使用、将来用）
        GameOver, // 残機が尽きた状態。ボールは非アクティブ化される
        Cleared   // ブロックを全消し（クリア状態）
    }
}
