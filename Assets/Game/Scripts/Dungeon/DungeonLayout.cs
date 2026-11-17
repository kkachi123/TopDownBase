using System;
using System.Collections.Generic;
using UnityEngine;

namespace TopDownBase.Dungeon
{
    /// <summary>방이 던전 안에서 맡는 역할.</summary>
    public enum DungeonRoomRole
    {
        Start,
        Combat,
        Item,
        BossGate
    }

    /// <summary>방 배치 생성에 쓰는 값 묶음.</summary>
    [Serializable]
    public struct DungeonLayoutSettings
    {
        public Vector2Int GridSize;
        public int RoomCount;
        /// <summary>1이면 Recursive Backtracking(일자형), 0이면 Prim에 가까운 방사형.</summary>
        public float NewestPickChance;
        /// <summary>시작 방에서 보스 입구까지 지나야 하는 최소 방 수(양 끝 포함).</summary>
        public int MinBossPathLength;
        public int MinItemRooms;
        public int MaxAttempts;
    }

    /// <summary>격자 위의 방 하나. 기하 정보 없이 연결과 역할만 가진다.</summary>
    public class DungeonLayoutRoom
    {
        public Vector2Int Cell;
        public DungeonRoomRole Role = DungeonRoomRole.Combat;
        public int DistanceFromStart;
        /// <summary>보스 포털을 여는 열쇠가 놓이는 방인지.</summary>
        public bool HasKey;
        public readonly List<Vector2Int> Neighbours = new List<Vector2Int>(4);
    }

    /// <summary>
    /// Growing Tree 알고리즘으로 만든 방 연결 그래프.
    /// 루프가 없는 트리이므로 모든 방이 항상 시작 방과 연결된다.
    /// </summary>
    public class DungeonLayout
    {
        public static readonly Vector2Int[] Directions =
        {
            Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down
        };

        /// <summary>생성된 순서대로 담긴 방 목록.</summary>
        public readonly List<DungeonLayoutRoom> Rooms = new List<DungeonLayoutRoom>();

        public Vector2Int StartCell { get; private set; }
        public Vector2Int BossGateCell { get; private set; }
        public int BossPathLength { get; private set; }
        public int ItemRoomCount { get; private set; }
        public bool MeetsRequirements { get; private set; }
        public int Attempts { get; private set; }
        /// <summary>열쇠가 놓이는 방. 아이템 방이 없는 배치에서는 null이다.</summary>
        public DungeonLayoutRoom KeyRoom { get; private set; }

        private readonly Dictionary<Vector2Int, DungeonLayoutRoom> _roomsByCell =
            new Dictionary<Vector2Int, DungeonLayoutRoom>();

        public bool Contains(Vector2Int cell)
        {
            return _roomsByCell.ContainsKey(cell);
        }

        public DungeonLayoutRoom GetRoom(Vector2Int cell)
        {
            return _roomsByCell[cell];
        }

        /// <summary>
        /// 조건(보스까지 거리, 아이템 방 수)을 만족하는 배치가 나올 때까지 다시 뽑는다.
        /// 끝까지 실패하면 조건에 가장 가까웠던 배치를 돌려준다.
        /// </summary>
        public static DungeonLayout Generate(DungeonLayoutSettings settings, System.Random rng)
        {
            int maxAttempts = Mathf.Max(1, settings.MaxAttempts);
            DungeonLayout best = null;
            int bestScore = int.MinValue;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                DungeonLayout candidate = GenerateOnce(settings, rng);
                candidate.Attempts = attempt;
                if (candidate.MeetsRequirements)
                {
                    return candidate;
                }

                int score = Mathf.Min(candidate.BossPathLength, settings.MinBossPathLength)
                            + Mathf.Min(candidate.ItemRoomCount, settings.MinItemRooms);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            best.Attempts = maxAttempts;
            return best;
        }

        /// <summary>
        /// 아이템 방 중 하나를 열쇠 방으로 정한다. 방 배치와 다른 난수를 받아,
        /// 열쇠 기능을 넣기 전과 같은 시드가 같은 방 배치와 장애물을 만들도록 한다.
        /// </summary>
        public DungeonLayoutRoom PickKeyRoom(System.Random rng)
        {
            var itemRooms = new List<DungeonLayoutRoom>();
            foreach (DungeonLayoutRoom room in Rooms)
            {
                room.HasKey = false;
                if (room.Role == DungeonRoomRole.Item)
                {
                    itemRooms.Add(room);
                }
            }

            KeyRoom = itemRooms.Count > 0 ? itemRooms[rng.Next(itemRooms.Count)] : null;
            if (KeyRoom != null)
            {
                KeyRoom.HasKey = true;
            }

            return KeyRoom;
        }

        private static DungeonLayout GenerateOnce(DungeonLayoutSettings settings, System.Random rng)
        {
            int gridWidth = Mathf.Max(1, settings.GridSize.x);
            int gridHeight = Mathf.Max(1, settings.GridSize.y);
            int targetRooms = Mathf.Clamp(settings.RoomCount, 1, gridWidth * gridHeight);

            var layout = new DungeonLayout();
            var start = new Vector2Int(gridWidth / 2, gridHeight / 2);
            layout.AddRoom(start);

            // active: 아직 빈 이웃 칸이 남아 있을 수 있는 방들.
            var active = new List<Vector2Int> { start };
            var candidates = new List<Vector2Int>(4);

            while (active.Count > 0 && layout.Rooms.Count < targetRooms)
            {
                // 가장 최근 방을 고르면 길게 뻗고, 무작위 방을 고르면 가지가 생긴다.
                int index = rng.NextDouble() < settings.NewestPickChance
                    ? active.Count - 1
                    : rng.Next(active.Count);
                Vector2Int current = active[index];

                candidates.Clear();
                foreach (Vector2Int direction in Directions)
                {
                    Vector2Int next = current + direction;
                    bool inside = next.x >= 0 && next.x < gridWidth && next.y >= 0 && next.y < gridHeight;
                    if (inside && !layout.Contains(next))
                    {
                        candidates.Add(next);
                    }
                }

                if (candidates.Count == 0)
                {
                    active.RemoveAt(index);
                    continue;
                }

                Vector2Int chosen = candidates[rng.Next(candidates.Count)];
                layout.AddRoom(chosen);
                layout.Link(current, chosen);
                active.Add(chosen);
            }

            layout.AssignRoles(start, settings);
            return layout;
        }

        private void AddRoom(Vector2Int cell)
        {
            var room = new DungeonLayoutRoom { Cell = cell };
            Rooms.Add(room);
            _roomsByCell.Add(cell, room);
        }

        private void Link(Vector2Int a, Vector2Int b)
        {
            _roomsByCell[a].Neighbours.Add(b);
            _roomsByCell[b].Neighbours.Add(a);
        }

        /// <summary>
        /// 시작 방에서 가장 먼 방을 보스 입구로, 나머지 막다른 방을 아이템 방으로 정한다.
        /// </summary>
        private void AssignRoles(Vector2Int start, DungeonLayoutSettings settings)
        {
            StartCell = start;

            var visited = new HashSet<Vector2Int> { start };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            _roomsByCell[start].DistanceFromStart = 0;

            while (queue.Count > 0)
            {
                DungeonLayoutRoom room = _roomsByCell[queue.Dequeue()];
                foreach (Vector2Int neighbour in room.Neighbours)
                {
                    if (visited.Add(neighbour))
                    {
                        _roomsByCell[neighbour].DistanceFromStart = room.DistanceFromStart + 1;
                        queue.Enqueue(neighbour);
                    }
                }
            }

            DungeonLayoutRoom farthest = _roomsByCell[start];
            foreach (DungeonLayoutRoom room in Rooms)
            {
                if (room.DistanceFromStart > farthest.DistanceFromStart)
                {
                    farthest = room;
                }
            }

            BossGateCell = farthest.Cell;
            BossPathLength = farthest.DistanceFromStart + 1;
            ItemRoomCount = 0;

            foreach (DungeonLayoutRoom room in Rooms)
            {
                if (room.Cell == start)
                {
                    room.Role = DungeonRoomRole.Start;
                }
                else if (room == farthest)
                {
                    room.Role = DungeonRoomRole.BossGate;
                }
                else if (room.Neighbours.Count == 1)
                {
                    room.Role = DungeonRoomRole.Item;
                    ItemRoomCount++;
                }
                else
                {
                    room.Role = DungeonRoomRole.Combat;
                }
            }

            MeetsRequirements = BossPathLength >= settings.MinBossPathLength
                                && ItemRoomCount >= settings.MinItemRooms;
        }
    }
}
