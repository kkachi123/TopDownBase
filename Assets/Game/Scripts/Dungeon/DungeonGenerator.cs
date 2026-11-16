using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace TopDownBase.Dungeon
{
    /// <summary>
    /// 보스 전 던전 구간을 생성하는 프로토타입.
    /// 방 배치(DungeonLayout) → 방 조립(바닥·벽·문) → 장애물 스탬프 → 스폰 위치 → NavMesh 순서로 만든다.
    /// 모든 형상은 기본 도형이며, 생성물은 "Generated" 자식 아래에만 놓인다.
    /// Play 중에는 물론 에디터(편집 모드)에서도 생성할 수 있고, 생성물은 씬에 저장할 수 있다.
    /// </summary>
    [DisallowMultipleComponent]
    public class DungeonGenerator : MonoBehaviour
    {
        private const string GeneratedRootName = "Generated";
        private const float FloorThickness = 0.2f;
        private const int PlacementTries = 20;

        [Header("Seed")]
        public int Seed = 12345;
        [Tooltip("Play 시작 시 새 시드를 뽑는다. GenerateOnAwake가 켜져 있을 때만 쓰인다.")]
        public bool RandomizeSeedOnAwake = true;
        [Tooltip("켜면 Play 시작 시 다시 생성한다. 끄면 씬에 저장된 맵을 그대로 쓴다.")]
        public bool GenerateOnAwake = true;

        [Header("Layout")]
        public Vector2Int GridSize = new Vector2Int(5, 5);
        [Min(2)] public int RoomCount = 8;
        [Tooltip("1 = Recursive Backtracking(일자형), 0 = 가지가 많은 방사형")]
        [Range(0f, 1f)] public float NewestPickChance = 0.6f;
        [Tooltip("시작 방에서 보스 입구까지의 최소 방 수(양 끝 포함)")]
        [Min(2)] public int MinBossPathLength = 5;
        [Min(0)] public int MinItemRooms = 1;
        [Min(1)] public int MaxLayoutAttempts = 50;

        [Header("Room (tile units)")]
        [Min(0.5f)] public float TileSize = 2f;
        public Vector2Int RoomTiles = new Vector2Int(12, 10);
        [Min(1)] public int DoorTiles = 3;
        [Min(0.1f)] public float WallThickness = 1f;
        [Min(0.1f)] public float WallHeight = 2f;

        [Header("Obstacles (combat rooms)")]
        public Vector2Int ObstacleCount = new Vector2Int(2, 4);
        [Tooltip("벽에서 띄우는 칸 수. 방 둘레에 항상 빈 통로가 남는다.")]
        [Min(1)] public int ObstacleWallMargin = 2;
        [Tooltip("스탬프 기준점 사이의 최소 칸 수. 스탬프끼리 붙어 길을 막지 않게 한다.")]
        [Min(4)] public int ObstacleSpacing = 5;
        [Min(0.1f)] public float ObstacleHeight = 1.5f;

        [Header("Enemy spawns (combat rooms)")]
        public Vector2Int EnemySpawnCount = new Vector2Int(2, 4);
        [Min(0)] public int SpawnDoorClearance = 3;
        [Min(1)] public int SpawnSpacing = 2;

        [Header("NavMesh")]
        public bool BuildNavMesh = true;

        [Header("Look")]
        [Tooltip("비워 두면 현재 렌더 파이프라인의 기본 머티리얼을 쓴다.")]
        public Material BaseMaterial;
        public Color FloorColor = new Color(0.45f, 0.42f, 0.38f);
        public Color WallColor = new Color(0.25f, 0.25f, 0.28f);
        public Color ObstacleColor = new Color(0.55f, 0.4f, 0.25f);
        public Color DoorBlockerColor = new Color(0.8f, 0.2f, 0.2f);
        public Color StartColor = new Color(0.2f, 0.75f, 0.3f);
        public Color EnemyColor = new Color(0.85f, 0.2f, 0.2f);
        public Color ItemColor = new Color(0.95f, 0.8f, 0.2f);
        public Color BossGateColor = new Color(0.6f, 0.25f, 0.85f);

        /// <summary>기둥·낮은 벽 등 작은 장애물 묶음. 칸 좌표는 기준점(왼쪽 아래)에서의 오프셋이다.</summary>
        private static readonly Vector2Int[][] Stamps =
        {
            new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) },
            new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) },
            new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2) },
            new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1) },
            new[] { new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, 0) }
        };

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        // 색상별로 하나씩 만든 머티리얼. 씬에 함께 저장되어 다시 열어도 색이 유지되고, Clear에서 지운다.
        [SerializeField, HideInInspector] private List<Material> _generatedMaterials = new List<Material>();

        // 생성 결과에 대한 참조. 씬에 함께 저장되므로 저장된 맵을 다시 쓸 때 이름이나 타입으로 찾지 않는다.
        [SerializeField, HideInInspector] private Transform _generatedRoot;
        [SerializeField, HideInInspector] private List<DungeonRoom> _rooms = new List<DungeonRoom>();
        [SerializeField, HideInInspector] private DungeonRoom _startRoom;
        [SerializeField, HideInInspector] private DungeonRoom _bossGateRoom;
        [SerializeField, HideInInspector] private NavMeshSurface _navMeshSurface;

        private readonly Dictionary<Color, Material> _materialsByColor = new Dictionary<Color, Material>();

        public DungeonLayout Layout { get; private set; }
        public IReadOnlyList<DungeonRoom> Rooms => _rooms;
        public DungeonRoom StartRoom => _startRoom;
        public DungeonRoom BossGateRoom => _bossGateRoom;

        /// <summary>씬에 생성물이 있는지. 에디터에서 만든 뒤 저장한 맵도 포함한다.</summary>
        public bool HasGeneratedMap => _generatedRoot != null;

        /// <summary>마지막 생성(또는 저장된 맵 인식)의 한 줄 요약. 인스펙터 표시용이며 저장되지 않는다.</summary>
        public string LastReport { get; private set; }

        /// <summary>플레이어를 놓을 위치(시작 방 중앙).</summary>
        public Vector3 StartPosition => StartRoom != null ? StartRoom.transform.position : transform.position;

        /// <summary>방 중심 사이의 간격. 방 안쪽 크기에 벽 두께를 더한 값이다.</summary>
        private Vector2 CellPitch => InteriorSize + Vector2.one * WallThickness;

        private Vector2 InteriorSize => new Vector2(RoomTiles.x * TileSize, RoomTiles.y * TileSize);

        private void OnValidate()
        {
            // 문과 벽을 만들 수 있는 최소 크기를 보장한다.
            GridSize = Vector2Int.Max(GridSize, Vector2Int.one);
            RoomTiles = Vector2Int.Max(RoomTiles, new Vector2Int(4, 4));
            ObstacleCount = Vector2Int.Max(ObstacleCount, Vector2Int.zero);
            EnemySpawnCount = Vector2Int.Max(EnemySpawnCount, Vector2Int.zero);
        }

        private void Awake()
        {
            if (!GenerateOnAwake)
            {
                AdoptExistingMap();
                return;
            }

            if (RandomizeSeedOnAwake)
            {
                Seed = System.Environment.TickCount;
            }

            Generate();
        }

        public void GenerateWithRandomSeed()
        {
            Seed = System.Environment.TickCount;
            Generate();
        }

        public void Generate()
        {
            Clear();

            var rng = new System.Random(Seed);
            Layout = DungeonLayout.Generate(new DungeonLayoutSettings
            {
                GridSize = GridSize,
                RoomCount = RoomCount,
                NewestPickChance = NewestPickChance,
                MinBossPathLength = MinBossPathLength,
                MinItemRooms = MinItemRooms,
                MaxAttempts = MaxLayoutAttempts
            }, rng);

            Transform root = new GameObject(GeneratedRootName).transform;
            root.SetParent(transform, false);
            _generatedRoot = root;

            var roomsByCell = new Dictionary<Vector2Int, DungeonRoom>();
            foreach (DungeonLayoutRoom data in Layout.Rooms)
            {
                DungeonRoom room = BuildRoom(data, root, rng);
                roomsByCell.Add(data.Cell, room);
                _rooms.Add(room);
            }

            _startRoom = roomsByCell[Layout.StartCell];
            _bossGateRoom = roomsByCell[Layout.BossGateCell];

            foreach (DungeonLayoutRoom data in Layout.Rooms)
            {
                BuildWalls(data, roomsByCell);
            }

            BuildCornerPosts(root);

            string navMeshReport = "skipped";
            if (BuildNavMesh)
            {
                navMeshReport = BakeAndCheckNavMesh();
            }

            LastReport = $"seed {Seed}: rooms {Layout.Rooms.Count}, boss path {Layout.BossPathLength}, " +
                         $"item rooms {Layout.ItemRoomCount}, layout attempts {Layout.Attempts}, " +
                         $"requirements met {Layout.MeetsRequirements}, navmesh {navMeshReport}";
            Debug.Log($"[Dungeon] {LastReport}", this);

            if (!Layout.MeetsRequirements)
            {
                Debug.LogWarning("[Dungeon] 조건을 만족하는 배치를 찾지 못해 가장 가까운 배치를 사용했습니다. " +
                                 "RoomCount, GridSize, MinBossPathLength, MinItemRooms 값을 확인하세요.", this);
            }
        }

        /// <summary>
        /// 다시 생성하지 않고 씬에 저장된 맵을 그대로 쓴다. 저장된 방 참조를 그대로 쓰고 NavMesh만 새로 굽는다.
        /// 에디터에서 만든 맵을 손으로 고쳐 쓰는 경우를 위한 경로이며, 이때 Layout은 비어 있다.
        /// </summary>
        public bool AdoptExistingMap()
        {
            Layout = null;

            if (_generatedRoot == null)
            {
                _rooms.Clear();
                _startRoom = null;
                _bossGateRoom = null;
                LastReport = null;
                return false;
            }

            // 에디터에서 손으로 지운 방은 목록에서 뺀다.
            _rooms.RemoveAll(room => room == null);

            string navMeshReport = BuildNavMesh ? BakeAndCheckNavMesh() : "skipped";
            LastReport = $"saved map: rooms {_rooms.Count}, navmesh {navMeshReport}";
            Debug.Log($"[Dungeon] {LastReport}", this);
            return true;
        }

        public void Clear()
        {
            _rooms.Clear();
            _startRoom = null;
            _bossGateRoom = null;
            Layout = null;
            LastReport = null;

            if (_navMeshSurface != null)
            {
                ReleaseNavMeshData(_navMeshSurface);
            }

            if (_generatedRoot != null)
            {
                // 같은 프레임에 다시 생성하므로 이전 생성물이 NavMesh 수집에 잡히지 않도록 즉시 지운다.
                DestroyImmediate(_generatedRoot.gameObject);
                _generatedRoot = null;
            }

            foreach (Material material in _generatedMaterials)
            {
                if (material != null)
                {
                    DestroyImmediate(material);
                }
            }

            _generatedMaterials.Clear();
            _materialsByColor.Clear();
        }

        /// <summary>던전 전체를 감싸는 월드 영역. 전체 보기 카메라에 쓴다.</summary>
        public Bounds GetWorldBounds()
        {
            var bounds = new Bounds(transform.position, Vector3.zero);
            bool first = true;
            foreach (DungeonRoom room in _rooms)
            {
                var roomBounds = new Bounds(room.transform.position,
                    new Vector3(CellPitch.x, WallHeight, CellPitch.y));
                if (first)
                {
                    bounds = roomBounds;
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(roomBounds);
                }
            }

            return bounds;
        }

        // ---------- 방 조립 ----------

        private DungeonRoom BuildRoom(DungeonLayoutRoom data, Transform root, System.Random rng)
        {
            var roomObject = new GameObject($"Room_{data.Cell.x}_{data.Cell.y}_{data.Role}");
            Transform roomTransform = roomObject.transform;
            roomTransform.SetParent(root, false);
            roomTransform.localPosition = new Vector3(data.Cell.x * CellPitch.x, 0f, data.Cell.y * CellPitch.y);

            var room = roomObject.AddComponent<DungeonRoom>();
            room.Initialize(data.Role, data.Cell, data.DistanceFromStart, InteriorSize);

            // 바닥은 벽 아래와 문턱까지 덮도록 방 간격 크기로 만든다. 역할 색을 옅게 섞어 전체 보기에서 구분한다.
            Color floorColor = Color.Lerp(FloorColor, RoleColor(data.Role), data.Role == DungeonRoomRole.Combat ? 0f : 0.3f);
            CreateBox("Floor", roomTransform,
                new Vector3(0f, -FloorThickness * 0.5f, 0f),
                new Vector3(CellPitch.x, FloorThickness, CellPitch.y),
                floorColor, true);

            switch (data.Role)
            {
                case DungeonRoomRole.Combat:
                    bool[,] occupied = PlaceObstacles(roomTransform, rng);
                    PlaceEnemySpawns(data, room, occupied, rng);
                    break;
                case DungeonRoomRole.Start:
                    CreateMarker("PlayerStart", roomTransform, Vector3.zero, StartColor);
                    break;
                case DungeonRoomRole.Item:
                    CreateMarker("ItemSpawn", roomTransform, Vector3.zero, ItemColor);
                    break;
                case DungeonRoomRole.BossGate:
                    CreateMarker("BossGate", roomTransform, Vector3.zero, BossGateColor);
                    break;
            }

            return room;
        }

        /// <summary>
        /// 스탬프를 벽에서 ObstacleWallMargin칸, 서로 ObstacleSpacing칸 이상 띄워 놓는다.
        /// 이 두 규칙만으로 빈 칸이 모두 이어지므로 도달 가능성 검사를 따로 하지 않는다.
        /// </summary>
        private bool[,] PlaceObstacles(Transform roomTransform, System.Random rng)
        {
            var occupied = new bool[RoomTiles.x, RoomTiles.y];
            var origins = new List<Vector2Int>();
            int count = rng.Next(ObstacleCount.x, Mathf.Max(ObstacleCount.x, ObstacleCount.y) + 1);

            for (int i = 0; i < count; i++)
            {
                for (int attempt = 0; attempt < PlacementTries; attempt++)
                {
                    Vector2Int[] stamp = Stamps[rng.Next(Stamps.Length)];
                    Vector2Int size = StampSize(stamp);
                    int maxX = RoomTiles.x - ObstacleWallMargin - size.x;
                    int maxY = RoomTiles.y - ObstacleWallMargin - size.y;
                    if (maxX < ObstacleWallMargin || maxY < ObstacleWallMargin)
                    {
                        continue;
                    }

                    var origin = new Vector2Int(
                        rng.Next(ObstacleWallMargin, maxX + 1),
                        rng.Next(ObstacleWallMargin, maxY + 1));

                    if (!IsFarFromAll(origin, origins, ObstacleSpacing))
                    {
                        continue;
                    }

                    origins.Add(origin);
                    foreach (Vector2Int offset in stamp)
                    {
                        Vector2Int tile = origin + offset;
                        occupied[tile.x, tile.y] = true;
                        Vector3 position = TileToLocal(tile);
                        position.y = ObstacleHeight * 0.5f;
                        CreateBox("Obstacle", roomTransform, position,
                            new Vector3(TileSize, ObstacleHeight, TileSize), ObstacleColor, true);
                    }

                    break;
                }
            }

            return occupied;
        }

        private void PlaceEnemySpawns(DungeonLayoutRoom data, DungeonRoom room, bool[,] occupied, System.Random rng)
        {
            // 문 앞은 비워 둔다. 방에 들어서자마자 적과 겹치지 않게 하기 위함이다.
            var doorPositions = new List<Vector3>();
            foreach (Vector2Int neighbour in data.Neighbours)
            {
                Vector2Int direction = neighbour - data.Cell;
                doorPositions.Add(new Vector3(direction.x * CellPitch.x * 0.5f, 0f, direction.y * CellPitch.y * 0.5f));
            }

            float doorClearance = SpawnDoorClearance * TileSize;
            var candidates = new List<Vector2Int>();
            for (int x = 0; x < RoomTiles.x; x++)
            {
                for (int y = 0; y < RoomTiles.y; y++)
                {
                    if (occupied[x, y])
                    {
                        continue;
                    }

                    Vector3 position = TileToLocal(new Vector2Int(x, y));
                    bool nearDoor = false;
                    foreach (Vector3 door in doorPositions)
                    {
                        if (Vector3.Distance(position, door) < doorClearance)
                        {
                            nearDoor = true;
                            break;
                        }
                    }

                    if (!nearDoor)
                    {
                        candidates.Add(new Vector2Int(x, y));
                    }
                }
            }

            Shuffle(candidates, rng);

            int count = rng.Next(EnemySpawnCount.x, Mathf.Max(EnemySpawnCount.x, EnemySpawnCount.y) + 1);
            var chosen = new List<Vector2Int>();
            foreach (Vector2Int tile in candidates)
            {
                if (chosen.Count >= count)
                {
                    break;
                }

                if (!IsFarFromAll(tile, chosen, SpawnSpacing))
                {
                    continue;
                }

                chosen.Add(tile);
                GameObject marker = CreateMarker("EnemySpawn", room.transform, TileToLocal(tile), EnemyColor);
                room.AddEnemySpawnPoint(marker.transform);
            }
        }

        // ---------- 벽과 문 ----------

        private void BuildWalls(DungeonLayoutRoom data, Dictionary<Vector2Int, DungeonRoom> roomsByCell)
        {
            DungeonRoom room = roomsByCell[data.Cell];

            foreach (Vector2Int direction in DungeonLayout.Directions)
            {
                Vector2Int neighbourCell = data.Cell + direction;
                bool neighbourExists = roomsByCell.ContainsKey(neighbourCell);

                // 방 사이 벽은 한 번만 만든다: 동·북쪽은 자신이, 서·남쪽은 이웃이 없을 때만 만든다.
                bool ownsWall = direction.x > 0 || direction.y > 0 || !neighbourExists;
                if (!ownsWall)
                {
                    continue;
                }

                bool alongZ = direction.x != 0;
                float length = alongZ ? InteriorSize.y : InteriorSize.x;
                Vector3 axis = alongZ ? Vector3.forward : Vector3.right;
                var center = new Vector3(
                    direction.x * CellPitch.x * 0.5f,
                    WallHeight * 0.5f,
                    direction.y * CellPitch.y * 0.5f);

                if (!data.Neighbours.Contains(neighbourCell))
                {
                    CreateWall(room.transform, center, length, alongZ);
                    continue;
                }

                float doorWidth = Mathf.Min(DoorTiles * TileSize, length - TileSize);
                float segmentLength = (length - doorWidth) * 0.5f;
                float segmentOffset = (doorWidth + segmentLength) * 0.5f;
                CreateWall(room.transform, center + axis * segmentOffset, segmentLength, alongZ);
                CreateWall(room.transform, center - axis * segmentOffset, segmentLength, alongZ);

                // 문 차단물은 꺼 둔 채로 만들어 두고, 방 잠금 시 양쪽 방이 함께 켠다.
                Vector3 blockerSize = alongZ
                    ? new Vector3(WallThickness * 0.6f, WallHeight, doorWidth)
                    : new Vector3(doorWidth, WallHeight, WallThickness * 0.6f);
                GameObject blocker = CreateBox("DoorBlocker", room.transform, center, blockerSize, DoorBlockerColor, true);
                blocker.SetActive(false);
                room.AddDoorBlocker(blocker);
                roomsByCell[neighbourCell].AddDoorBlocker(blocker);
            }
        }

        private void CreateWall(Transform parent, Vector3 center, float length, bool alongZ)
        {
            Vector3 size = alongZ
                ? new Vector3(WallThickness, WallHeight, length)
                : new Vector3(length, WallHeight, WallThickness);
            CreateBox("Wall", parent, center, size, WallColor, true);
        }

        /// <summary>벽은 방 안쪽 길이만큼만 만들고, 모서리는 겹치지 않도록 기둥 하나로 채운다.</summary>
        private void BuildCornerPosts(Transform root)
        {
            Transform posts = new GameObject("CornerPosts").transform;
            posts.SetParent(root, false);

            var built = new HashSet<Vector2Int>();
            foreach (DungeonLayoutRoom data in Layout.Rooms)
            {
                for (int dx = 0; dx <= 1; dx++)
                {
                    for (int dy = 0; dy <= 1; dy++)
                    {
                        var corner = new Vector2Int(data.Cell.x + dx, data.Cell.y + dy);
                        if (!built.Add(corner))
                        {
                            continue;
                        }

                        var position = new Vector3(
                            (corner.x - 0.5f) * CellPitch.x,
                            WallHeight * 0.5f,
                            (corner.y - 0.5f) * CellPitch.y);
                        CreateBox("CornerPost", posts, position,
                            new Vector3(WallThickness, WallHeight, WallThickness), WallColor, true);
                    }
                }
            }
        }

        // ---------- NavMesh ----------

        private string BakeAndCheckNavMesh()
        {
            // 처음 한 번만 컴포넌트를 붙이고, 이후에는 저장된 참조를 쓴다.
            if (_navMeshSurface == null && !TryGetComponent(out _navMeshSurface))
            {
                _navMeshSurface = gameObject.AddComponent<NavMeshSurface>();
            }

            NavMeshSurface surface = _navMeshSurface;
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            Physics.SyncTransforms();
            surface.BuildNavMesh();
            string report = CheckNavMesh();

            if (!Application.isPlaying)
            {
                // 편집 모드에서는 검사만 하고 데이터를 남기지 않는다. 남기면 저장 시 NavMesh가 씬 파일에 통째로 들어간다.
                // Play 시작 시 항상 다시 굽는다.
                ReleaseNavMeshData(surface);
                report += " (edit mode: checked, not kept)";
            }

            return report;
        }

        /// <summary>이 생성기가 구운 NavMesh를 내리고 메모리에서 지운다.</summary>
        private static void ReleaseNavMeshData(NavMeshSurface surface)
        {
            surface.RemoveData();
            NavMeshData data = surface.navMeshData;
            surface.navMeshData = null;
            if (data != null)
            {
                DestroyImmediate(data);
            }
        }

        private string CheckNavMesh()
        {
            if (StartRoom == null || BossGateRoom == null)
            {
                return "built, start/boss room missing";
            }

            float sampleRange = TileSize * 2f;
            if (!NavMesh.SamplePosition(StartRoom.transform.position, out NavMeshHit startHit, sampleRange, NavMesh.AllAreas)
                || !NavMesh.SamplePosition(BossGateRoom.transform.position, out NavMeshHit bossHit, sampleRange, NavMesh.AllAreas))
            {
                Debug.LogWarning("[Dungeon] 시작 방 또는 보스 입구가 NavMesh 위에 있지 않습니다.", this);
                return "start/boss not on navmesh";
            }

            var path = new NavMeshPath();
            NavMesh.CalculatePath(startHit.position, bossHit.position, NavMesh.AllAreas, path);

            int offMeshSpawns = 0;
            int totalSpawns = 0;
            foreach (DungeonRoom room in _rooms)
            {
                foreach (Transform spawn in room.EnemySpawnPoints)
                {
                    totalSpawns++;
                    if (!NavMesh.SamplePosition(spawn.position, out _, TileSize * 0.5f, NavMesh.AllAreas))
                    {
                        offMeshSpawns++;
                    }
                }
            }

            return $"path start→boss {path.status}, enemy spawns on mesh {totalSpawns - offMeshSpawns}/{totalSpawns}";
        }

        // ---------- 도우미 ----------

        /// <summary>방 안 칸 좌표를 방 기준 위치로 바꾼다. (0,0)은 남서쪽 구석 칸이다.</summary>
        private Vector3 TileToLocal(Vector2Int tile)
        {
            return new Vector3(
                (tile.x + 0.5f) * TileSize - InteriorSize.x * 0.5f,
                0f,
                (tile.y + 0.5f) * TileSize - InteriorSize.y * 0.5f);
        }

        private static Vector2Int StampSize(Vector2Int[] stamp)
        {
            int width = 0;
            int height = 0;
            foreach (Vector2Int offset in stamp)
            {
                width = Mathf.Max(width, offset.x + 1);
                height = Mathf.Max(height, offset.y + 1);
            }

            return new Vector2Int(width, height);
        }

        /// <summary>가로 또는 세로로 minDistance칸 이상 떨어져 있으면 충분히 먼 것으로 본다.</summary>
        private static bool IsFarFromAll(Vector2Int tile, List<Vector2Int> others, int minDistance)
        {
            foreach (Vector2Int other in others)
            {
                if (Mathf.Abs(tile.x - other.x) < minDistance && Mathf.Abs(tile.y - other.y) < minDistance)
                {
                    return false;
                }
            }

            return true;
        }

        private static void Shuffle(List<Vector2Int> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private Color RoleColor(DungeonRoomRole role)
        {
            switch (role)
            {
                case DungeonRoomRole.Start: return StartColor;
                case DungeonRoomRole.Item: return ItemColor;
                case DungeonRoomRole.BossGate: return BossGateColor;
                default: return EnemyColor;
            }
        }

        private GameObject CreateBox(string objectName, Transform parent, Vector3 localPosition, Vector3 size,
            Color color, bool keepCollider)
        {
            return CreatePrimitive(PrimitiveType.Cube, objectName, parent, localPosition, size, color, keepCollider);
        }

        /// <summary>충돌 없는 납작한 원판. 스폰·아이템·보스 입구 위치를 눈으로 확인하는 용도다.</summary>
        private GameObject CreateMarker(string objectName, Transform parent, Vector3 localPosition, Color color)
        {
            localPosition.y = 0.03f;
            float diameter = TileSize * 0.7f;
            return CreatePrimitive(PrimitiveType.Cylinder, objectName, parent, localPosition,
                new Vector3(diameter, 0.02f, diameter), color, false);
        }

        private GameObject CreatePrimitive(PrimitiveType type, string objectName, Transform parent,
            Vector3 localPosition, Vector3 size, Color color, bool keepCollider)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = objectName;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localScale = size;

            if (!keepCollider)
            {
                // NavMesh를 같은 프레임에 굽기 때문에 지연 삭제(Destroy)를 쓰면 표식이 장애물로 잡힌다.
                DestroyImmediate(primitive.GetComponent<Collider>());
            }

            var meshRenderer = primitive.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = GetMaterial(color, meshRenderer.sharedMaterial);

            return primitive;
        }

        /// <summary>같은 색은 머티리얼 하나를 함께 쓴다. 한 번 생성에 만들어지는 머티리얼은 색 종류 수(10개 안팎)다.</summary>
        private Material GetMaterial(Color color, Material primitiveDefault)
        {
            if (_materialsByColor.TryGetValue(color, out Material cached) && cached != null)
            {
                return cached;
            }

            Material source = BaseMaterial;
            if (source == null && GraphicsSettings.currentRenderPipeline != null)
            {
                source = GraphicsSettings.currentRenderPipeline.defaultMaterial;
            }

            if (source == null)
            {
                source = primitiveDefault;
            }

            var material = new Material(source) { name = $"Dungeon_{ColorUtility.ToHtmlStringRGB(color)}" };
            if (material.HasProperty(BaseColorId))
            {
                material.SetColor(BaseColorId, color);
            }

            if (material.HasProperty(ColorId))
            {
                material.SetColor(ColorId, color);
            }

            _materialsByColor[color] = material;
            _generatedMaterials.Add(material);
            return material;
        }
    }
}
