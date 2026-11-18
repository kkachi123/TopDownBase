using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace TopDownBase.Dungeon
{
    /// <summary>
    /// 보스 전 던전 구간을 "불규칙 공터 + 굽은 길 + 자연물 경계"로 생성한다.
    /// 방 그래프(DungeonLayout) → 걸을 수 있는 영역 지도(WalkableMap) → 지면·충돌·소품 → 장애물
    /// → 방(DungeonRoom)·스폰 위치 → 보스 포털과 열쇠 → 입구 잠금 → NavMesh 순서로 만든다.
    /// 생성물은 "Generated" 자식 아래에만 놓이며 씬에 저장되지 않는다. Play 시작과 인스펙터 버튼으로 다시 만든다.
    /// 지면은 DungeonGenerator.Ground.cs, 소품은 DungeonGenerator.Props.cs에 있다.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshSurface))]
    public partial class DungeonGenerator : DungeonMapSource
    {
        private const string GeneratedRootName = "Generated";
        // 방 배치와 섞이지 않도록 시드에서 따로 파생하는 난수의 구분값.
        private const int KeySeedSalt = 0x4B4559;
        private const int MapSeedSalt = 0x4D4150;
        private const int EnvironmentSeedSalt = 0x454E56;
        private const int ObstacleSeedSalt = 0x4F4253;
        private const int SpawnSeedSalt = 0x53504E;
        private const float MarkerDiameter = 1.4f;

        [Header("Seed")]
        public int Seed = 12345;
        [Tooltip("Play 시작 시 새 시드를 뽑는다. GenerateOnAwake가 켜져 있을 때만 쓰인다.")]
        public bool RandomizeSeedOnAwake = true;
        [Tooltip("켜면 Play 시작 시 생성한다. 끄면 스크립트에서 Generate를 부를 때만 생성한다.")]
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

        [Header("Shape")]
        public WalkableShape Shape = WalkableShape.Clearing;
        [Tooltip("방 하나가 쓸 수 있는 최대 크기(유닛). 공터는 이 안에서 만들어진다.")]
        public Vector2 RoomSize = new Vector2(24f, 20f);
        [Tooltip("이웃한 방 사이에 남기는 경계의 폭(유닛). 방 간격은 RoomSize + BoundaryWidth다. 맵 크기와 방 사이 숲의 두께를 정한다.")]
        [Min(1f)] public float BoundaryWidth = 5f;
        [Tooltip("공터 크기 배율의 (최소, 최대). Clearing 모양에서만 쓴다.")]
        public Vector2 ClearingScale = new Vector2(0.8f, 1f);
        [Tooltip("공터 중심을 방 안에서 흔드는 정도(0~1).")]
        [Range(0f, 1f)] public float CenterJitter = 1f;
        [Tooltip("공터 윤곽의 울퉁불퉁한 정도. 0.3을 넘기면 좁게 튀어나온 부분이 생긴다.")]
        [Range(0f, 0.4f)] public float OutlineWobble = 0.2f;
        [Tooltip("길 폭(유닛). NavMesh 에이전트 반지름이 1이라 4보다 좁히지 않는다.")]
        [Min(2f)] public float PathWidth = 5f;
        [Tooltip("길 가운데를 옆으로 미는 최대 거리(유닛). BoundaryWidth의 1/3 정도까지가 적당하다.")]
        [Min(0f)] public float PathBend = 2f;
        [Tooltip("맵 바깥쪽 숲의 폭(유닛).")]
        [Min(2f)] public float OuterMargin = 10f;

        [Header("Obstacles (combat rooms)")]
        public Vector2Int ObstacleCount = new Vector2Int(2, 4);
        [Tooltip("장애물 가장자리와 경계 사이의 거리(유닛). NavMesh 에이전트 지름보다 넉넉히 커야 그 사이로 지나갈 수 있다.")]
        [Min(1f)] public float ObstacleMargin = 4f;
        [Tooltip("장애물 중심 사이의 최소 거리(유닛).")]
        [Min(2f)] public float ObstacleSpacing = 9f;
        public Vector2 ObstacleRadius = new Vector2(1.2f, 2f);
        [Min(0.1f)] public float ObstacleHeight = 1.5f;
        [Tooltip("장애물로 보여 줄 프리팹. 비워 두면 원기둥을 쓴다. 충돌은 프리팹과 상관없이 기둥 모양이다.")]
        public GameObject[] ObstaclePrefabs;
        [Tooltip("ObstaclePrefabs의 대략적인 바닥 지름(유닛). 장애물 반지름에 맞춰 크기를 정하는 데 쓴다.")]
        [Min(0.1f)] public float ObstaclePrefabDiameter = 1.5f;

        [Header("Enemy spawns (combat rooms)")]
        public Vector2Int EnemySpawnCount = new Vector2Int(2, 4);
        [Tooltip("경계에서 띄우는 거리(유닛).")]
        [Min(0f)] public float SpawnEdgeMargin = 2f;
        [Tooltip("입구에서 띄우는 거리(유닛). 방에 들어서자마자 적과 겹치지 않게 한다.")]
        [Min(0f)] public float SpawnEntranceClearance = 5f;
        [Tooltip("장애물 가장자리에서 띄우는 거리(유닛).")]
        [Min(0f)] public float SpawnObstacleClearance = 1f;
        [Tooltip("스폰 위치 사이의 최소 거리(유닛).")]
        [Min(0f)] public float SpawnSpacing = 4f;

        [Header("Boss portal")]
        [Tooltip("포털에 들어가면 불러올 보스 씬 이름. Build Settings에 등록된 씬이어야 한다. 비워 두면 포털은 표식으로만 남는다.")]
        public string BossSceneName;
        [Tooltip("아이템 방 중 한 곳에 열쇠를 두고, 얻기 전에는 포털을 잠근다.")]
        public bool LockPortalWithKey = true;

        [Header("Gates")]
        [Tooltip("길이 공터와 만나는 곳마다 잠금 오브젝트를 둔다. 처음에는 열려(꺼져) 있고, 방 단위로 잠근다(DungeonRoom.SetDoorsLocked).")]
        public bool BuildGates = true;
        [Tooltip("잠금 충돌체의 높이(유닛). 이동체가 넘지 못할 만큼 높아야 한다.")]
        [Min(0.5f)] public float GateHeight = 3f;
        [Tooltip("잠금 표시의 높이와 두께(유닛). 충돌체와 별개로 보이기만 한다.")]
        [Min(0.1f)] public float GateVisualHeight = 1.2f;
        [Min(0.1f)] public float GateVisualThickness = 0.6f;
        public Color GateColor = new Color(0.75f, 0.25f, 0.2f);

        [Header("Ground")]
        [Tooltip("켜면 Terrain으로 지면을 만든다. 공터 가장자리가 부드럽게 섞이고 경계 바깥에 낮은 언덕이 생긴다. 끄면 평평한 판과 칸 단위 표면을 쓴다.")]
        public bool UseTerrainGround = true;
        [Tooltip("경계 바깥 언덕의 최대 높이(유닛). 캐릭터 키(2)보다 낮아야 남쪽 가장자리에서 캐릭터가 가려지지 않는다.")]
        [Range(0f, 1.8f)] public float HillHeight = 1f;
        [Tooltip("걸을 수 있는 영역의 끝에서 이 거리(유닛)까지는 평지로 둔다.")]
        [Min(0.5f)] public float HillStart = 2.5f;
        [Min(0.5f)] public float HillWidth = 4f;
        [Min(1f)] public float HillNoiseScale = 12f;
        [Tooltip("흙과 풀이 섞이는 폭(유닛).")]
        [Min(0.1f)] public float EdgeBlend = 1.2f;
        public int HeightmapResolution = 257;
        public int AlphamapResolution = 512;
        public Color GroundColor = new Color(0.33f, 0.45f, 0.28f);
        public Color ClearingColor = new Color(0.52f, 0.47f, 0.38f);

        [Header("Props")]
        public DungeonPropRendering PropRendering = DungeonPropRendering.Instanced;
        public bool PropsCastShadows = true;
        [Tooltip("끄면 가장자리의 낮은 소품은 그림자를 드리우지 않는다. 그림자 단계마다 한 번씩 더 그리는 비용이 줄어든다.")]
        public bool LowPropsCastShadows;
        [Tooltip("화면 밖이라도 이 거리(유닛) 안의 소품은 그린다. 화면 밖 나무의 그림자가 화면 안으로 드리워지기 때문이다.")]
        [Min(0f)] public float ShadowCullPadding = 8f;
        [Tooltip("경계 가장자리에 놓는 낮은 소품(바위, 덤불). 루트에 메시 하나가 있는 프리팹이어야 묶어 그릴 수 있다.")]
        public GameObject[] LowProps;
        [Tooltip("가장자리 바깥에 놓는 키 큰 소품(나무).")]
        public GameObject[] TallProps;
        [Tooltip("걸을 수 있는 영역에서 이 거리(유닛) 안쪽의 막힌 칸을 가장자리로 본다.")]
        [Min(1f)] public float EdgeDepth = 2f;
        [Range(0f, 1f)] public float EdgeDensity = 0.55f;
        [Tooltip("걸을 수 있는 곳과 바로 맞닿은 첫 줄(대각선 포함)의 소품 밀도. 높이면 공터와 길의 테두리가 끊기지 않는다.")]
        [Range(0f, 1f)] public float RimDensity = 0.85f;
        [Range(0f, 1f)] public float FillDensity = 0.07f;
        [Tooltip("가장자리 소품의 크기 배율. 크게 하면 소품이 걸을 수 있는 영역 위로 넘어와 바닥을 가린다.")]
        public Vector2 LowPropScale = new Vector2(0.7f, 1.2f);
        public Vector2 TallPropScale = new Vector2(1.2f, 2.2f);
        [Tooltip("북쪽으로 이 거리(유닛) 안에 걸을 수 있는 곳이 있으면 키 큰 소품을 놓지 않는다. 카메라가 남쪽에서 내려다보기 때문이다. 소품이 높으면 그 높이가 가리는 거리까지 자동으로 늘어난다.")]
        [Min(0f)] public float CameraClearance = 8f;
        [Tooltip("게임 카메라가 내려다보는 각도. 키 큰 소품이 바닥을 가리는 거리를 계산하는 데 쓴다.")]
        [Range(20f, 90f)] public float CameraPitch = 40f;

        [Header("Collision")]
        [Tooltip("걸을 수 없는 칸을 덮는 보이지 않는 상자의 높이(유닛).")]
        [Min(1f)] public float BlockerHeight = 3f;

        [Header("NavMesh")]
        public bool BuildNavMesh = true;

        [Header("Look")]
        [Tooltip("비워 두면 현재 렌더 파이프라인의 기본 머티리얼을 쓴다.")]
        public Material BaseMaterial;
        public Color ObstacleColor = new Color(0.55f, 0.4f, 0.25f);
        public Color StartColor = new Color(0.2f, 0.75f, 0.3f);
        public Color EnemyColor = new Color(0.85f, 0.2f, 0.2f);
        public Color ItemColor = new Color(0.95f, 0.8f, 0.2f);
        public Color BossGateColor = new Color(0.6f, 0.25f, 0.85f);
        public Color PortalLockedColor = new Color(0.3f, 0.28f, 0.33f);
        public Color KeyColor = new Color(0.2f, 0.85f, 0.9f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private readonly Dictionary<Color, Material> _materialsByColor = new Dictionary<Color, Material>();
        // 생성 중에 만든 머티리얼·메시·지형 데이터. 저장되지 않으며 Clear에서 지운다.
        private readonly List<Object> _createdAssets = new List<Object>();
        private readonly List<Collider> _obstacleColliders = new List<Collider>();
        private readonly List<DungeonRoom> _rooms = new List<DungeonRoom>();
        private Transform _generatedRoot;
        private NavMeshSurface _navMeshSurface;
        private DungeonRoom _startRoom;
        private DungeonRoom _bossGateRoom;
        private DungeonPortal _bossPortal;
        private DungeonKeyPickup _keyPickup;

        public DungeonLayout Layout { get; private set; }
        /// <summary>걸을 수 있는 영역 지도. 방 모양, 길, 입구, 장애물, 스폰 위치를 담는다.</summary>
        public WalkableMap Map { get; private set; }
        /// <summary>방 목록. 순서는 Layout.Rooms, Map.Rooms와 같다.</summary>
        public IReadOnlyList<DungeonRoom> Rooms => _rooms;
        public DungeonRoom StartRoom => _startRoom;
        public DungeonRoom BossGateRoom => _bossGateRoom;
        /// <summary>보스 입구 방의 포털.</summary>
        public DungeonPortal BossPortal => _bossPortal;
        /// <summary>포털을 여는 열쇠. 열쇠 방이 없는 배치나 잠금을 끈 경우에는 null이다.</summary>
        public DungeonKeyPickup KeyPickup => _keyPickup;
        public bool HasGeneratedMap => _generatedRoot != null;
        /// <summary>마지막 생성의 한 줄 요약. 인스펙터 표시용이다.</summary>
        public string LastReport { get; private set; }
        /// <summary>마지막 생성에 걸린 시간(ms). NavMesh 빌드를 포함한다.</summary>
        public double LastGenerateMilliseconds { get; private set; }
        public int GeneratedObjectCount { get; private set; }

        /// <summary>플레이어를 놓을 위치(시작 공터 중심).</summary>
        public override Vector3 StartPosition => StartRoom != null ? StartRoom.transform.position : transform.position;

        /// <summary>이 생성기의 NavMeshSurface가 쓰는 에이전트 반지름.</summary>
        public float AgentRadius => NavMesh.GetSettingsByID(GetNavMeshSurface().agentTypeID).agentRadius;

        private void OnValidate()
        {
            GridSize = Vector2Int.Max(GridSize, Vector2Int.one);
            RoomSize = Vector2.Max(RoomSize, Vector2.one * 8f);
            ObstacleCount = Vector2Int.Max(ObstacleCount, Vector2Int.zero);
            ObstacleRadius = Vector2.Max(ObstacleRadius, Vector2.one * 0.3f);
            EnemySpawnCount = Vector2Int.Max(EnemySpawnCount, Vector2Int.zero);
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

            if (LockPortalWithKey)
            {
                Layout.PickKeyRoom(new System.Random(Seed ^ KeySeedSalt));
            }

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

            Map.PlaceEnemySpawns(new WalkableSpawnSettings
            {
                Count = EnemySpawnCount,
                EdgeMargin = SpawnEdgeMargin,
                EntranceClearance = SpawnEntranceClearance,
                ObstacleClearance = SpawnObstacleClearance,
                Spacing = SpawnSpacing
            }, new System.Random(Seed ^ SpawnSeedSalt));

            _generatedRoot = new GameObject(GeneratedRootName).transform;
            _generatedRoot.SetParent(transform, false);

            BuildGround(_generatedRoot, new System.Random(Seed ^ EnvironmentSeedSalt));
            BuildObstacles(_generatedRoot, new System.Random(Seed ^ ObstacleSeedSalt));
            BuildRooms(_generatedRoot);
            BuildBossPortalAndKey();
            // 잠금 오브젝트는 꺼진 채로 만들어 NavMesh에는 열린 상태가 구워진다.
            BuildGateObjects(_generatedRoot);

            string navMeshReport = BuildNavMesh ? BakeAndCheckNavMesh() : "skipped";

            GeneratedObjectCount = CountAndFlag(_generatedRoot);
            stopwatch.Stop();
            LastGenerateMilliseconds = stopwatch.Elapsed.TotalMilliseconds;

            string portalReport = _bossPortal.HasDestination
                ? (_bossPortal.IsLocked ? "locked by key" : "open")
                : "no destination";
            LastReport = $"seed {Seed}: rooms {Layout.Rooms.Count}, boss path {Layout.BossPathLength}, " +
                         $"item rooms {Layout.ItemRoomCount}, entrances {Map.Entrances.Count}, " +
                         $"enemy spawns {Map.EnemySpawns.Count}, portal {portalReport}, navmesh {navMeshReport}, " +
                         $"objects {GeneratedObjectCount}, {LastGenerateMilliseconds:0} ms";
            Debug.Log($"[Dungeon] {LastReport}", this);

            if (string.IsNullOrEmpty(BossSceneName))
            {
                Debug.LogWarning("[Dungeon] BossSceneName이 비어 있어 포털이 씬을 불러오지 않습니다.", this);
            }

            if (!Layout.MeetsRequirements)
            {
                Debug.LogWarning("[Dungeon] 조건을 만족하는 배치를 찾지 못해 가장 가까운 배치를 사용했습니다. " +
                                 "RoomCount, GridSize, MinBossPathLength, MinItemRooms 값을 확인하세요.", this);
            }
        }

        public void Clear()
        {
            Layout = null;
            Map = null;
            LastReport = null;
            _rooms.Clear();
            _startRoom = null;
            _bossGateRoom = null;
            _bossPortal = null;
            _keyPickup = null;
            _obstacleColliders.Clear();

            if (_navMeshSurface != null)
            {
                ReleaseNavMeshData(_navMeshSurface);
            }

            ClearProps();
            ClearGround();

            if (_generatedRoot != null)
            {
                // 같은 프레임에 다시 생성하므로 이전 생성물이 NavMesh 수집에 잡히지 않도록 즉시 지운다.
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

        /// <summary>맵 전체를 감싸는 월드 영역. 전체 보기 카메라에 쓴다.</summary>
        public override Bounds GetWorldBounds()
        {
            if (Map == null)
            {
                return new Bounds(transform.position, Vector3.zero);
            }

            Vector2 center = Map.Origin + new Vector2(Map.Width, Map.Height) * 0.5f;
            return new Bounds(ToWorld(center), new Vector3(Map.Width, 4f, Map.Height));
        }

        /// <summary>월드 위치가 들어 있는 방. 길이나 경계 위면 null.</summary>
        public DungeonRoom RoomAt(Vector3 worldPosition)
        {
            if (Map == null)
            {
                return null;
            }

            Vector3 local = transform.InverseTransformPoint(worldPosition);
            int index = Map.RoomIndexAt(new Vector2(local.x, local.z));
            return index >= 0 && index < _rooms.Count ? _rooms[index] : null;
        }

        public override bool ToggleRoomLockAt(Vector3 worldPosition)
        {
            DungeonRoom room = RoomAt(worldPosition);
            if (room == null)
            {
                return false;
            }

            room.SetDoorsLocked(!room.DoorsLocked);
            return true;
        }

        /// <summary>
        /// 카메라가 내려다보는 방향으로 광선을 쏴, 걸을 수 있는 면적 중 가려지는 비율을 잰다.
        /// 경계 소품과 장애물이 바닥을 얼마나 가리는지 확인하는 지표다.
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

        // ---------- 장애물 ----------

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

        // ---------- 방과 스폰 위치 ----------

        /// <summary>공터마다 DungeonRoom을 만들고 역할 표식과 적 스폰 위치를 단다. 방 오브젝트는 공터 중심에 놓인다.</summary>
        private void BuildRooms(Transform root)
        {
            Transform rooms = new GameObject("Rooms").transform;
            rooms.SetParent(root, false);

            foreach (WalkableRoom walkable in Map.Rooms)
            {
                DungeonLayoutRoom data = walkable.Data;
                var roomObject = new GameObject($"Room_{data.Cell.x}_{data.Cell.y}_{data.Role}");
                roomObject.transform.SetParent(rooms, false);
                roomObject.transform.localPosition = ToLocal(walkable.Center);

                var room = roomObject.AddComponent<DungeonRoom>();
                room.Initialize(data.Role, data.Cell, data.DistanceFromStart, walkable.Radius * 2f);
                if (data.HasKey)
                {
                    room.MarkAsKeyRoom();
                }

                switch (data.Role)
                {
                    case DungeonRoomRole.Start:
                        CreateMarker("PlayerStart", roomObject.transform, Vector3.zero, StartColor);
                        _startRoom = room;
                        break;
                    case DungeonRoomRole.BossGate:
                        _bossGateRoom = room;
                        break;
                    case DungeonRoomRole.Item:
                        // 열쇠 방에는 아이템 표식 대신 열쇠가 놓인다(BuildBossPortalAndKey).
                        if (!data.HasKey)
                        {
                            CreateMarker("ItemSpawn", roomObject.transform, Vector3.zero, ItemColor);
                        }

                        break;
                }

                _rooms.Add(room);
            }

            foreach (WalkableSpawn spawn in Map.EnemySpawns)
            {
                DungeonRoom room = _rooms[spawn.RoomIndex];
                Vector3 local = ToLocal(spawn.Position) - room.transform.localPosition;
                GameObject marker = CreateMarker("EnemySpawn", room.transform, local, EnemyColor);
                room.AddEnemySpawnPoint(marker.transform);
            }
        }

        // ---------- 보스 포털과 열쇠 ----------

        /// <summary>
        /// 보스 입구 공터에 포털을, 열쇠 방 공터에 열쇠를 만들고 서로 연결한다.
        /// 포털은 불러올 보스 씬의 이름만 알며 투기장의 크기나 구성에는 의존하지 않는다.
        /// 열쇠 방이 없는 배치에서는 포털을 열어 둔 채로 만들어 진행이 막히지 않게 한다.
        /// </summary>
        private void BuildBossPortalAndKey()
        {
            DungeonLayoutRoom keyRoom = LockPortalWithKey ? Layout.KeyRoom : null;

            const float portalSize = 3f;
            GameObject portalObject = CreateTriggerZone("BossPortal", _bossGateRoom.transform, Vector3.zero, portalSize);
            GameObject portalVisual = CreateMarker("Visual", portalObject.transform, Vector3.zero, BossGateColor);
            portalVisual.transform.localScale = new Vector3(portalSize, 0.02f, portalSize);

            _bossPortal = portalObject.AddComponent<DungeonPortal>();
            _bossPortal.Initialize(BossSceneName, keyRoom != null, portalVisual.GetComponent<Renderer>(),
                GetMaterial(PortalLockedColor, null), GetMaterial(BossGateColor, null));

            if (keyRoom == null)
            {
                return;
            }

            DungeonRoom room = _rooms[Layout.Rooms.IndexOf(keyRoom)];
            GameObject keyObject = CreateTriggerZone("Key", room.transform, Vector3.zero, 2f);
            GameObject keyVisual = CreatePrimitive(PrimitiveType.Cube, "Visual", keyObject.transform,
                new Vector3(0f, 1f, 0f), Vector3.one * 0.7f, KeyColor, false);
            keyVisual.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);

            _keyPickup = keyObject.AddComponent<DungeonKeyPickup>();
            _keyPickup.Initialize(_bossPortal);
        }

        /// <summary>대상이 들어오면 반응하는 보이지 않는 구역. 사람 키 높이의 트리거 상자다.</summary>
        private static GameObject CreateTriggerZone(string objectName, Transform parent, Vector3 localPosition, float size)
        {
            var zone = new GameObject(objectName);
            zone.transform.SetParent(parent, false);
            zone.transform.localPosition = localPosition;

            var trigger = zone.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(size, 2f, size);
            trigger.center = new Vector3(0f, 1f, 0f);
            return zone;
        }

        // ---------- 입구 잠금 ----------

        /// <summary>
        /// 입구마다 잠금 오브젝트를 만들어 그 방의 문 차단물로 등록한다. 충돌 상자는 입구 칸을 모두 덮어 방을 완전히 막고,
        /// NavMeshObstacle은 잠긴 동안 NavMesh에서도 길을 끊는다. 보이는 것은 입구 가운데의 낮은 벽이다.
        /// </summary>
        private void BuildGateObjects(Transform root)
        {
            if (!BuildGates)
            {
                return;
            }

            Transform gates = new GameObject("Gates").transform;
            gates.SetParent(root, false);
            var countByRoom = new int[_rooms.Count];

            foreach (WalkableEntrance entrance in Map.Entrances)
            {
                var gate = new GameObject($"Gate_Room{entrance.RoomIndex}_{countByRoom[entrance.RoomIndex]++}");
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
                _rooms[entrance.RoomIndex].AddDoorBlocker(gate);
            }
        }

        // ---------- NavMesh ----------

        private NavMeshSurface GetNavMeshSurface()
        {
            if (_navMeshSurface == null && !TryGetComponent(out _navMeshSurface))
            {
                _navMeshSurface = gameObject.AddComponent<NavMeshSurface>();
            }

            return _navMeshSurface;
        }

        private string BakeAndCheckNavMesh()
        {
            NavMeshSurface surface = GetNavMeshSurface();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            Physics.SyncTransforms();
            surface.BuildNavMesh();
            string report = CheckNavMesh();

            if (!Application.isPlaying)
            {
                // 편집 모드에서는 검사만 하고 데이터를 남기지 않는다. Play 시작 시 항상 다시 굽는다.
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

        /// <summary>시작 방에서 모든 방·열쇠·포털까지 길이 이어지는지, 스폰 위치가 NavMesh 위에 있는지 센다.</summary>
        private string CheckNavMesh()
        {
            if (StartRoom == null || BossGateRoom == null)
            {
                return "built, start/boss room missing";
            }

            if (!NavMesh.SamplePosition(StartRoom.transform.position, out NavMeshHit startHit, 2f, NavMesh.AllAreas))
            {
                Debug.LogWarning("[Dungeon] 시작 방이 NavMesh 위에 있지 않습니다.", this);
                return "start not on navmesh";
            }

            int reachable = 0;
            foreach (DungeonRoom room in _rooms)
            {
                if (IsReachable(startHit.position, room.transform.position))
                {
                    reachable++;
                }
            }

            bool bossReachable = IsReachable(startHit.position, BossGateRoom.transform.position);
            string keyReport = _keyPickup == null
                ? "none"
                : IsReachable(startHit.position, _keyPickup.transform.position) ? "reachable" : "unreachable";

            int offMeshSpawns = 0;
            int totalSpawns = 0;
            foreach (DungeonRoom room in _rooms)
            {
                foreach (Transform spawn in room.EnemySpawnPoints)
                {
                    totalSpawns++;
                    if (!NavMesh.SamplePosition(spawn.position, out _, 0.5f, NavMesh.AllAreas))
                    {
                        offMeshSpawns++;
                    }
                }
            }

            if (reachable < _rooms.Count || !bossReachable || keyReport == "unreachable" || offMeshSpawns > 0)
            {
                Debug.LogWarning("[Dungeon] NavMesh 검사에서 이어지지 않은 곳이 있습니다. " +
                                 "ObstacleMargin과 PathWidth가 에이전트 크기에 맞는지 확인하세요.", this);
            }

            return $"rooms reachable {reachable}/{_rooms.Count}, boss {(bossReachable ? "reachable" : "unreachable")}, " +
                   $"key {keyReport}, enemy spawns on mesh {totalSpawns - offMeshSpawns}/{totalSpawns}";
        }

        private static bool IsReachable(Vector3 from, Vector3 to)
        {
            var path = new NavMeshPath();
            return NavMesh.SamplePosition(to, out NavMeshHit hit, 2f, NavMesh.AllAreas)
                   && NavMesh.CalculatePath(from, hit.position, NavMesh.AllAreas, path)
                   && path.status == NavMeshPathStatus.PathComplete;
        }

        // ---------- 도우미 ----------

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

        private static Vector3 ToLocal(Vector2 mapPosition)
        {
            return new Vector3(mapPosition.x, 0f, mapPosition.y);
        }

        private Vector3 ToWorld(Vector2 mapPosition)
        {
            return transform.TransformPoint(ToLocal(mapPosition));
        }

        /// <summary>Clear에서 함께 지울 런타임 생성 에셋(머티리얼, 메시, 지형 데이터 등)을 등록한다.</summary>
        private T Track<T>(T asset) where T : Object
        {
            _createdAssets.Add(asset);
            return asset;
        }

        /// <summary>충돌 없는 납작한 원판. 스폰·아이템·시작 위치를 눈으로 확인하는 용도다.</summary>
        private GameObject CreateMarker(string objectName, Transform parent, Vector3 localPosition, Color color)
        {
            localPosition.y = 0.03f;
            return CreatePrimitive(PrimitiveType.Cylinder, objectName, parent, localPosition,
                new Vector3(MarkerDiameter, 0.02f, MarkerDiameter), color, false);
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

        /// <summary>같은 색은 머티리얼 하나를 함께 쓴다.</summary>
        private Material GetMaterial(Color color, Material fallback)
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
                // 렌더 파이프라인 기본 머티리얼이 없는 빌드에서 BaseMaterial도 비어 있는 경우.
                Debug.LogError("[Dungeon] 머티리얼을 만들 바탕이 없습니다. BaseMaterial을 지정하세요.", this);
                return null;
            }

            var material = Track(new Material(source) { name = $"Dungeon_{ColorUtility.ToHtmlStringRGB(color)}" });
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
