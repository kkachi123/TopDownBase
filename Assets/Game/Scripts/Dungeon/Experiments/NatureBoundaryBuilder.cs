using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.Rendering;

namespace TopDownBase.Dungeon.Experiments
{
    /// <summary>소품을 그리는 방식.</summary>
    public enum NaturePropRendering
    {
        /// <summary>소품마다 GameObject를 만든다. 오브젝트 수와 DrawCall이 소품 수만큼 늘어난다.</summary>
        GameObjects,
        /// <summary>같은 메시를 묶어 한 번에 그린다. 화면에 보이는 소품만 골라 그리며 GameObject를 만들지 않는다.</summary>
        Instanced
    }

    /// <summary>
    /// 경계를 바위·덤불·나무 같은 소품으로 표현하는 테스트.
    /// 걸을 수 있는 곳은 높이 0의 평지이고, 소품은 보이기만 한다.
    /// 이동을 막는 것은 걸을 수 없는 칸을 덮는 보이지 않는 상자다.
    /// 방의 남쪽 가장자리에는 낮은 소품만 놓아 카메라가 방 바닥을 볼 수 있게 한다.
    /// </summary>
    public class NatureBoundaryBuilder : LayoutTestBuilder
    {
        private const float OccluderBucketSize = 4f;

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
        public NaturePropRendering PropRendering = NaturePropRendering.Instanced;
        public bool PropsCastShadows = true;
        [Tooltip("끄면 가장자리의 낮은 소품은 그림자를 드리우지 않는다. 그림자 단계마다 한 번씩 더 그리는 비용이 줄어든다.")]
        public bool LowPropsCastShadows;
        [Tooltip("화면 밖이라도 이 거리(유닛) 안의 소품은 그린다. 화면 밖 나무의 그림자가 화면 안으로 드리워지기 때문이다.")]
        [Min(0f)] public float ShadowCullPadding = 8f;
        [Tooltip("경계 가장자리에 놓는 낮은 소품(바위, 덤불).")]
        public GameObject[] LowProps;
        [Tooltip("가장자리 바깥에 놓는 키 큰 소품(나무).")]
        public GameObject[] TallProps;
        [Tooltip("걸을 수 있는 영역에서 이 거리(유닛) 안쪽의 막힌 칸을 가장자리로 본다.")]
        [Min(1f)] public float EdgeDepth = 2f;
        [Range(0f, 1f)] public float EdgeDensity = 0.55f;
        [Range(0f, 1f)] public float FillDensity = 0.07f;
        [Tooltip("가장자리 소품의 크기 배율. 크게 하면 소품이 걸을 수 있는 영역 위로 넘어와 바닥을 가린다.")]
        public Vector2 LowPropScale = new Vector2(0.7f, 1.2f);
        public Vector2 TallPropScale = new Vector2(1.2f, 2.2f);
        [Tooltip("북쪽으로 이 거리(유닛) 안에 걸을 수 있는 곳이 있으면 키 큰 소품을 놓지 않는다. 카메라가 남쪽에서 내려다보기 때문이다. 소품이 높으면 그 높이가 가리는 거리까지 자동으로 늘어난다.")]
        [Min(0f)] public float CameraClearance = 8f;
        [Tooltip("게임 카메라가 내려다보는 각도. 키 큰 소품이 바닥을 가리는 거리를 계산하는 데 쓴다.")]
        [Range(20f, 90f)] public float CameraPitch = 40f;

        [Header("Collision")]
        [Min(1f)] public float BlockerHeight = 3f;

        private struct Occluder
        {
            public Vector3 Position;
            public float Radius;
            public float Height;
        }

        private struct PrefabInfo
        {
            public float Radius;
            public float Height;
            public bool HasColliders;
        }

        private struct PropPlacement
        {
            public GameObject Prefab;
            public Vector2 Position;
            public float Yaw;
            public float Scale;
            public bool IsTall;
        }

        // 프리팹마다 한 번만 읽은 정보. 소품을 놓을 때마다 계층을 훑지 않기 위함이다.
        private readonly Dictionary<GameObject, PrefabInfo> _prefabInfos = new Dictionary<GameObject, PrefabInfo>();
        private readonly Dictionary<Vector2Int, List<Occluder>> _occluders = new Dictionary<Vector2Int, List<Occluder>>();
        private readonly List<PropPlacement> _placements = new List<PropPlacement>();

        /// <summary>같은 프리팹을 쓰는 소품 전체. 매 프레임 화면에 보이는 것만 Visible에 옮겨 한 번에 그린다.</summary>
        private class InstanceGroup
        {
            public Mesh Mesh;
            public Material[] Materials;
            public Matrix4x4[] All;
            /// <summary>소품을 감싸는 구(중심 xyz, 반지름 w). 화면 안에 있는지 가리는 데 쓴다.</summary>
            public Vector4[] Spheres;
            public Matrix4x4[] Visible;
            public bool CastShadows;
        }

        private readonly List<InstanceGroup> _groups = new List<InstanceGroup>();
        private Bounds _propBounds;
        private readonly Dictionary<Material, Material> _instancedMaterials = new Dictionary<Material, Material>();
        private readonly Plane[] _frustumPlanes = new Plane[6];
        private bool _renderingHooked;

        private float[,] _signedDistance;
        private Vector2 _noiseOffset;
        private Terrain _terrain;
        private TerrainCollider _terrainCollider;

        /// <summary>놓인 소품 수. 측정 기록용이다.</summary>
        public int PropCount { get; private set; }
        public int BlockerCount { get; private set; }

        protected override void BuildBoundary(Transform root, System.Random rng)
        {
            _noiseOffset = new Vector2((float)rng.NextDouble() * 1000f, (float)rng.NextDouble() * 1000f);
            _signedDistance = BuildSmoothedSignedDistance();

            if (UseTerrainGround)
            {
                BuildTerrainGround(root);
            }
            else
            {
                BuildFlatGround(root);
                BuildWalkableSurface(root);
            }

            BuildBlockers(root);
            PlanProps(rng);

            if (PropRendering == NaturePropRendering.Instanced)
            {
                BuildInstanceGroups();
            }
            else
            {
                InstantiateProps(root);
            }
        }

        protected override bool IsOccludedByBoundary(Vector3 point, Vector3 towardCamera, float maxDistance)
        {
            if (_terrainCollider != null && _terrainCollider.Raycast(new Ray(point, towardCamera), out _, maxDistance))
            {
                return true;
            }

            // 광선은 남쪽 위로만 올라간다(x 성분 없음). 남쪽에 있는 소품의 기둥 모양 영역에
            // 그 높이보다 낮게 들어가면 가려진 것으로 본다.
            float horizontal = Mathf.Max(-towardCamera.z, 0.0001f);
            float risePerUnit = towardCamera.y / horizontal;
            Vector2Int bucket = BucketOf(point);

            for (int bx = bucket.x - 1; bx <= bucket.x + 1; bx++)
            {
                for (int bz = bucket.y - 4; bz <= bucket.y + 1; bz++)
                {
                    if (!_occluders.TryGetValue(new Vector2Int(bx, bz), out List<Occluder> list))
                    {
                        continue;
                    }

                    foreach (Occluder occluder in list)
                    {
                        if (Mathf.Abs(point.x - occluder.Position.x) > occluder.Radius)
                        {
                            continue;
                        }

                        float nearEdge = occluder.Position.z + occluder.Radius;
                        float farEdge = occluder.Position.z - occluder.Radius;
                        if (farEdge > point.z)
                        {
                            continue;
                        }

                        float distanceToEntry = Mathf.Max(0f, point.z - nearEdge);
                        if (distanceToEntry <= maxDistance
                            && distanceToEntry * risePerUnit <= occluder.Position.y + occluder.Height)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        protected override void OnClear()
        {
            if (_renderingHooked)
            {
                RenderPipelineManager.beginCameraRendering -= RenderProps;
                _renderingHooked = false;
            }

            _groups.Clear();
            _instancedMaterials.Clear();
            _occluders.Clear();
            _placements.Clear();
            _signedDistance = null;
            _terrain = null;
            _terrainCollider = null;
            PropCount = 0;
            BlockerCount = 0;
        }

        // ---------- 지면 ----------

        /// <summary>
        /// 걸을 수 있는 영역의 경계까지의 거리. 바깥은 양수, 안쪽은 음수다.
        /// 칸 단위 지도의 계단 모양이 그대로 드러나지 않도록 두 번 뭉갠다.
        /// </summary>
        private float[,] BuildSmoothedSignedDistance()
        {
            var signed = new float[Map.Width, Map.Height];
            for (int x = 0; x < Map.Width; x++)
            {
                for (int y = 0; y < Map.Height; y++)
                {
                    signed[x, y] = Map.IsWalkable(x, y)
                        ? -(Map.Clearance[x, y] - 0.5f)
                        : Map.DistanceToWalkable[x, y] - 0.5f;
                }
            }

            var buffer = new float[Map.Width, Map.Height];
            for (int pass = 0; pass < 2; pass++)
            {
                for (int x = 0; x < Map.Width; x++)
                {
                    for (int y = 0; y < Map.Height; y++)
                    {
                        float sum = 0f;
                        int count = 0;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                int nx = x + dx;
                                int ny = y + dy;
                                if (nx >= 0 && ny >= 0 && nx < Map.Width && ny < Map.Height)
                                {
                                    sum += signed[nx, ny];
                                    count++;
                                }
                            }
                        }

                        buffer[x, y] = sum / count;
                    }
                }

                (signed, buffer) = (buffer, signed);
            }

            return signed;
        }

        /// <summary>칸 중심에 저장된 값을 주변 네 칸으로 보간한다. 좌표는 지도 원점 기준이다.</summary>
        private float SampleSignedDistance(float mapX, float mapZ)
        {
            float u = Mathf.Clamp(mapX - 0.5f, 0f, Map.Width - 1f);
            float v = Mathf.Clamp(mapZ - 0.5f, 0f, Map.Height - 1f);
            int x0 = Mathf.FloorToInt(u);
            int y0 = Mathf.FloorToInt(v);
            int x1 = Mathf.Min(x0 + 1, Map.Width - 1);
            int y1 = Mathf.Min(y0 + 1, Map.Height - 1);
            float fx = u - x0;
            float fy = v - y0;

            float bottom = Mathf.Lerp(_signedDistance[x0, y0], _signedDistance[x1, y0], fx);
            float top = Mathf.Lerp(_signedDistance[x0, y1], _signedDistance[x1, y1], fx);
            return Mathf.Lerp(bottom, top, fy);
        }

        /// <summary>지면 높이. 걸을 수 있는 곳과 그 둘레 HillStart까지는 0이고, 그 바깥만 낮게 솟는다.</summary>
        private float GroundHeightAt(float mapX, float mapZ)
        {
            if (!UseTerrainGround || HillHeight <= 0f)
            {
                return 0f;
            }

            float beyond = SampleSignedDistance(mapX, mapZ) - HillStart;
            if (beyond <= 0f)
            {
                return 0f;
            }

            float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(beyond / HillWidth));
            float noise = Mathf.PerlinNoise(mapX / HillNoiseScale + _noiseOffset.x, mapZ / HillNoiseScale + _noiseOffset.y);
            return HillHeight * rise * Mathf.Lerp(0.35f, 1f, noise);
        }

        private void BuildTerrainGround(Transform root)
        {
            float maxHeight = Mathf.Max(HillHeight, 0.1f);
            TerrainData data = CreateTerrainData(HeightmapResolution, maxHeight);

            int resolution = data.heightmapResolution;
            var heights = new float[resolution, resolution];
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float mapX = (float)x / (resolution - 1) * Map.Width;
                    float mapZ = (float)z / (resolution - 1) * Map.Height;
                    heights[z, x] = GroundHeightAt(mapX, mapZ) / maxHeight;
                }
            }

            data.SetHeights(0, 0, heights);

            // 흙(공터와 길)과 풀을 거리에 따라 섞어 칠한다. 약한 잡음을 더해 경계선이 고르지 않게 한다.
            data.alphamapResolution = Mathf.ClosestPowerOfTwo(Mathf.Clamp(AlphamapResolution, 64, 2048));
            data.terrainLayers = new[] { CreateSolidLayer("Clearing", ClearingColor), CreateSolidLayer("Ground", GroundColor) };
            int alphaResolution = data.alphamapResolution;
            var weights = new float[alphaResolution, alphaResolution, 2];
            for (int z = 0; z < alphaResolution; z++)
            {
                for (int x = 0; x < alphaResolution; x++)
                {
                    float mapX = (x + 0.5f) / alphaResolution * Map.Width;
                    float mapZ = (z + 0.5f) / alphaResolution * Map.Height;
                    float wobble = Mathf.PerlinNoise(mapX * 0.35f + _noiseOffset.y, mapZ * 0.35f + _noiseOffset.x) - 0.5f;
                    float grass = Mathf.SmoothStep(0f, 1f,
                        Mathf.InverseLerp(-EdgeBlend, EdgeBlend, SampleSignedDistance(mapX, mapZ) + wobble));
                    weights[z, x, 0] = 1f - grass;
                    weights[z, x, 1] = grass;
                }
            }

            data.SetAlphamaps(0, 0, weights);

            _terrain = CreateTerrainObject(root, data);
            _terrainCollider = _terrain.GetComponent<TerrainCollider>();
        }

        private void BuildFlatGround(Transform root)
        {
            const float thickness = 0.2f;
            Vector2 center = Map.Origin + new Vector2(Map.Width, Map.Height) * 0.5f;
            CreatePrimitive(PrimitiveType.Cube, "Ground", root,
                new Vector3(center.x, -thickness * 0.5f, center.y),
                new Vector3(Map.Width, thickness, Map.Height), GroundColor, true);
        }

        /// <summary>공터와 길을 지면과 다른 색으로 보여 주는 메시 하나. 가로줄 단위로 사각형을 합친다.</summary>
        private void BuildWalkableSurface(Transform root)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            const float height = 0.01f;

            for (int y = 0; y < Map.Height; y++)
            {
                int x = 0;
                while (x < Map.Width)
                {
                    if (!Map.IsWalkable(x, y))
                    {
                        x++;
                        continue;
                    }

                    int start = x;
                    while (x < Map.Width && Map.IsWalkable(x, y))
                    {
                        x++;
                    }

                    int index = vertices.Count;
                    vertices.Add(new Vector3(Map.Origin.x + start, height, Map.Origin.y + y));
                    vertices.Add(new Vector3(Map.Origin.x + start, height, Map.Origin.y + y + 1));
                    vertices.Add(new Vector3(Map.Origin.x + x, height, Map.Origin.y + y + 1));
                    vertices.Add(new Vector3(Map.Origin.x + x, height, Map.Origin.y + y));
                    triangles.AddRange(new[] { index, index + 1, index + 2, index, index + 2, index + 3 });
                }
            }

            var mesh = Track(new Mesh { name = "LayoutTestWalkable" });
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var surface = new GameObject("WalkableSurface");
            surface.transform.SetParent(root, false);
            surface.AddComponent<MeshFilter>().sharedMesh = mesh;
            surface.AddComponent<MeshRenderer>().sharedMaterial = GetMaterial(ClearingColor, null);
        }

        // ---------- 충돌 ----------

        /// <summary>
        /// 걸을 수 없는 칸을 덮는 보이지 않는 상자들. 가로줄의 연속 구간을 구하고,
        /// 위아래 줄에서 같은 구간이 이어지면 하나로 합쳐 개수를 줄인다.
        /// </summary>
        private void BuildBlockers(Transform root)
        {
            var blockers = new GameObject("Blockers");
            blockers.transform.SetParent(root, false);

            var open = new Dictionary<Vector2Int, int>();
            var stillOpen = new Dictionary<Vector2Int, int>();

            for (int y = 0; y <= Map.Height; y++)
            {
                stillOpen.Clear();
                if (y < Map.Height)
                {
                    int x = 0;
                    while (x < Map.Width)
                    {
                        if (Map.IsWalkable(x, y))
                        {
                            x++;
                            continue;
                        }

                        int start = x;
                        while (x < Map.Width && !Map.IsWalkable(x, y))
                        {
                            x++;
                        }

                        var run = new Vector2Int(start, x);
                        stillOpen[run] = open.TryGetValue(run, out int startRow) ? startRow : y;
                    }
                }

                foreach (KeyValuePair<Vector2Int, int> rectangle in open)
                {
                    if (!stillOpen.ContainsKey(rectangle.Key))
                    {
                        AddBlocker(blockers, rectangle.Key.x, rectangle.Key.y, rectangle.Value, y);
                    }
                }

                (open, stillOpen) = (stillOpen, open);
            }
        }

        private void AddBlocker(GameObject blockers, int minX, int maxX, int minY, int maxY)
        {
            var box = blockers.AddComponent<BoxCollider>();
            box.center = new Vector3(
                Map.Origin.x + (minX + maxX) * 0.5f,
                BlockerHeight * 0.5f,
                Map.Origin.y + (minY + maxY) * 0.5f);
            box.size = new Vector3(maxX - minX, BlockerHeight, maxY - minY);
            BlockerCount++;
        }

        // ---------- 소품 ----------

        /// <summary>어떤 소품을 어디에 놓을지만 정한다. 그리는 방식은 그다음에 고른다.</summary>
        private void PlanProps(System.Random rng)
        {
            bool hasLow = LowProps != null && LowProps.Length > 0;
            bool hasTall = TallProps != null && TallProps.Length > 0;
            if (!hasLow && !hasTall)
            {
                return;
            }

            for (int x = 0; x < Map.Width; x++)
            {
                for (int y = 0; y < Map.Height; y++)
                {
                    if (Map.IsWalkable(x, y))
                    {
                        continue;
                    }

                    bool edge = Map.DistanceToWalkable[x, y] <= EdgeDepth;
                    if (rng.NextDouble() >= (edge ? EdgeDensity : FillDensity))
                    {
                        continue;
                    }

                    var jitter = new Vector2((float)rng.NextDouble() * 0.8f - 0.4f, (float)rng.NextDouble() * 0.8f - 0.4f);
                    float yaw = (float)rng.NextDouble() * 360f;
                    float scaleRoll = (float)rng.NextDouble();
                    int tallIndex = hasTall ? rng.Next(TallProps.Length) : 0;
                    int lowIndex = hasLow ? rng.Next(LowProps.Length) : 0;

                    // 가장자리와, 북쪽 가까이에 방이나 길이 있는 자리에는 낮은 소품만 쓴다.
                    // "가까이"는 그 소품의 꼭대기가 카메라에서 바닥을 가리는 거리까지다.
                    bool useTall = hasTall && !edge && TallProps[tallIndex] != null;
                    float scale = 0f;
                    if (useTall)
                    {
                        scale = Mathf.Lerp(TallPropScale.x, Mathf.Max(TallPropScale.x, TallPropScale.y), scaleRoll);
                        PrefabInfo info = GetPrefabInfo(TallProps[tallIndex]);
                        float shadowed = (info.Height * scale + HillHeight) / Mathf.Tan(CameraPitch * Mathf.Deg2Rad);
                        int sideways = Mathf.CeilToInt(info.Radius * scale) + 1;
                        useTall = !HasWalkableToTheNorth(x, y, Mathf.Max(CameraClearance, shadowed), sideways);
                    }

                    if (!useTall)
                    {
                        if (!hasLow || LowProps[lowIndex] == null)
                        {
                            continue;
                        }

                        scale = Mathf.Lerp(LowPropScale.x, Mathf.Max(LowPropScale.x, LowPropScale.y), scaleRoll);
                    }

                    GameObject prefab = useTall ? TallProps[tallIndex] : LowProps[lowIndex];

                    var placement = new PropPlacement
                    {
                        Prefab = prefab,
                        Position = Map.CellCenter(x, y) + jitter,
                        Yaw = yaw,
                        Scale = scale,
                        IsTall = useTall
                    };
                    _placements.Add(placement);
                    RegisterOccluder(placement);
                }
            }

            PropCount = _placements.Count;
        }

        private bool HasWalkableToTheNorth(int x, int y, float distance, int sideways)
        {
            int reach = Mathf.CeilToInt(distance);
            for (int dy = 1; dy <= reach && y + dy < Map.Height; dy++)
            {
                // 소품의 폭만큼 옆으로도 살핀다.
                for (int dx = -sideways; dx <= sideways; dx++)
                {
                    int nx = x + dx;
                    if (nx >= 0 && nx < Map.Width && Map.IsWalkable(nx, y + dy))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void InstantiateProps(Transform root)
        {
            Transform props = new GameObject("Props").transform;
            props.SetParent(root, false);
            // 소품은 보이기만 한다. 길 찾기에서도 제외한다.
            props.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

            foreach (PropPlacement placement in _placements)
            {
                GameObject instance = Instantiate(placement.Prefab, props);
                instance.name = placement.Prefab.name;
                instance.transform.localPosition = new Vector3(placement.Position.x,
                    GroundHeightAt(placement.Position.x - Map.Origin.x, placement.Position.y - Map.Origin.y),
                    placement.Position.y);
                instance.transform.localRotation = Quaternion.Euler(0f, placement.Yaw, 0f);
                instance.transform.localScale = placement.Prefab.transform.localScale * placement.Scale;

                if (GetPrefabInfo(placement.Prefab).HasColliders)
                {
                    // 충돌체가 달린 프리팹은 길과 방 안으로 튀어나와 이동을 막으므로 떼어 낸다.
                    foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
                    {
                        DestroyImmediate(collider);
                    }
                }
            }
        }

        /// <summary>
        /// 소품을 GameObject 없이 그릴 준비를 한다. 같은 프리팹끼리 묶어 변환 행렬과 감싸는 구를 만들어 둔다.
        /// 프리팹은 루트에 메시 하나가 있는 형태여야 한다. 그렇지 않은 프리팹은 건너뛰고 경고를 남긴다.
        /// </summary>
        private void BuildInstanceGroups()
        {
            var matricesByPrefab = new Dictionary<GameObject, List<Matrix4x4>>();
            var spheresByPrefab = new Dictionary<GameObject, List<Vector4>>();
            var tallPrefabs = new HashSet<GameObject>();
            var skipped = new HashSet<GameObject>();
            bool hasBounds = false;

            foreach (PropPlacement placement in _placements)
            {
                if (!placement.Prefab.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null
                    || !placement.Prefab.TryGetComponent(out MeshRenderer _))
                {
                    if (skipped.Add(placement.Prefab))
                    {
                        Debug.LogWarning($"[LayoutTest] {placement.Prefab.name}: 루트에 메시가 없어 묶어 그릴 수 없습니다.", this);
                    }

                    continue;
                }

                if (!matricesByPrefab.TryGetValue(placement.Prefab, out List<Matrix4x4> matrices))
                {
                    matrices = new List<Matrix4x4>();
                    matricesByPrefab.Add(placement.Prefab, matrices);
                    spheresByPrefab.Add(placement.Prefab, new List<Vector4>());
                }

                var local = new Vector3(placement.Position.x,
                    GroundHeightAt(placement.Position.x - Map.Origin.x, placement.Position.y - Map.Origin.y),
                    placement.Position.y);
                matrices.Add(transform.localToWorldMatrix * Matrix4x4.TRS(local,
                    Quaternion.Euler(0f, placement.Yaw, 0f) * placement.Prefab.transform.localRotation,
                    placement.Prefab.transform.localScale * placement.Scale));

                if (placement.IsTall)
                {
                    tallPrefabs.Add(placement.Prefab);
                }

                PrefabInfo info = GetPrefabInfo(placement.Prefab);
                float height = info.Height * placement.Scale;
                float radius = Mathf.Max(info.Radius * placement.Scale, height * 0.5f) * 1.5f;
                Vector3 center = transform.TransformPoint(local) + Vector3.up * (height * 0.5f);
                spheresByPrefab[placement.Prefab].Add(new Vector4(center.x, center.y, center.z, radius));

                var bounds = new Bounds(center, Vector3.one * (radius * 2f));
                if (hasBounds)
                {
                    _propBounds.Encapsulate(bounds);
                }
                else
                {
                    _propBounds = bounds;
                    hasBounds = true;
                }
            }

            foreach (KeyValuePair<GameObject, List<Matrix4x4>> entry in matricesByPrefab)
            {
                Material[] sources = entry.Key.GetComponent<MeshRenderer>().sharedMaterials;
                var materials = new Material[sources.Length];
                for (int i = 0; i < sources.Length; i++)
                {
                    materials[i] = GetInstancedMaterial(sources[i]);
                }

                _groups.Add(new InstanceGroup
                {
                    Mesh = entry.Key.GetComponent<MeshFilter>().sharedMesh,
                    Materials = materials,
                    All = entry.Value.ToArray(),
                    Spheres = spheresByPrefab[entry.Key].ToArray(),
                    Visible = new Matrix4x4[entry.Value.Count],
                    CastShadows = PropsCastShadows && (LowPropsCastShadows || tallPrefabs.Contains(entry.Key))
                });
            }

            if (_groups.Count > 0 && !_renderingHooked)
            {
                RenderPipelineManager.beginCameraRendering += RenderProps;
                _renderingHooked = true;
            }
        }

        /// <summary>인스턴싱을 켠 머티리얼 사본. 원본 머티리얼 에셋은 건드리지 않는다.</summary>
        private Material GetInstancedMaterial(Material source)
        {
            if (source == null)
            {
                return null;
            }

            if (!_instancedMaterials.TryGetValue(source, out Material instanced))
            {
                instanced = Track(new Material(source) { name = source.name + " (Instanced)", enableInstancing = true });
                _instancedMaterials.Add(source, instanced);
            }

            return instanced;
        }

        /// <summary>
        /// 카메라가 그리기 직전에 불린다. 그 카메라의 화면 안(과 그림자가 닿을 만큼 가까운 곳)에 있는 소품만 골라,
        /// 프리팹 종류마다 한 번씩 그린다. DrawCall은 소품 수가 아니라 프리팹 종류 수에 비례한다.
        /// </summary>
        private void RenderProps(ScriptableRenderContext context, Camera camera)
        {
            GeometryUtility.CalculateFrustumPlanes(camera, _frustumPlanes);
            foreach (InstanceGroup group in _groups)
            {
                float padding = group.CastShadows ? ShadowCullPadding : 0f;
                int visibleCount = 0;
                for (int i = 0; i < group.All.Length; i++)
                {
                    Vector4 sphere = group.Spheres[i];
                    var center = new Vector3(sphere.x, sphere.y, sphere.z);
                    bool inside = true;
                    for (int plane = 0; plane < 6; plane++)
                    {
                        if (_frustumPlanes[plane].GetDistanceToPoint(center) < -(sphere.w + padding))
                        {
                            inside = false;
                            break;
                        }
                    }

                    if (inside)
                    {
                        group.Visible[visibleCount++] = group.All[i];
                    }
                }

                if (visibleCount == 0)
                {
                    continue;
                }

                for (int subMesh = 0; subMesh < group.Mesh.subMeshCount && subMesh < group.Materials.Length; subMesh++)
                {
                    if (group.Materials[subMesh] == null)
                    {
                        continue;
                    }

                    var parameters = new RenderParams(group.Materials[subMesh])
                    {
                        camera = camera,
                        layer = gameObject.layer,
                        worldBounds = _propBounds,
                        shadowCastingMode = group.CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                        receiveShadows = true
                    };

                    // 한 번에 그릴 수 있는 개수의 상한(1023)에 맞춰 나눈다.
                    for (int start = 0; start < visibleCount; start += 1023)
                    {
                        Graphics.RenderMeshInstanced(parameters, group.Mesh, subMesh, group.Visible,
                            Mathf.Min(1023, visibleCount - start), start);
                    }
                }
            }
        }

        private void RegisterOccluder(PropPlacement placement)
        {
            PrefabInfo info = GetPrefabInfo(placement.Prefab);
            var local = new Vector3(placement.Position.x,
                GroundHeightAt(placement.Position.x - Map.Origin.x, placement.Position.y - Map.Origin.y),
                placement.Position.y);
            var occluder = new Occluder
            {
                Position = transform.TransformPoint(local),
                Radius = info.Radius * placement.Scale,
                Height = info.Height * placement.Scale
            };

            Vector2Int bucket = BucketOf(occluder.Position);
            if (!_occluders.TryGetValue(bucket, out List<Occluder> list))
            {
                list = new List<Occluder>();
                _occluders.Add(bucket, list);
            }

            list.Add(occluder);
        }

        /// <summary>프리팹의 바닥 반지름, 높이, 충돌체 유무. 프리팹 하나당 한 번만 읽는다.</summary>
        private PrefabInfo GetPrefabInfo(GameObject prefab)
        {
            if (_prefabInfos.TryGetValue(prefab, out PrefabInfo info))
            {
                return info;
            }

            info = new PrefabInfo { Radius = 0.5f, Height = 1f };
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }

                info.Radius = (bounds.extents.x + bounds.extents.z) * 0.5f;
                info.Height = bounds.max.y;
            }

            info.HasColliders = prefab.GetComponentInChildren<Collider>(true) != null;
            _prefabInfos.Add(prefab, info);
            return info;
        }

        private static Vector2Int BucketOf(Vector3 position)
        {
            return new Vector2Int(
                Mathf.FloorToInt(position.x / OccluderBucketSize),
                Mathf.FloorToInt(position.z / OccluderBucketSize));
        }
    }
}
