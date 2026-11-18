using UnityEngine;
using UnityEngine.InputSystem;

namespace TopDownBase.Dungeon.Prototype
{
    /// <summary>
    /// 생성된 던전을 걸어 다니며 방 크기와 구조를 확인하기 위한 임시 이동체.
    /// 게임의 플레이어 캐릭터가 아니며 TopDown Engine과 연결되어 있지 않다.
    /// 포털과 열쇠가 반응하려면 이 오브젝트의 태그가 Player여야 한다.
    /// WASD: 이동, R: 새 시드로 다시 생성, L: 현재 방의 출입구 잠금 전환.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class DungeonPrototypeWalker : MonoBehaviour
    {
        public DungeonMapSource Generator;
        [Min(0f)] public float MoveSpeed = 8f;

        private CharacterController _controller;
        private float _verticalSpeed;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        private void Start()
        {
            WarpToStart();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.rKey.wasPressedThisFrame && Generator != null)
            {
                Generator.GenerateWithRandomSeed();
                WarpToStart();
                return;
            }

            if (keyboard.lKey.wasPressedThisFrame && Generator != null)
            {
                Generator.ToggleRoomLockAt(transform.position);
            }

            var input = new Vector3(
                (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                0f,
                (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));
            Vector3 velocity = Vector3.ClampMagnitude(input, 1f) * MoveSpeed;

            _verticalSpeed = _controller.isGrounded ? -1f : _verticalSpeed + Physics.gravity.y * Time.deltaTime;
            velocity.y = _verticalSpeed;

            _controller.Move(velocity * Time.deltaTime);
        }

        private void WarpToStart()
        {
            if (Generator == null)
            {
                return;
            }

            // CharacterController가 켜져 있으면 위치 대입이 무시되므로 잠시 끈다.
            _controller.enabled = false;
            transform.position = Generator.StartPosition + Vector3.up * 0.1f;
            _controller.enabled = true;
            _verticalSpeed = 0f;
        }
    }
}
