using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// ジェットパックアクション。所持中はスペース(またはジャンプキー)長押しで上昇できる。
    /// 燃料制: 使用中は consumeRate で減り、未使用中は regenRate で回復する。
    /// 燃料が0の間は噴射不可。離せば即座に通常重力で落下する。空中/地上どちらでも反応。
    /// 残量は SO の NonSerialized フィールドに持つ (シーン跨ぎは保持、Domain Reload で消える)。
    /// </summary>
    [CreateAssetMenu(fileName = "JetpackAction", menuName = "ScrollAction/Actions/Jetpack")]
    public class JetpackAction : PlayerAction
    {
        public override string DisplayName => "ジェットパック";
        public override string HelpText => "Space / ↑ / W 長押しで上昇";

        [Header("噴射")]
        // 長押し中の維持上昇速度 (units/sec)。値はインスペクタ (Jetpack.asset) で調整する。
        // 重力は無視され、押している間 vy はこの値に張り付く (現vyがこれ未満なら持ち上げる)
        [SerializeField] private float riseSpeed;

        [Header("燃料")]
        // 燃料の最大値 (consumeRate=1ならこの秒数=連続噴射可能秒数)
        [SerializeField] private float maxFuel;
        // 噴射中の燃料消費レート (fuel/sec)
        [SerializeField] private float consumeRate;
        // 未使用時の燃料回復レート (fuel/sec)
        [SerializeField] private float regenRate;

        // 残燃料。Domain Reload で消えるが、購入直後は OnSessionInit で満タンに戻る
        [System.NonSerialized] private float currentFuel;

        /// <summary>UI 表示用に正規化した残燃料 [0..1]。maxFuel=0 の不正設定でも安全に 0 を返す。</summary>
        public float Fuel01 => maxFuel > 0f ? Mathf.Clamp01(currentFuel / maxFuel) : 0f;

        public override void OnFixedTick(PlayerActionContext ctx, int count)
        {
            float dt = Time.fixedDeltaTime;

            if (ctx.jetpackHeld && currentFuel > 0f)
            {
                // 消費しつつ上昇。ジャンプ等で既に上向き速度が高ければ温存
                currentFuel = Mathf.Max(0f, currentFuel - consumeRate * dt);

                float vy = ctx.rb.linearVelocity.y;
                if (vy < riseSpeed)
                    ctx.rb.linearVelocity = new Vector2(ctx.rb.linearVelocity.x, riseSpeed);

                // 効果音判定用フラグ。JetpackSE が読み出して Jet.wav の Play/Stop を切替える
                ctx.isJetpacking = true;
            }
            else if (!ctx.jetpackHeld)
            {
                // 押していない時だけ回復 (空タンクで押し続けても回復しない)
                currentFuel = Mathf.Min(maxFuel, currentFuel + regenRate * dt);
            }
        }

        public override void OnRespawn() => currentFuel = maxFuel;
        public override void OnSessionInit() => currentFuel = maxFuel;
        public override void OnPurchased() => currentFuel = maxFuel;
    }
}
