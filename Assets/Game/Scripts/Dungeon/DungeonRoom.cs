using System.Collections.Generic;
using UnityEngine;

namespace TopDownBase.Dungeon
{
    /// <summary>
    /// 생성된 방 하나의 결과물. 역할, 적 스폰 위치, 문 차단물을 보관해
    /// 이후 전투·보상 로직이 방 단위로 접근할 수 있게 한다.
    /// </summary>
    public class DungeonRoom : MonoBehaviour
    {
        [SerializeField] private DungeonRoomRole _role;
        [SerializeField] private Vector2Int _cell;
        [SerializeField] private int _distanceFromStart;
        [SerializeField] private Vector2 _interiorSize;
        [SerializeField] private List<Transform> _enemySpawnPoints = new List<Transform>();
        [SerializeField] private List<GameObject> _doorBlockers = new List<GameObject>();

        public DungeonRoomRole Role => _role;
        public Vector2Int Cell => _cell;
        public int DistanceFromStart => _distanceFromStart;
        /// <summary>벽 안쪽 바닥의 가로(x)·세로(z) 크기(유닛).</summary>
        public Vector2 InteriorSize => _interiorSize;
        public IReadOnlyList<Transform> EnemySpawnPoints => _enemySpawnPoints;
        public bool DoorsLocked { get; private set; }

        public void Initialize(DungeonRoomRole role, Vector2Int cell, int distanceFromStart, Vector2 interiorSize)
        {
            _role = role;
            _cell = cell;
            _distanceFromStart = distanceFromStart;
            _interiorSize = interiorSize;
        }

        public void AddEnemySpawnPoint(Transform point)
        {
            _enemySpawnPoints.Add(point);
        }

        public void AddDoorBlocker(GameObject blocker)
        {
            _doorBlockers.Add(blocker);
        }

        /// <summary>이 방에 연결된 모든 문을 막거나 연다.</summary>
        public void SetDoorsLocked(bool locked)
        {
            DoorsLocked = locked;
            foreach (GameObject blocker in _doorBlockers)
            {
                if (blocker != null)
                {
                    blocker.SetActive(locked);
                }
            }
        }
    }
}
