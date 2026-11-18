using System;
using System.Collections.Generic;
using UnityEngine;

namespace TopDownBase.Dungeon
{
    /// <summary>방의 모양. Rectangle은 직사각형, Clearing은 크기와 윤곽이 흔들리는 공터다.</summary>
    public enum WalkableShape
    {
        Rectangle,
        Clearing
    }

    [Serializable]
    public struct WalkableMapSettings
    {
        public WalkableShape Shape;
        /// <summary>방 하나가 쓸 수 있는 최대 크기(유닛). 공터는 이 안에서 만들어진다.</summary>
        public Vector2 RoomSize;
        /// <summary>이웃한 방 사이에 남기는 경계의 폭(유닛). 방 간격은 RoomSize + BoundaryWidth다.</summary>
        public float BoundaryWidth;
        /// <summary>공터 크기 배율의 (최소, 최대). RoomSize에 대한 비율이다.</summary>
        public Vector2 ClearingScale;
        /// <summary>공터 중심을 방 안에서 흔드는 정도(0~1).</summary>
        public float CenterJitter;
        /// <summary>공터 윤곽의 울퉁불퉁한 정도(0~0.4).</summary>
        public float OutlineWobble;
        public float PathWidth;
        /// <summary>길의 가운데를 옆으로 미는 최대 거리(유닛). 0이면 직선이다.</summary>
        public float PathBend;
        /// <summary>맵 바깥쪽에 두르는 막힌 영역의 폭(유닛).</summary>
        public float OuterMargin;
        public Vector2Int ObstacleCount;
        /// <summary>장애물 가장자리와 경계 사이에 남기는 거리(유닛).</summary>
        public float ObstacleMargin;
        /// <summary>장애물 중심 사이의 최소 거리(유닛).</summary>
        public float ObstacleSpacing;
        public Vector2 ObstacleRadius;
    }

    public class WalkableRoom
    {
        public DungeonLayoutRoom Data;
        public int Index;
        public Vector2 Center;
        public Vector2 Radius;
        public float[] WobbleAmplitude;
        public float[] WobblePhase;
    }

    public struct WalkableObstacle
    {
        public Vector2 Position;
        public float Radius;
        public int RoomIndex;
    }

    public struct WalkableSpawn
    {
        public Vector2 Position;
        public int RoomIndex;
    }

    /// <summary>적 스폰 위치를 고르는 규칙. 거리는 모두 유닛이다.</summary>
    [Serializable]
    public struct WalkableSpawnSettings
    {
        /// <summary>전투 방마다 놓을 스폰 위치 수 (최소, 최대).</summary>
        public Vector2Int Count;
        /// <summary>경계에서 띄우는 거리.</summary>
        public float EdgeMargin;
        /// <summary>입구 상자에서 띄우는 거리. 방에 들어서자마자 적과 겹치지 않게 한다.</summary>
        public float EntranceClearance;
        /// <summary>장애물 가장자리에서 띄우는 거리.</summary>
        public float ObstacleClearance;
        /// <summary>스폰 위치 사이의 최소 거리.</summary>
        public float Spacing;
    }

    /// <summary>
    /// 길이 방(공터)에 닿는 곳 하나. 방에 붙은 길 칸들을 이어진 덩어리로 묶은 것이다.
    /// 이 칸들을 모두 막으면 그 방에서 길로 나갈 수 없다.
    /// 상자(Center, Direction, Length, Width)는 그 칸들을 빠짐없이 덮는 회전된 직사각형이다.
    /// </summary>
    public class WalkableEntrance
    {
        public int RoomIndex;
        public readonly List<Vector2Int> Cells = new List<Vector2Int>();
        public Vector2 Center;
        /// <summary>방에서 길 쪽으로 나가는 방향(단위 벡터).</summary>
        public Vector2 Direction;
        /// <summary>Direction 방향의 상자 길이(유닛).</summary>
        public float Length;
        /// <summary>Direction에 수직인 방향의 상자 폭(유닛).</summary>
        public float Width;
    }

    /// <summary>
    /// 방 그래프(DungeonLayout)에서 만든 "걸을 수 있는 영역" 지도. 한 칸이 1유닛이다.
    /// 경계를 무엇으로 표현할지(지형 높낮이, 소품)는 이 지도를 읽는 쪽이 정한다.
    /// 좌표는 맵을 만드는 오브젝트 기준의 (x, z) 평면이다.
    /// </summary>
    public class WalkableMap
    {
        public const int Blocked = -1;
        public const int Path = -2;

        private const float Diagonal = 1.41421356f;
        private static readonly int[] WobbleHarmonics = { 2, 3, 4 };

        public int Width { get; private set; }
        public int Height { get; private set; }
        /// <summary>칸 (0, 0)의 남서쪽 모서리 위치.</summary>
        public Vector2 Origin { get; private set; }
        public Vector2 CellPitch { get; private set; }

        /// <summary>칸의 주인: 방 번호, Path, 또는 Blocked.</summary>
        public int[,] Owner { get; private set; }
        /// <summary>막힌 칸에서 가장 가까운 걸을 수 있는 칸까지의 거리. 걸을 수 있는 칸은 0이다.</summary>
        public float[,] DistanceToWalkable { get; private set; }
        /// <summary>걸을 수 있는 칸에서 가장 가까운 막힌 칸까지의 거리. 막힌 칸은 0이다.</summary>
        public float[,] Clearance { get; private set; }

        public readonly List<WalkableRoom> Rooms = new List<WalkableRoom>();
        public readonly List<WalkableObstacle> Obstacles = new List<WalkableObstacle>();
        public readonly List<WalkableEntrance> Entrances = new List<WalkableEntrance>();
        /// <summary>PlaceEnemySpawns를 부른 뒤에 채워진다.</summary>
        public readonly List<WalkableSpawn> EnemySpawns = new List<WalkableSpawn>();
        public WalkableRoom StartRoom { get; private set; }
        public WalkableRoom BossRoom { get; private set; }
        public int WalkableCellCount { get; private set; }
        /// <summary>길로 판 칸 수. 방 사이 이동 거리의 척도다.</summary>
        public int PathCellCount { get; private set; }

        public float WalkableRatio => (float)WalkableCellCount / (Width * Height);

        public bool IsWalkable(int x, int y)
        {
            return Owner[x, y] != Blocked;
        }

        public Vector2 CellCenter(int x, int y)
        {
            return Origin + new Vector2(x + 0.5f, y + 0.5f);
        }

        public bool TryGetCell(Vector2 position, out int x, out int y)
        {
            x = Mathf.FloorToInt(position.x - Origin.x);
            y = Mathf.FloorToInt(position.y - Origin.y);
            return x >= 0 && y >= 0 && x < Width && y < Height;
        }

        public static WalkableMap Build(DungeonLayout layout, WalkableMapSettings settings, System.Random rng)
        {
            var map = new WalkableMap();
            map.CellPitch = settings.RoomSize + Vector2.one * settings.BoundaryWidth;

            Vector2Int min = layout.Rooms[0].Cell;
            Vector2Int max = min;
            foreach (DungeonLayoutRoom room in layout.Rooms)
            {
                min = Vector2Int.Min(min, room.Cell);
                max = Vector2Int.Max(max, room.Cell);
            }

            float margin = Mathf.Max(2f, settings.OuterMargin);
            map.Origin = new Vector2(
                (min.x - 0.5f) * map.CellPitch.x - margin,
                (min.y - 0.5f) * map.CellPitch.y - margin);
            map.Width = Mathf.CeilToInt((max.x - min.x + 1) * map.CellPitch.x + margin * 2f);
            map.Height = Mathf.CeilToInt((max.y - min.y + 1) * map.CellPitch.y + margin * 2f);

            map.Owner = new int[map.Width, map.Height];
            for (int x = 0; x < map.Width; x++)
            {
                for (int y = 0; y < map.Height; y++)
                {
                    map.Owner[x, y] = Blocked;
                }
            }

            var roomsByCell = new Dictionary<Vector2Int, WalkableRoom>();
            foreach (DungeonLayoutRoom data in layout.Rooms)
            {
                WalkableRoom room = map.CreateRoom(data, settings, rng);
                roomsByCell.Add(data.Cell, room);
                map.CarveRoom(room, settings);

                if (data.Role == DungeonRoomRole.Start)
                {
                    map.StartRoom = room;
                }
                else if (data.Role == DungeonRoomRole.BossGate)
                {
                    map.BossRoom = room;
                }
            }

            foreach (WalkableRoom room in map.Rooms)
            {
                foreach (Vector2Int neighbourCell in room.Data.Neighbours)
                {
                    WalkableRoom neighbour = roomsByCell[neighbourCell];
                    if (neighbour.Index > room.Index)
                    {
                        map.CarvePath(room, neighbour, settings, rng);
                    }
                }
            }

            map.CountCells();
            map.FindEntrances();
            map.ComputeDistances();
            map.PlaceObstacles(settings, rng);
            return map;
        }

        /// <summary>
        /// 시작 방에서 모든 방의 중심까지 걸어서 갈 수 있는지. 장애물을 막힌 칸으로 칠 수도 있다.
        /// bodyRadius를 주면 경계와 장애물에서 그만큼 떨어진 칸만 지나갈 수 있는 것으로 본다.
        /// NavMesh는 에이전트 반지름만큼 가장자리를 깎으므로, 그 값을 넣어야 실제 이동 가능 여부와 맞는다.
        /// </summary>
        public bool IsFullyConnected(bool obstaclesBlock, float bodyRadius = 0f)
        {
            if (StartRoom == null || !TryGetCell(StartRoom.Center, out int startX, out int startY))
            {
                return false;
            }

            var visited = new bool[Width, Height];
            var queue = new Queue<Vector2Int>();
            visited[startX, startY] = true;
            queue.Enqueue(new Vector2Int(startX, startY));

            while (queue.Count > 0)
            {
                Vector2Int cell = queue.Dequeue();
                for (int i = 0; i < DungeonLayout.Directions.Length; i++)
                {
                    Vector2Int next = cell + DungeonLayout.Directions[i];
                    if (next.x < 0 || next.y < 0 || next.x >= Width || next.y >= Height
                        || visited[next.x, next.y] || !IsWalkable(next.x, next.y))
                    {
                        continue;
                    }

                    if (bodyRadius > 0f && Clearance[next.x, next.y] < bodyRadius + 0.5f)
                    {
                        continue;
                    }

                    if (obstaclesBlock && IsInsideObstacle(CellCenter(next.x, next.y), bodyRadius))
                    {
                        continue;
                    }

                    visited[next.x, next.y] = true;
                    queue.Enqueue(next);
                }
            }

            foreach (WalkableRoom room in Rooms)
            {
                if (!TryGetCell(room.Center, out int x, out int y) || !visited[x, y])
                {
                    return false;
                }
            }

            return true;
        }

        public bool IsInsideObstacle(Vector2 position, float extraRadius = 0f)
        {
            foreach (WalkableObstacle obstacle in Obstacles)
            {
                float radius = obstacle.Radius + extraRadius;
                if ((position - obstacle.Position).sqrMagnitude < radius * radius)
                {
                    return true;
                }
            }

            return false;
        }

        // ---------- 방과 길 ----------

        private WalkableRoom CreateRoom(DungeonLayoutRoom data, WalkableMapSettings settings, System.Random rng)
        {
            var room = new WalkableRoom { Data = data, Index = Rooms.Count };
            var cellCenter = new Vector2(data.Cell.x * CellPitch.x, data.Cell.y * CellPitch.y);
            Vector2 half = settings.RoomSize * 0.5f;

            if (settings.Shape == WalkableShape.Rectangle)
            {
                room.Center = cellCenter;
                room.Radius = half;
            }
            else
            {
                float scaleMin = Mathf.Clamp01(Mathf.Min(settings.ClearingScale.x, settings.ClearingScale.y));
                float scaleMax = Mathf.Clamp01(Mathf.Max(settings.ClearingScale.x, settings.ClearingScale.y));
                room.Radius = new Vector2(
                    half.x * Mathf.Lerp(scaleMin, scaleMax, (float)rng.NextDouble()),
                    half.y * Mathf.Lerp(scaleMin, scaleMax, (float)rng.NextDouble()));

                // 방이 쓸 수 있는 영역 안에서만 중심을 흔든다. 경계 폭이 줄지 않는다.
                Vector2 free = (half - room.Radius) * Mathf.Clamp01(settings.CenterJitter);
                room.Center = cellCenter + new Vector2(
                    free.x * ((float)rng.NextDouble() * 2f - 1f),
                    free.y * ((float)rng.NextDouble() * 2f - 1f));

                room.WobbleAmplitude = new float[WobbleHarmonics.Length];
                room.WobblePhase = new float[WobbleHarmonics.Length];
                float total = 0f;
                for (int i = 0; i < WobbleHarmonics.Length; i++)
                {
                    room.WobbleAmplitude[i] = (float)rng.NextDouble();
                    room.WobblePhase[i] = (float)rng.NextDouble() * Mathf.PI * 2f;
                    total += room.WobbleAmplitude[i];
                }

                for (int i = 0; i < WobbleHarmonics.Length; i++)
                {
                    room.WobbleAmplitude[i] = total > 0f ? room.WobbleAmplitude[i] / total : 0f;
                }
            }

            Rooms.Add(room);
            return room;
        }

        private void CarveRoom(WalkableRoom room, WalkableMapSettings settings)
        {
            var cellCenter = new Vector2(room.Data.Cell.x * CellPitch.x, room.Data.Cell.y * CellPitch.y);
            Vector2 half = settings.RoomSize * 0.5f;

            TryGetCell(cellCenter - half, out int minX, out int minY);
            TryGetCell(cellCenter + half, out int maxX, out int maxY);

            for (int x = Mathf.Max(0, minX); x <= Mathf.Min(Width - 1, maxX); x++)
            {
                for (int y = Mathf.Max(0, minY); y <= Mathf.Min(Height - 1, maxY); y++)
                {
                    Vector2 position = CellCenter(x, y);
                    Vector2 fromCell = position - cellCenter;

                    // 어떤 모양이든 방에 주어진 사각형을 넘지 않는다.
                    if (Mathf.Abs(fromCell.x) > half.x || Mathf.Abs(fromCell.y) > half.y)
                    {
                        continue;
                    }

                    if (settings.Shape == WalkableShape.Rectangle || IsInsideClearing(room, position, settings))
                    {
                        Owner[x, y] = room.Index;
                    }
                }
            }
        }

        private static bool IsInsideClearing(WalkableRoom room, Vector2 position, WalkableMapSettings settings)
        {
            Vector2 offset = position - room.Center;
            float nx = offset.x / Mathf.Max(room.Radius.x, 0.01f);
            float ny = offset.y / Mathf.Max(room.Radius.y, 0.01f);
            float angle = Mathf.Atan2(ny, nx);

            float outline = 1f;
            for (int i = 0; i < WobbleHarmonics.Length; i++)
            {
                outline += settings.OutlineWobble * room.WobbleAmplitude[i]
                           * Mathf.Sin(WobbleHarmonics[i] * angle + room.WobblePhase[i]);
            }

            return nx * nx + ny * ny <= outline * outline;
        }

        private void CarvePath(WalkableRoom from, WalkableRoom to, WalkableMapSettings settings, System.Random rng)
        {
            Vector2 direction = to.Center - from.Center;
            var side = new Vector2(-direction.y, direction.x).normalized;
            float bend = settings.PathBend * ((float)rng.NextDouble() * 2f - 1f);
            Vector2 middle = (from.Center + to.Center) * 0.5f + side * bend;

            CarveSegment(from.Center, middle, settings.PathWidth * 0.5f);
            CarveSegment(middle, to.Center, settings.PathWidth * 0.5f);
        }

        private void CarveSegment(Vector2 a, Vector2 b, float halfWidth)
        {
            TryGetCell(Vector2.Min(a, b) - Vector2.one * halfWidth, out int minX, out int minY);
            TryGetCell(Vector2.Max(a, b) + Vector2.one * halfWidth, out int maxX, out int maxY);
            Vector2 ab = b - a;
            float lengthSquared = Mathf.Max(ab.sqrMagnitude, 0.0001f);

            for (int x = Mathf.Max(0, minX); x <= Mathf.Min(Width - 1, maxX); x++)
            {
                for (int y = Mathf.Max(0, minY); y <= Mathf.Min(Height - 1, maxY); y++)
                {
                    if (Owner[x, y] != Blocked)
                    {
                        continue;
                    }

                    Vector2 position = CellCenter(x, y);
                    float t = Mathf.Clamp01(Vector2.Dot(position - a, ab) / lengthSquared);
                    if ((a + ab * t - position).sqrMagnitude <= halfWidth * halfWidth)
                    {
                        Owner[x, y] = Path;
                    }
                }
            }
        }

        private void CountCells()
        {
            WalkableCellCount = 0;
            PathCellCount = 0;
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (Owner[x, y] == Blocked)
                    {
                        continue;
                    }

                    WalkableCellCount++;
                    if (Owner[x, y] == Path)
                    {
                        PathCellCount++;
                    }
                }
            }
        }

        // ---------- 적 스폰 ----------

        /// <summary>
        /// 전투 방마다 적 스폰 위치를 고른다. 경계·입구·장애물에서 떨어지고 서로도 떨어진 칸의 중심이다.
        /// 맵 생성과 다른 난수를 받아, 스폰 규칙을 바꿔도 방 모양과 장애물은 그대로 유지된다.
        /// </summary>
        public void PlaceEnemySpawns(WalkableSpawnSettings settings, System.Random rng)
        {
            EnemySpawns.Clear();
            var candidatesByRoom = new List<Vector2>[Rooms.Count];

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    int owner = Owner[x, y];
                    if (owner < 0 || Rooms[owner].Data.Role != DungeonRoomRole.Combat
                        || Clearance[x, y] - 0.5f < settings.EdgeMargin)
                    {
                        continue;
                    }

                    Vector2 position = CellCenter(x, y);
                    if (IsNearEntrance(position, settings.EntranceClearance)
                        || IsInsideObstacle(position, settings.ObstacleClearance))
                    {
                        continue;
                    }

                    if (candidatesByRoom[owner] == null)
                    {
                        candidatesByRoom[owner] = new List<Vector2>();
                    }

                    candidatesByRoom[owner].Add(position);
                }
            }

            int countMin = Mathf.Max(0, Mathf.Min(settings.Count.x, settings.Count.y));
            int countMax = Mathf.Max(settings.Count.x, settings.Count.y);
            foreach (WalkableRoom room in Rooms)
            {
                List<Vector2> candidates = candidatesByRoom[room.Index];
                if (candidates == null)
                {
                    continue;
                }

                for (int i = candidates.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
                }

                int count = rng.Next(countMin, countMax + 1);
                int firstIndex = EnemySpawns.Count;
                foreach (Vector2 position in candidates)
                {
                    if (EnemySpawns.Count - firstIndex >= count)
                    {
                        break;
                    }

                    bool tooClose = false;
                    for (int i = firstIndex; i < EnemySpawns.Count; i++)
                    {
                        if ((EnemySpawns[i].Position - position).sqrMagnitude < settings.Spacing * settings.Spacing)
                        {
                            tooClose = true;
                            break;
                        }
                    }

                    if (!tooClose)
                    {
                        EnemySpawns.Add(new WalkableSpawn { Position = position, RoomIndex = room.Index });
                    }
                }
            }
        }

        /// <summary>위치가 들어 있는 방의 번호. 길이나 막힌 칸, 맵 밖이면 -1.</summary>
        public int RoomIndexAt(Vector2 position)
        {
            return TryGetCell(position, out int x, out int y) ? Mathf.Max(Owner[x, y], -1) : -1;
        }

        // ---------- 입구 ----------

        /// <summary>
        /// 방 칸과 맞닿은(대각선 포함) 길 칸을 방마다 모으고, 서로 이어진 것끼리 입구 하나로 묶는다.
        /// 길 칸은 방 바깥에만 파이므로, 방에서 길로 가려면 반드시 이 칸들 중 하나를 지난다.
        /// </summary>
        private void FindEntrances()
        {
            var cellsByRoom = new Dictionary<int, HashSet<Vector2Int>>();
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (Owner[x, y] != Path)
                    {
                        continue;
                    }

                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int nx = x + dx;
                            int ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= Width || ny >= Height || Owner[nx, ny] < 0)
                            {
                                continue;
                            }

                            if (!cellsByRoom.TryGetValue(Owner[nx, ny], out HashSet<Vector2Int> cells))
                            {
                                cells = new HashSet<Vector2Int>();
                                cellsByRoom.Add(Owner[nx, ny], cells);
                            }

                            cells.Add(new Vector2Int(x, y));
                        }
                    }
                }
            }

            // 방 번호 순으로 만들어 같은 시드에서 항상 같은 순서가 되게 한다.
            for (int roomIndex = 0; roomIndex < Rooms.Count; roomIndex++)
            {
                if (!cellsByRoom.TryGetValue(roomIndex, out HashSet<Vector2Int> remaining))
                {
                    continue;
                }

                var sorted = new List<Vector2Int>(remaining);
                sorted.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
                foreach (Vector2Int seed in sorted)
                {
                    if (!remaining.Remove(seed))
                    {
                        continue;
                    }

                    var entrance = new WalkableEntrance { RoomIndex = roomIndex };
                    var queue = new Queue<Vector2Int>();
                    queue.Enqueue(seed);
                    while (queue.Count > 0)
                    {
                        Vector2Int cell = queue.Dequeue();
                        entrance.Cells.Add(cell);
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                var next = new Vector2Int(cell.x + dx, cell.y + dy);
                                if (remaining.Remove(next))
                                {
                                    queue.Enqueue(next);
                                }
                            }
                        }
                    }

                    FitEntranceBox(entrance);
                    Entrances.Add(entrance);
                }
            }
        }

        /// <summary>입구 칸들을 빠짐없이 덮는, 길 방향으로 회전된 직사각형을 구한다.</summary>
        private void FitEntranceBox(WalkableEntrance entrance)
        {
            Vector2 sum = Vector2.zero;
            foreach (Vector2Int cell in entrance.Cells)
            {
                sum += CellCenter(cell.x, cell.y);
            }

            Vector2 mean = sum / entrance.Cells.Count;
            // 길은 방 중심에서 뻗어 나가므로, 방 중심에서 입구를 향하는 방향이 곧 길의 방향이다.
            Vector2 direction = mean - Rooms[entrance.RoomIndex].Center;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
            var side = new Vector2(-direction.y, direction.x);

            float alongMin = float.MaxValue;
            float alongMax = float.MinValue;
            float sideMin = float.MaxValue;
            float sideMax = float.MinValue;
            foreach (Vector2Int cell in entrance.Cells)
            {
                Vector2 offset = CellCenter(cell.x, cell.y) - mean;
                float along = Vector2.Dot(offset, direction);
                float across = Vector2.Dot(offset, side);
                alongMin = Mathf.Min(alongMin, along);
                alongMax = Mathf.Max(alongMax, along);
                sideMin = Mathf.Min(sideMin, across);
                sideMax = Mathf.Max(sideMax, across);
            }

            // 회전된 칸(한 변 1)의 꼭짓점까지 덮으려면 중심에서 0.71씩 더 넓혀야 한다.
            const float cellReach = 0.71f;
            entrance.Direction = direction;
            entrance.Center = mean + direction * ((alongMin + alongMax) * 0.5f) + side * ((sideMin + sideMax) * 0.5f);
            entrance.Length = alongMax - alongMin + cellReach * 2f;
            entrance.Width = sideMax - sideMin + cellReach * 2f;
        }

        public bool IsNearEntrance(Vector2 position, float margin)
        {
            foreach (WalkableEntrance entrance in Entrances)
            {
                if (IsInsideEntranceBox(entrance, position, margin))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>점이 입구 상자 안에 있는지. 여유(margin)만큼 상자를 키워 본다.</summary>
        public static bool IsInsideEntranceBox(WalkableEntrance entrance, Vector2 position, float margin = 0f)
        {
            Vector2 offset = position - entrance.Center;
            var side = new Vector2(-entrance.Direction.y, entrance.Direction.x);
            return Mathf.Abs(Vector2.Dot(offset, entrance.Direction)) <= entrance.Length * 0.5f + margin
                   && Mathf.Abs(Vector2.Dot(offset, side)) <= entrance.Width * 0.5f + margin;
        }

        // ---------- 거리 ----------

        private void ComputeDistances()
        {
            DistanceToWalkable = new float[Width, Height];
            Clearance = new float[Width, Height];
            float far = Width + Height;

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    bool walkable = IsWalkable(x, y);
                    DistanceToWalkable[x, y] = walkable ? 0f : far;
                    Clearance[x, y] = walkable ? far : 0f;
                }
            }

            Sweep(DistanceToWalkable);
            Sweep(Clearance);
        }

        /// <summary>두 번 훑어 가장 가까운 0까지의 거리를 근사한다(직선 1, 대각선 1.41).</summary>
        private void Sweep(float[,] field)
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    float value = field[x, y];
                    if (x > 0) value = Mathf.Min(value, field[x - 1, y] + 1f);
                    if (y > 0) value = Mathf.Min(value, field[x, y - 1] + 1f);
                    if (x > 0 && y > 0) value = Mathf.Min(value, field[x - 1, y - 1] + Diagonal);
                    if (x < Width - 1 && y > 0) value = Mathf.Min(value, field[x + 1, y - 1] + Diagonal);
                    field[x, y] = value;
                }
            }

            for (int y = Height - 1; y >= 0; y--)
            {
                for (int x = Width - 1; x >= 0; x--)
                {
                    float value = field[x, y];
                    if (x < Width - 1) value = Mathf.Min(value, field[x + 1, y] + 1f);
                    if (y < Height - 1) value = Mathf.Min(value, field[x, y + 1] + 1f);
                    if (x < Width - 1 && y < Height - 1) value = Mathf.Min(value, field[x + 1, y + 1] + Diagonal);
                    if (x > 0 && y < Height - 1) value = Mathf.Min(value, field[x - 1, y + 1] + Diagonal);
                    field[x, y] = value;
                }
            }
        }

        // ---------- 장애물 ----------

        /// <summary>
        /// 전투 방 안에서, 경계와 떨어지고 서로도 떨어진 점을 골라 장애물을 놓는다.
        /// 장애물 둘레에 항상 여유가 남으므로 방 안의 이동이 막히지 않는다.
        /// </summary>
        private void PlaceObstacles(WalkableMapSettings settings, System.Random rng)
        {
            var candidatesByRoom = new List<Vector2Int>[Rooms.Count];
            float radiusMin = Mathf.Min(settings.ObstacleRadius.x, settings.ObstacleRadius.y);
            float radiusMax = Mathf.Max(settings.ObstacleRadius.x, settings.ObstacleRadius.y);

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    int owner = Owner[x, y];
                    // Clearance는 칸 중심 사이의 거리라서, 경계 면까지의 거리는 0.5만큼 짧다.
                    if (owner < 0 || Rooms[owner].Data.Role != DungeonRoomRole.Combat
                        || Clearance[x, y] - 0.5f < settings.ObstacleMargin + radiusMin)
                    {
                        continue;
                    }

                    // 방 중심은 비워 둔다. 길이 중심을 향해 들어오고 표식도 중심에 놓인다.
                    float centerKeepOut = radiusMax + 2f;
                    if ((CellCenter(x, y) - Rooms[owner].Center).sqrMagnitude < centerKeepOut * centerKeepOut)
                    {
                        continue;
                    }

                    // 입구 앞은 비워 둔다. 잠금 오브젝트와 겹치거나 드나드는 길목을 막지 않게 한다.
                    if (IsNearEntrance(CellCenter(x, y), radiusMax + 1f))
                    {
                        continue;
                    }

                    if (candidatesByRoom[owner] == null)
                    {
                        candidatesByRoom[owner] = new List<Vector2Int>();
                    }

                    candidatesByRoom[owner].Add(new Vector2Int(x, y));
                }
            }

            foreach (WalkableRoom room in Rooms)
            {
                List<Vector2Int> candidates = candidatesByRoom[room.Index];
                if (candidates == null)
                {
                    continue;
                }

                for (int i = candidates.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
                }

                int count = rng.Next(settings.ObstacleCount.x,
                    Mathf.Max(settings.ObstacleCount.x, settings.ObstacleCount.y) + 1);
                int firstIndex = Obstacles.Count;

                foreach (Vector2Int cell in candidates)
                {
                    if (Obstacles.Count - firstIndex >= count)
                    {
                        break;
                    }

                    Vector2 position = CellCenter(cell.x, cell.y);
                    // 경계와의 여유를 지킬 수 있는 가장 큰 반지름까지만 허용한다.
                    float limit = Mathf.Min(radiusMax, Clearance[cell.x, cell.y] - 0.5f - settings.ObstacleMargin);
                    float radius = Mathf.Lerp(radiusMin, Mathf.Max(radiusMin, limit), (float)rng.NextDouble());

                    bool tooClose = false;
                    for (int i = firstIndex; i < Obstacles.Count; i++)
                    {
                        if ((Obstacles[i].Position - position).sqrMagnitude
                            < settings.ObstacleSpacing * settings.ObstacleSpacing)
                        {
                            tooClose = true;
                            break;
                        }
                    }

                    if (!tooClose)
                    {
                        Obstacles.Add(new WalkableObstacle { Position = position, Radius = radius, RoomIndex = room.Index });
                    }
                }
            }
        }
    }
}
