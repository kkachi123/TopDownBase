using UnityEngine;

namespace TopDownBase.Dungeon
{
    /// <summary>
    /// 아이템 방에 놓이는 열쇠. 플레이어가 닿으면 연결된 포털을 열고 사라진다.
    /// 인벤토리 없이 포털을 직접 여는 가벼운 잠금이다.
    /// </summary>
    public class DungeonKeyPickup : MonoBehaviour
    {
        [SerializeField] private DungeonPortal _portal;

        public bool IsCollected { get; private set; }

        /// <summary>생성기가 만든 직후 열어야 할 포털을 넘겨준다.</summary>
        public void Initialize(DungeonPortal portal)
        {
            _portal = portal;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsCollected || !other.CompareTag(DungeonPortal.TravellerTag))
            {
                return;
            }

            IsCollected = true;
            if (_portal != null)
            {
                _portal.Unlock();
            }

            gameObject.SetActive(false);
        }
    }
}
