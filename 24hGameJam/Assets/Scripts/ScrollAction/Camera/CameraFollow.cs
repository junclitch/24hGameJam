using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// カメラがターゲットを滑らかに追従する。Z成分は固定（2Dカメラを手前に保つ）。
    /// LateUpdate で動かすことで、ターゲットの移動が確定した後に追従できカクつきを抑える。
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;

        // 追従の滑らかさ。大きいほど即座に追いつき、小さいほどゆったり追う
        [SerializeField] private float smoothing;

        // ターゲットからのオフセット (主にY方向の見上げ/見下ろし調整に使う)
        [SerializeField] private Vector2 offset;

        void LateUpdate()
        {
            if (target == null) return;

            Vector3 desired = new Vector3(target.position.x + offset.x, target.position.y + offset.y, transform.position.z);
            // 速度ベースではなく時間ベースの補間: フレームレート差で追従感が変わらない
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
        }
    }
}
