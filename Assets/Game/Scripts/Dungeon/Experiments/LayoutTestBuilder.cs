using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace TopDownBase.Dungeon.Experiments
{
    /// <summary>
    /// 맵 경계 표현 테스트의 공통 기반. 게임 기획에 포함된 기능이 아니라 배치를 비교해 보기 위한 실험이다.
    /// 방 그래프(DungeonLayout) → 걸을 수 있는 영역 지도(WalkableMap) → 경계(하위 클래스) → 장애물과 표식 → NavMesh.
    /// 생성물은 씬에 저장하지 않는다. Play 시작과 인스펙터 버튼으로 다시 만든다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(NavMeshSurface))]
    public abstract class LayoutTestBuilder : DungeonMapSource
    {
        private const string GeneratedRootName = "Generated";
        private const int MapSeedSalt = 0x4D4150;

        [Header("Seed")]
        public int Seed = 12345;
        public bool RandomizeSeedOnAwake = true;
        public bool GenerateOnAwake = true;

        [Header("Layout")]
        public Vector2Int GridSize = new Vector2Int(5, 5);
        [Min(2)] public int RoomCount = 8;
        [Range(0f, 1f)] public float NewestPickChance = 0.6f;
        [Min(2)] public int MinBossPathLength = 5;
        [Min(0)] public int MinItemRooms = 1;
        [Min(1)] public int MaxLayoutAttempts = 50;

        [Header("Shape")]
        public WalkableShape Shape = WalkableShape.Rectangle;
        [Tooltip("방 하나가 쓸 수 있는 최대 크기(유닛). 현재 던전의 방 안쪽 크기와 같다.")]
        public Vector2 RoomSize = new Vector2(24f, 20f);
        [Tooltip("이웃한 방 사이에 남기는 경계의 폭(유닛). 방 간격은 RoomSize + BoundaryWidth다.")]
        [Min(1f)] public float BoundaryWidth = 5f;
        [Tooltip("공터 크기 배율의 (최소, 최대). Clearing 모양에서만 쓴다.")]
        public Vector2 ClearingScale = new Vector2(0.8f, 1f);
        [Range(0f, 1f)] public float CenterJitter = 1f;
        [Range(0f, 0.4f)] public float OutlineWobble = 0.2f;
        [Min(2f)] public float PathWidth = 6f;
        [Min(0f)] public float PathBend;
        [Min(2f)] public float OuterMargin = 10f;

        [Header("Obstacles")]
        public Vector2Int ObstacleCount = new Vector2Int(2, 4);
        [Tooltip("장애물 가장자리와 경계 사이의 거리(유닛). NavMesh 에이전트 지름보다 넉넉히 커야 그 사이로 지나갈 수 있다.")]
        [Min(1f)] public float ObstacleMargin = 4f;
        [Min(2f)] public float ObstacleSpacing = 9f;
        public Vector2 ObstacleRadius = new Vector2(1.2f, 2f);
        [Min(0.1f)] public float ObstacleHeight = 1.5f;
        [Tooltip("장애물로 보여 줄 프리팹. 비워 두면 원기둥을 쓴다.")]
        public GameObject[] ObstaclePrefabs;
        [Tooltip("ObstaclePrefabs의 대략적인 바닥 지름(유닛). 장애물 반지름에 맞춰 크기를 정하는 데 쓴다.")]
        [Min(0.1f)] public float ObstaclePrefabDiameter = 2f;

        [Header("Gates")]
        [Tooltip("길이 공터와 만나는 곳마다 잠금 오브젝트를 둔다. 처음에는 열려(꺼져) 있고, 방 단위로 잠근다.")]
        public bool BuildGates = true;
        [Tooltip("잠금 충돌체의 높이(유닛). 이동체가 넘지 못할 만큼 높아야 한다.")]
        [Min(0.5f)] public float GateHeight = 3f;
        [Tooltip("잠금 표시의 높이와 두께(유닛). 충돌체와 별개로 보이기만 한다.")]
        [Min(0.1f)] public float GateVisualHeight = 1.2f;
        [Min(0.1f)] public float GateVisualThickness = 0.6f;
        public Color GateColor = new Color(0.75f, 0.25f, 0.2f);

        [Header("NavMesh")]
        public bool BuildNavMesh = true;

        [Header("Look")]
        [Tooltip("비워 두면 현재 렌더 파이프라인의 기본 머티리얼을 쓴다.")]
        public Material BaseMaterial;
        public Color ObstacleColor = new Color(0.5f, 0.5f, 0.52f);
        public Color StartColor = new Color(0.2f, 0.75f, 0.3f);
        public Color ItemColor = new Color(0.95f, 0.8f, 0.2f);
        public Color KeyColor = new Color(0.2f, 0.85f, 0.9f);
        public Color BossGateColor = new Color(0.6f, 0.25f, 0.85f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private readonly Dictionary<Color, Material> _materialsByColor = new Dictionary<Color, Material>();
        private readonly List<Object> _createdAssets = new List<Object>();
        private readonly List<Collider> _obstacleColliders = new List<Collider>();
        private Transform _generatedRoot;
        private NavMeshSurface _navMeshSurface;
        // 방 번호마다 그 방의 입구에 놓인 잠금 오브젝트와 잠금 상태.
        private List<GameObject>[] _gatesByRoom;
        private bool[] _roomLocked;

        public DungeonLayout Layout { get; private set; }
        public WalkableMap Map { get; private set; }
        public bool HasGeneratedMap => _generatedRoot != null;
        public string LastReport { get; private set; }
        /// <summary>마지막 생성에 걸린 시간(ms). NavMesh 빌드를 포함한다.</summary>
        public double LastGenerateMilliseconds { get; private set; }
        public int GeneratedObjectCount { get; private set; }

        /// <summary>이 빌더의 NavMeshSurface가 쓰는 에이전트 반지름.</summary>
        public float AgentRadius
        {
            get
            {
                if (_navMeshSurface == null)
                {
                    _navMeshSurface = GetComponent<NavMeshSurface>();
                }

                return NavMesh.GetSettingsByID(_navMeshSurface.agentTypeID).agentRadius;
            }
        }

        public override Vector3 StartPosition => Map != null && Map.StartRoom != null
            ? ToWorld(Map.StartRoom.Center)
            : transform.position;

        private void OnValidate()
        {
            GridSize = Vector2Int.Max(GridSize, Vector2Int.one);
            RoomSize = Vector2.Max(RoomSize, Vector2.one * 8f);
            ObstacleCount = Vector2Int.Max(ObstacleCount, Vector2Int.zero);
            ObstacleRadius = Vector2.Max(ObstacleRadius, Vector2.one * 0.3f);
        }

        private void Awake()
        {
            // ExecuteAlways 때문에 편집 모드에서도 불린다. 자동 생성은 Play에서만 한다.
            if (!Application.isPlaying || !GenerateOnAwake)
            {
                return;
            }

            if (RandomizeSeedOnAwake)
            {
                Seed = System.Environment.TickCount;
            }

            Generate();
        }

        private void OnDisable()
        {
            // 생성물은 저장 대상이 아니므로 Play 진입·종료, 씬 닫기, 스크립트 재컴파일 때 스스로 치운다.
            Clear();
        }

        public override void GenerateWithRandomSeed()
        {
            Seed = System.Environment.TickCount;
            Generate();
        }

        public void Generate()
        {
            Clear();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            Layout = DungeonLayout.Generate(new DungeonLayoutSettings
            {
                GridSize = GridSize,
                RoomCount = RoomCount,
                NewestPickChance = NewestPickChance,
                MinBossPathLength = MinBossPathLength,
                MinItemRooms = MinItemRooms,
                MaxAttempts = MaxLayoutAttempts
            }, new System.Random(Seed));
            Layout.PickKeyRoom(new System.Random(Seed ^ 0x4B4559));

            Map = WalkableMap.Build(Layout, new WalkableMapSettings
            {
                Shape = Shape,
                RoomSize = RoomSize,
                BoundaryWidth = BoundaryWidth,
                ClearingScale = ClearingScale,
                CenterJitter = CenterJitter,
                OutlineWobble = OutlineWobble,
                PathWidth = PathWidth,
                PathBend = PathBend,
                OuterMargin = OuterMargin,
                ObstacleCount = ObstacleCount,
                ObstacleMargin = ObstacleMargin,
                ObstacleSpacing = ObstacleSpacing,
                ObstacleRadius = ObstacleRadius
            }, new System.Random(Seed ^ MapSeedSalt));

            _generatedRoot = new GameObject(GeneratedRootName).transform;
            _generatedRoot.SetParent(transform, false);

            BuildBoundary(_generatedRoot, new System.Random(Seed ^ 0x454E56));
            BuildObstacles(_generatedRoot, new System.Random(Seed ^ 0x4F4253));
            BuildMarkers(_generatedRoot);
            // 잠금 오브젝트는 꺼진 채로 만들어 NavMesh에는 열린 상태가 구워진다.
            BuildGateObjects(_generatedRoot);

            string navMeshReport = BuildNavMesh ? BakeAndCheckNavMesh() : "skipped";

            GeneratedObjectCount = CountAndFlag(_generatedRoot);
            stopwatch.Stop();
            LastGenerateMilliseconds = stopwatch.Elapsed.TotalMilliseconds;

            LastReport = $"seed {Seed}: rooms {Layout.Rooms.Count}, shape {Shape}, map {Map.Width}x{Map.Height}, " +
                         $"walkable {Map.WalkableRatio * 100f:0.0}%, obstacles {Map.Obstacles.Count}, " +
                         $"entrances {Map.Entrances.Count}, " +
                         $"connected {Map.IsFullyConnected(true, AgentRadius)}, navmesh {navMeshReport}, " +
                         $"objects {GeneratedObjectCount}, {LastGenerateMilliseconds:0} ms";
            Debug.Log($"[LayoutTest] {name}: {LastReport}", this);
        }

        public void Clear()
        {
            Layout = null;
            Map = null;
            LastReport = null;
            _obstacleColliders.Clear();
            _gatesByRoom = null;
            _roomLocked = null;

            if (_navMeshSurface != null)
            {
                ReleaseNavMeshData(_navMeshSurface);
            }

            OnClear();

            if (_generatedRoot != null)
            {
                DestroyImmediate(_generatedRoot.gameObject);
                _generatedRoot = null;
            }

            foreach (Object asset in _createdAssets)
            {
                if (asset != null)
                {
                    DestroyImmediate(asset);
                }
            }

            _createdAssets.Clear();
            _materialsByColor.Clear();
        }

        public override Bounds GetWorldBounds()
        {
            if (Map == null)
            {
                return new Bounds(transform.position, Vector3.zero);
            }

            Vector2 center = Map.Origin + new Vector2(Map.Width, Map.Height) * 0.5f;
            return new Bounds(ToWorld(center), new Vector3(Map.Width, 4f, Map.Height));
        }

        // ---------- 방 잠금 ----------

        /// <summary>월드 위치가 들어 있는 방의 번호. 길이나 경계 위면 -1.</summary>
        public int RoomIndexAt(Vector3 worldPosition)
        {
            if (Map == null)
            {
                return -1;
            }

            Vector3 local = transform.InverseTransformPoint(worldPosition);
            return Map.TryGetCell(new Vector2(local.x, local.z), out int x, out int y) ? Mathf.Max(Map.Owner[x, y], -1) : -1;
        }

        public bool IsRoomLocked(int roomIndex)
        {
            return _roomLocked != null && roomIndex >= 0 && roomIndex < _roomLocked.Length && _roomLocked[roomIndex];
        }

        /// <summary>그 방의 모든 입구를 막거나 연다. 잠기면 방 안에서 길로 나갈 수 없다.</summary>
        public void SetRoomLocked(int roomIndex, bool locked)
        {
            if (_gatesByRoom == null || roomIndex < 0 || roomIndex >= _gatesByRoom.Length)
            {
                return;
            }

            _roomLocked[roomIndex] = locked;
            foreach (GameObject gate in _gatesByRoom[roomIndex])
            {
                if (gate != null)
                {
                    gate.SetActive(locked);
                }
            }
        }

        /// <summary>방에 놓인 잠금 오브젝트. 검사용이다.</summary>
        public IReadOnlyList<GameObject> GetRoomGates(int roomIndex)
        {
            return _gatesByRoom != null && roomIndex >= 0 && roomIndex < _gatesByRoom.Length
                ? _gatesByRoom[roomIndex]
                : (IReadOnlyList<GameObject>)System.Array.Empty<GameObject>();
        }

        public override bool ToggleRoomLockAt(Vector3 worldPosition)
        {
            int roomIndex = RoomIndexAt(worldPosition);
            if (roomIndex < 0 || _gatesByRoom == null)
            {
                return false;
            }

            SetRoomLocked(roomIndex, !IsRoomLocked(roomIndex));
            return true;
        }

        /// <summary>
        /// 카메라가 내려다보는 방향으로 광선을 쏴, 걸을 수 있는 면적 중 가려지는 비율을 잰다.
        /// 경계와 장애물이 바닥을 얼마나 가리는지 비교하는 지표다.
        /// </summary>
        public float MeasureOccludedRatio(float cameraPitchDegrees, int step = 2)
        {
            if (Map == null)
            {
                return 0f;
            }

            float pitch = cameraPitchDegrees * Mathf.Deg2Rad;
            var towardCamera = new Vector3(0f, Mathf.Sin(pitch), -Mathf.Cos(pitch));
            int samples = 0;
            int occluded = 0;

            for (int x = 0; x < Map.Width; x += step)
            {
                for (int y = 0; y < Map.Height; y += step)
                {
                    if (!Map.IsWalkable(x, y))
                    {
                        continue;
                    }

                    samples++;
                    Vector3 point = ToWorld(Map.CellCenter(x, y)) + Vector3.up * 0.05f;
                    if (IsOccluded(point, towardCamera, 60f))
                    {
                        occluded++;
                    }
                }
            }

            return samples > 0 ? (float)occluded / samples : 0f;
        }

        // ---------- 하위 클래스가 채우는 부분 ----------

        /// <summary>경계와 바닥을 만든다. 걸을 수 있는 곳은 높이 0의 평지여야 한다.</summary>
        protected abstract void BuildBoundary(Transform root, System.Random rng);

        /// <summary>바닥의 한 점이 경계에 가려 카메라에서 보이지 않는지.</summary>
        protected abstract bool IsOccludedByBoundary(Vector3 point, Vector3 towardCamera, float maxDistance);

        /// <summary>하위 클래스가 만든 것을 치운다. Generated 자식은 기반 클래스가 지운다.</summary>
        protected virtual void OnClear()
        {
        }

        // ---------- 공통 조립 ----------

        private void BuildObstacles(Transform root, System.Random rng)
        {
            Transform obstacles = new GameObject("Obstacles").transform;
            obstacles.SetParent(root, false);

            foreach (WalkableObstacle obstacle in Map.Obstacles)
            {
                var obstacleObject = new GameObject("Obstacle");
                obstacleObject.transform.SetParent(obstacles, false);
                obstacleObject.transform.localPosition = ToLocal(obstacle.Position);

                // 보이는 모양과 상관없이 충돌은 단순한 기둥으로 둔다.
                var capsule = obstacleObject.AddComponent<CapsuleCollider>();
                capsule.radius = obstacle.Radius;
                capsule.height = Mathf.Max(ObstacleHeight, obstacle.Radius * 2f);
                capsule.center = new Vector3(0f, capsule.height * 0.5f, 0f);
                _obstacleColliders.Add(capsule);

                float yaw = (float)rng.NextDouble() * 360f;
                if (ObstaclePrefabs != null && ObstaclePrefabs.Length > 0)
                {
                    GameObject prefab = ObstaclePrefabs[rng.Next(ObstaclePrefabs.Length)];
                    if (prefab != null)
                    {
                        GameObject visual = Instantiate(prefab, obstacleObject.transform);
                        visual.name = prefab.name;
                        visual.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                        visual.transform.localScale = prefab.transform.localScale
                                                      * (obstacle.Radius * 2f / ObstaclePrefabDiameter);
                        // 프리팹에 달린 충돌체(MeshCollider 등)는 뗀다. 충돌은 위의 기둥이 맡고,
                        // 남겨 두면 NavMesh가 읽기 권한 없는 메시를 소스로 모으려 한다.
                        foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
                        {
                            DestroyImmediate(collider);
                        }

                        continue;
                    }
                }

                CreatePrimitive(PrimitiveType.Cylinder, "Visual", obstacleObject.transform,
                    new Vector3(0f, ObstacleHeight * 0.5f, 0f),
                    new Vector3(obstacle.Radius * 2f, ObstacleHeight * 0.5f, obstacle.Radius * 2f),
                    ObstacleColor, false);
            }
        }

        private void BuildMarkers(Transform root)
        {
            Transform markers = new GameObject("Markers").transform;
            markers.SetParent(root, false);

            foreach (WalkableRoom room in Map.Rooms)
            {
                string markerName;
                Color color;
                switch (room.Data.Role)
                {
                    case DungeonRoomRole.Start:
                        markerName = "PlayerStart";
                        color = StartColor;
                        break;
                    case DungeonRoomRole.BossGate:
                        markerName = "BossPortal";
                        color = BossGateColor;
                        break;
                    case DungeonRoomRole.Item:
                        markerName = room.Data.HasKey ? "Key" : "ItemSpawn";
                        color = room.Data.HasKey ? KeyColor : ItemColor;
                        break;
                    default:
                        continue;
                }

                Vector3 position = ToLocal(room.Center);
                position.y = 0.05f;
                float diameter = room.Data.Role == DungeonRoomRole.BossGate ? 3f : 1.6f;
                CreatePrimitive(PrimitiveType.Cylinder, markerName, markers, position,
                    new Vector3(diameter, 0.02f, diameter), color, false);
            }
        }

        /// <summary>
        /// 입구마다 잠금 오브젝트를 만든다. 충돌 상자는 입구 칸을 모두 덮어 방을 완전히 막고,
        /// NavMeshObstacle은 잠긴 동안 NavMesh에서도 길을 끊는다. 보이는 것은 입구 가운데의 낮은 벽이다.
        /// </summary>
        private void BuildGateObjects(Transform root)
        {
            _gatesByRoom = new List<GameObject>[Map.Rooms.Count];
            _roomLocked = new bool[Map.Rooms.Count];
            for (int i = 0; i < _gatesByRoom.Length; i++)
            {
                _gatesByRoom[i] = new List<GameObject>();
            }

            if (!BuildGates)
            {
                return;
            }

            Transform gates = new GameObject("Gates").transform;
            gates.SetParent(root, false);

            foreach (WalkableEntrance entrance in Map.Entrances)
            {
                var gate = new GameObject($"Gate_Room{entrance.RoomIndex}_{_gatesByRoom[entrance.RoomIndex].Count}");
                gate.transform.SetParent(gates, false);
                gate.transform.localPosition = ToLocal(entrance.Center);
                // 로컬 z가 길 방향(Length), x가 길을 가로지르는 방향(Width)이다.
                gate.transform.localRotation = Quaternion.LookRotation(new Vector3(entrance.Direction.x, 0f, entrance.Direction.y));

                var size = new Vector3(entrance.Width, GateHeight, entrance.Length);
                var box = gate.AddComponent<BoxCollider>();
                box.size = size;
                box.center = new Vector3(0f, GateHeight * 0.5f, 0f);

                var obstacle = gate.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.size = size;
                obstacle.center = box.center;
                obstacle.carving = true;

                CreatePrimitive(PrimitiveType.Cube, "Visual", gate.transform,
                    new Vector3(0f, GateVisualHeight * 0.5f, 0f),
                    new Vector3(entrance.Width, GateVisualHeight, GateVisualThickness), GateColor, false);

                gate.SetActive(false);
                _gatesByRoom[entrance.RoomIndex].Add(gate);
            }
        }

        private string BakeAndCheckNavMesh()
        {
            if (_navMeshSurface == null)
            {
                _navMeshSurface = GetComponent<NavMeshSurface>();
            }

            _navMeshSurface.collectObjects = CollectObjects.Children;
            _navMeshSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            Physics.SyncTransforms();
            _navMeshSurface.BuildNavMesh();
            string report = CheckNavMesh();

            if (!Application.isPlaying)
            {
                ReleaseNavMeshData(_navMeshSurface);
                report += " (edit mode: checked, not kept)";
            }

            return report;
        }

        /// <summary>시작 방에서 다른 모든 방의 중심까지 NavMesh로 갈 수 있는지 센다.</summary>
        private string CheckNavMesh()
        {
            if (!NavMesh.SamplePosition(StartPosition, out NavMeshHit startHit, 2f, NavMesh.AllAreas))
            {
                return "start not on navmesh";
            }

            int reachable = 0;
            var path = new NavMeshPath();
            foreach (WalkableRoom room in Map.Rooms)
            {
                if (NavMesh.SamplePosition(ToWorld(room.Center), out NavMeshHit hit, 2f, NavMesh.AllAreas)
                    && NavMesh.CalculatePath(startHit.position, hit.position, NavMesh.AllAreas, path)
                    && path.status == NavMeshPathStatus.PathComplete)
                {
                    reachable++;
                }
            }

            return $"rooms reachable {reachable}/{Map.Rooms.Count}";
        }

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

        private bool IsOccluded(Vector3 point, Vector3 towardCamera, float maxDistance)
        {
            var ray = new Ray(point, towardCamera);
            foreach (Collider obstacle in _obstacleColliders)
            {
                if (obstacle.Raycast(ray, out _, maxDistance))
                {
                    return true;
                }
            }

            return IsOccludedByBoundary(point, towardCamera, maxDistance);
        }

        /// <summary>
        /// 생성물 수를 세고, 편집 모드에서는 저장 대상에서 뺀다.
        /// Play 중의 오브젝트는 원래 저장되지 않으므로 표시하지 않는다.
        /// </summary>
        private static int CountAndFlag(Transform node)
        {
            if (!Application.isPlaying)
            {
                node.gameObject.hideFlags = HideFlags.DontSave;
            }

            int count = 1;
            for (int i = 0; i < node.childCount; i++)
            {
                count += CountAndFlag(node.GetChild(i));
            }

            return count;
        }

        // ---------- 도우미 ----------

        protected Vector3 ToLocal(Vector2 mapPosition)
        {
            return new Vector3(mapPosition.x, 0f, mapPosition.y);
        }

        protected Vector3 ToWorld(Vector2 mapPosition)
        {
            return transform.TransformPoint(ToLocal(mapPosition));
        }

        /// <summary>맵 전체를 덮는 지형 데이터를 만든다. 해상도를 먼저 정해야 size가 의도한 값으로 유지된다.</summary>
        protected TerrainData CreateTerrainData(int heightmapResolution, float maxHeight)
        {
            int resolution = Mathf.ClosestPowerOfTwo(Mathf.Clamp(heightmapResolution - 1, 32, 1024)) + 1;
            var data = Track(new TerrainData { name = "LayoutTestTerrain" });
            data.heightmapResolution = resolution;
            data.size = new Vector3(Map.Width, Mathf.Max(maxHeight, 0.1f), Map.Height);
            return data;
        }

        /// <summary>지형 오브젝트를 만들어 맵의 남서쪽 모서리에 놓는다. 지형 데이터는 메모리에만 있다.</summary>
        protected Terrain CreateTerrainObject(Transform root, TerrainData data)
        {
            GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.name = "Terrain";
            terrainObject.transform.SetParent(root, false);
            terrainObject.transform.localPosition = new Vector3(Map.Origin.x, 0f, Map.Origin.y);

            var terrain = terrainObject.GetComponent<Terrain>();
            if (GraphicsSettings.currentRenderPipeline != null
                && GraphicsSettings.currentRenderPipeline.defaultTerrainMaterial != null)
            {
                terrain.materialTemplate = GraphicsSettings.currentRenderPipeline.defaultTerrainMaterial;
            }

            return terrain;
        }

        /// <summary>한 가지 색으로 칠하는 지형 레이어.</summary>
        protected TerrainLayer CreateSolidLayer(string layerName, Color color)
        {
            var texture = Track(new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = $"LayoutTest_{layerName}" });
            texture.SetPixels(new[] { color, color, color, color });
            texture.Apply();

            return Track(new TerrainLayer
            {
                name = $"LayoutTest_{layerName}",
                diffuseTexture = texture,
                tileSize = new Vector2(4f, 4f),
                metallic = 0f,
                smoothness = 0f
            });
        }

        /// <summary>Clear에서 함께 지울 런타임 생성 에셋(머티리얼, 메시, 지형 데이터 등)을 등록한다.</summary>
        protected T Track<T>(T asset) where T : Object
        {
            _createdAssets.Add(asset);
            return asset;
        }

        protected GameObject CreatePrimitive(PrimitiveType type, string objectName, Transform parent,
            Vector3 localPosition, Vector3 size, Color color, bool keepCollider)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = objectName;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localScale = size;

            if (!keepCollider)
            {
                DestroyImmediate(primitive.GetComponent<Collider>());
            }

            var meshRenderer = primitive.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = GetMaterial(color, meshRenderer.sharedMaterial);
            return primitive;
        }

        protected Material GetMaterial(Color color, Material fallback)
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
                source = fallback;
            }

            if (source == null)
            {
                Debug.LogError("[LayoutTest] 머티리얼을 만들 바탕이 없습니다. BaseMaterial을 지정하세요.", this);
                return null;
            }

            var material = Track(new Material(source) { name = $"LayoutTest_{ColorUtility.ToHtmlStringRGB(color)}" });
            if (material.HasProperty(BaseColorId))
            {
                material.SetColor(BaseColorId, color);
            }

            if (material.HasProperty(ColorId))
            {
                material.SetColor(ColorId, color);
            }

            _materialsByColor[color] = material;
            return material;
        }
    }
}
