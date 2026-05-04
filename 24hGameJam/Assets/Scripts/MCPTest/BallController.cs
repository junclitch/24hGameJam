using System;
using UnityEngine;

namespace MCPTest
{
    /// <summary>
    /// ボールの物理挙動・反射補正・落下リセットを担当する。
    /// GameManager は知らない。落下したことを伝えるためだけに OnFell イベントを発火する。
    /// 実際の反射は Box2D が処理し、本クラスは「速度を一定に保つ」「浅すぎる角度を矯正する」
    /// 「パドルでは当たり位置に応じた反射角を上書き」の3点を担当する。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class BallController : MonoBehaviour
    {
        [SerializeField] private BallStats stats;

        // ボールが落下した瞬間に発火する static イベント。
        // GameManager 等が購読する（このクラスから GameManager を参照しないための仕組み）。
        public static event Action OnFell;

        private Rigidbody2D rb;
        private Vector3 startPosition;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            // 初期位置は、リセット時の戻り先として記憶しておく
            startPosition = transform.position;
        }

        void Start()
        {
            // ゲーム開始時 (Playing) のみ自動発射
            if (BreakoutGameManager.State == GameState.Playing) Launch();
        }

        void Update()
        {
            if (BreakoutGameManager.State != GameState.Playing) return;
            if (stats == null) return;

            // 画面外に落ちたらリセット処理に飛ばす
            if (transform.position.y < stats.resetY)
            {
                ResetBall();
                return;
            }

            // 反射やすり抜けで速度の絶対値が変わるので毎フレーム正規化して一定に保つ
            if (rb.linearVelocity.sqrMagnitude > 0.0001f)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * stats.speed;
            }
        }

        /// <summary>初期位置から斜め上方向にランダム発射。</summary>
        public void Launch()
        {
            if (stats == null) return;
            float x = UnityEngine.Random.Range(-stats.launchHorizontalRange, stats.launchHorizontalRange);
            Vector2 dir = new Vector2(x, 1f).normalized;
            rb.linearVelocity = dir * stats.speed;
        }

        /// <summary>
        /// ボール落下時の処理。OnFell を先に発火してから、新しいゲーム状態を確認して再発射 or 停止を決める。
        /// （順序が重要: OnFell を購読している GameManager がライフを減らし、必要なら GameOver に遷移するため、
        ///   再発射判定は state 変更後に行わなければならない）
        /// </summary>
        public void ResetBall()
        {
            transform.position = startPosition;
            rb.linearVelocity = Vector2.zero;

            // GameManager 等の購読者に通知
            OnFell?.Invoke();

            // 通知の結果ゲームオーバーやクリアになっていたらボールを非アクティブ化
            if (BreakoutGameManager.State != GameState.Playing)
            {
                gameObject.SetActive(false);
                return;
            }
            Launch();
        }

        void OnCollisionEnter2D(Collision2D c)
        {
            if (stats == null) return;

            // パドルとの衝突は、Box2Dの自然な反射ではなく
            // 「当たった位置による反射角」で上書きする（ブロック崩しらしい挙動）
            if (c.collider.GetComponent<PaddleController>() != null)
            {
                ReflectFromPaddle(c.collider.bounds);
                return;
            }

            // 壁・ブロックは Box2D の反射に任せ、浅すぎる角度だけ矯正
            AvoidShallowAngle();
        }

        /// <summary>
        /// パドル中央からの相対位置 t = [-1, 1] を求め、
        /// それに比例した「鉛直方向からの角度」で再発射する。
        /// 中央でまっすぐ上、端で最大 maxPaddleAngleDeg だけ斜め上方向。
        /// </summary>
        void ReflectFromPaddle(Bounds paddleBounds)
        {
            float halfW = Mathf.Max(paddleBounds.extents.x, 0.01f);
            float dx = transform.position.x - paddleBounds.center.x;
            float t = Mathf.Clamp(dx / halfW, -1f, 1f);
            float rad = t * stats.maxPaddleAngleDeg * Mathf.Deg2Rad;
            // sinで横方向、cosで上方向 → 必ず上向き
            Vector2 dir = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
            rb.linearVelocity = dir.normalized * stats.speed;
        }

        /// <summary>
        /// 壁同士の間で水平/垂直に往復し続けてしまう状況を防ぐ。
        /// 縦・横どちらの成分も最低比率を割らないよう押し上げ、normalize で速度を一定に揃える。
        /// </summary>
        void AvoidShallowAngle()
        {
            Vector2 v = rb.linearVelocity;
            float minVy = stats.speed * stats.minVerticalRatio;
            float minVx = stats.speed * stats.minHorizontalRatio;

            if (Mathf.Abs(v.y) < minVy)
            {
                v.y = (v.y >= 0f ? 1f : -1f) * minVy;
            }
            if (Mathf.Abs(v.x) < minVx)
            {
                // 横成分がほぼゼロのときは符号がないのでランダムに割り振る
                float sign = v.x != 0f ? Mathf.Sign(v.x) : (UnityEngine.Random.value < 0.5f ? -1f : 1f);
                v.x = sign * minVx;
            }
            rb.linearVelocity = v.normalized * stats.speed;
        }
    }
}
