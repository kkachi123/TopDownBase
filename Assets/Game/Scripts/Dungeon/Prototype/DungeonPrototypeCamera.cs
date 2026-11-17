using UnityEngine;
using UnityEngine.InputSystem;

namespace TopDownBase.Dungeon.Prototype
{
    /// <summary>
    /// 프로토타입 확인용 정사영 카메라. 평소에는 대상을 따라가고,
    /// Tab을 누르고 있는 동안에는 던전 전체가 보이도록 물러난다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class DungeonPrototypeCamera : MonoBehaviour
    {
        public Transform Target;
        public DungeonMapSource Generator;
        [Range(20f, 90f)] public float Pitch = 40f;
        [Min(1f)] public float FollowOrthographicSize = 12f;
        [Min(1f)] public float Distance = 80f;
        [Min(0f)] public float OverviewMargin = 4f;

        private Camera _camera;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
        }

        private void LateUpdate()
        {
            transform.rotation = Quaternion.Euler(Pitch, 0f, 0f);

            bool overview = Generator != null && Keyboard.current != null && Keyboard.current.tabKey.isPressed;
            Vector3 focus;
            if (overview)
            {
                Bounds bounds = Generator.GetWorldBounds();
                focus = bounds.center;

                // 기울어진 정사영 카메라에서 바닥의 깊이(z)는 sin(pitch)만큼 줄어들어 보인다.
                float depthOnScreen = bounds.size.z * Mathf.Sin(Pitch * Mathf.Deg2Rad);
                float sizeForDepth = depthOnScreen * 0.5f;
                float sizeForWidth = bounds.size.x * 0.5f / Mathf.Max(_camera.aspect, 0.01f);
                _camera.orthographicSize = Mathf.Max(sizeForDepth, sizeForWidth) + OverviewMargin;
            }
            else
            {
                focus = Target != null ? Target.position : transform.position + transform.forward * Distance;
                _camera.orthographicSize = FollowOrthographicSize;
            }

            transform.position = focus - transform.forward * Distance;
        }
    }
}
