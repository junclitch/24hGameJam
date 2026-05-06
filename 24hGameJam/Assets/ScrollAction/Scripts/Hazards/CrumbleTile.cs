using System.Collections;
using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// プレイヤーが触れた瞬間にカウントダウンを開始し、一定秒後に collider と sprite を消す床。
    /// PlayerController.OnPlayerRespawned を購読してリスポーン時に元へ戻すので、ステージ進行で「消えたまま」にはならない。
    /// 視覚は警告色 (warningTint) にして「これは崩れる」と事前に伝える設計 (フェアプレイ重視)。
    /// CompositeCollider2D 配下の床に取り付ける場合、子の Collider2D を enabled=false にすると Composite が再生成される。
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Collider2D))]
    public class CrumbleTile : MonoBehaviour
    {
        [Header("崩壊までの猶予 (sec)")]
        [SerializeField] private float crumbleDelay;

        [Header("初期表示の色 (警告色)")]
        // "これは崩れる" と分かる色合い (赤茶/橙系)。.asset 化はしない (タイル数が多いため per-tile で十分)
        [SerializeField] private Color initialColor;

        private SpriteRenderer sr;
        private Collider2D col;
        private bool triggered;
        private Coroutine crumbleCo;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            col = GetComponent<Collider2D>();
            sr.color = initialColor;
        }

        void OnEnable() => PlayerController.OnPlayerRespawned += ResetTile;
        void OnDisable() => PlayerController.OnPlayerRespawned -= ResetTile;

        /// <summary>
        /// プレイヤーが乗った瞬間 (上から接触した瞬間) にカウントダウン開始。
        /// 二重起動防止のため triggered フラグでガードする。
        /// </summary>
        void OnCollisionEnter2D(Collision2D c)
        {
            if (triggered) return;
            if (c.collider.GetComponentInParent<PlayerController>() == null) return;
            crumbleCo = StartCoroutine(CrumbleAfterDelay());
        }

        private IEnumerator CrumbleAfterDelay()
        {
            triggered = true;
            yield return new WaitForSeconds(crumbleDelay);
            col.enabled = false;
            sr.enabled = false;
        }

        private void ResetTile()
        {
            if (crumbleCo != null) { StopCoroutine(crumbleCo); crumbleCo = null; }
            triggered = false;
            col.enabled = true;
            sr.enabled = true;
            sr.color = initialColor;
        }
    }
}
