using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.Rendering;

namespace TopDownBase.Dungeon
{
    /// <summary>경계 소품을 그리는 방식.</summary>
    public enum DungeonPropRendering
    {
        /// <summary>소품마다 GameObject를 만든다. 오브젝트 수와 DrawCall이 소품 수만큼 늘어난다. 비교·확인용이다.</summary>
        GameObjects,
        /// <summary>같은 메시를 묶어 한 번에 그린다. 화면에 보이는 소품만 골라 그리며 GameObject를 만들지 않는다.</summary>
        Instanced
    }

    /// <summary>
    /// 경계 소품. 걸을 수 없는 칸의 가장자리에 바위·덤불을, 그 바깥에 나무를 놓는다. 소품은 보이기만 하고 충돌하지 않는다.
    /// 방과 길의 남쪽에는 키 큰 소품을 놓지 않아 카메라가 바닥을 볼 수 있게 한다.
    /// </summary>
    public partial class DungeonGenerator
    {
        private const float OccluderBucketSize = 4f;
        private const int LandmarkSeedSalt = 0x4C4D4B;

        private struct Occluder
        {
            public Vector3 Position;
            public float Radius;
            public float Height;
        }

        private struct PrefabInfo
        {
            public float Radius;
            /// <summary>바닥 사각형의 중심에서 모서리까지 거리. 어느 방향으로 돌려도 바닥이 이 원 안에 든다.</summary>
            public float HalfDiagonal;
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
            public Color Tint;
            public float Desaturate;
        }

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

        // 프리팹마다 한 번만 읽은 정보. 소품을 놓을 때마다 계층을 훑지 않기 위함이다.
        private readonly Dictionary<GameObject, PrefabInfo> _prefabInfos = new Dictionary<GameObject, PrefabInfo>();
        private readonly Dictionary<Vector2Int, List<Occluder>> _occluders = new Dictionary<Vector2Int, List<Occluder>>();
        private readonly List<PropPlacement> _placements = new List<PropPlacement>();
        private readonly List<InstanceGroup> _groups = new List<InstanceGroup>();
        // (원본, 곱할 색, 회색으로 옮기는 정도, 인스턴싱)마다 만든 머티리얼 사본.
        private readonly Dictionary<(Material, Color, float, bool), Material> _materialCopies = new Dictionary<(Material, Color, float, bool), Material>();
        // 놓인 랜드마크의 바닥 원(x, z: 맵 좌표 중심, w: 반지름). 다른 소품이 그 안에 놓이지 않게 한다.
        private readonly List<Vector3> _landmarkFootprints = new List<Vector3>();
        private readonly Plane[] _frustumPlanes = new Plane[6];
        private Bounds _propBounds;
        private bool _renderingHooked;

        /// <summary>놓인 소품 수(랜드마크 포함). 측정 기록용이다.</summary>
        public int PropCount { get; private set; }

        /// <summary>놓인 랜드마크 수. 측정 기록용이다.</summary>
        public int LandmarkCount => _landmarkFootprints.Count;

        private void BuildProps(Transform root, System.Random rng)
        {
            PlanProps(rng);

            if (PropRendering == DungeonPropRendering.Instanced)
            {
                BuildInstanceGroups();
            }
            else
            {
                InstantiateProps(root);
            }
        }

        private void ClearProps()
        {
            if (_renderingHooked)
            {
                RenderPipelineManager.beginCameraRendering -= RenderProps;
                _renderingHooked = false;
            }

            _groups.Clear();
            _materialCopies.Clear();
            _occluders.Clear();
            _placements.Clear();
            _landmarkFootprints.Clear();
            // 프리팹 크기는 인스펙터에서 바뀔 수 있으므로 생성마다 다시 읽는다.
            _prefabInfos.Clear();
            PropCount = 0;
        }

        /// <summary>어떤 소품을 어디에 놓을지만 정한다. 그리는 방식은 그다음에 고른다.</summary>
        private void PlanProps(System.Random rng)
        {
            DungeonEnvironmentSettings env = Env;
            // 랜드마크는 따로 만든 난수로 먼저 놓는다. 랜드마크가 없는 환경의 소품 배치는 이전과 같다.
            PlanLandmarks(env);
            if (!HasChoice(env.LowProps) && !HasChoice(env.TallProps))
            {
                PropCount = _placements.Count;
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

                    bool edge = Map.DistanceToWalkable[x, y] <= env.EdgeDepth;
                    // 첫 줄은 대각선 거리(1.41)까지다.
                    bool rim = Map.DistanceToWalkable[x, y] <= 1.5f;
                    float density = rim ? Mathf.Max(env.EdgeDensity, env.RimDensity) : edge ? env.EdgeDensity : env.FillDensity;
                    if (rng.NextDouble() >= density)
                    {
                        continue;
                    }

                    var jitter = new Vector2((float)rng.NextDouble() * 0.8f - 0.4f, (float)rng.NextDouble() * 0.8f - 0.4f);
                    float yaw = (float)rng.NextDouble() * 360f;
                    float scaleRoll = (float)rng.NextDouble();
                    DungeonPropEntry tall = PickWeighted(env.TallProps, rng);
                    DungeonPropEntry low = PickWeighted(env.LowProps, rng);

                    // 가장자리와, 북쪽 가까이에 방이나 길이 있는 자리에는 낮은 소품만 쓴다.
                    // "가까이"는 그 소품의 꼭대기가 카메라에서 바닥을 가리는 거리까지다.
                    DungeonPropEntry entry = null;
                    if (tall != null && !edge)
                    {
                        float tallScale = RollScale(tall, scaleRoll);
                        PrefabInfo info = GetPrefabInfo(tall.Prefab);
                        float shadowed = (info.Height * tallScale + env.HillHeight) / Mathf.Tan(CameraPitch * Mathf.Deg2Rad);
                        int sideways = Mathf.CeilToInt(info.Radius * tallScale) + 1;
                        if (!HasWalkableToTheNorth(x, y, Mathf.Max(CameraClearance, shadowed), sideways))
                        {
                            entry = tall;
                        }
                    }

                    // 랜드마크 자리에는 놓지 않는다. 난수는 위에서 이미 다 썼으므로 다른 칸의 배치는 달라지지 않는다.
                    if (IsInsideLandmark(Map.CellCenter(x, y) + jitter, 1f))
                    {
                        continue;
                    }

                    bool isTall = entry != null;
                    if (!isTall)
                    {
                        if (low == null)
                        {
                            continue;
                        }

                        entry = low;
                    }

                    var placement = new PropPlacement
                    {
                        Prefab = entry.Prefab,
                        Position = Map.CellCenter(x, y) + jitter,
                        Yaw = yaw,
                        Scale = RollScale(entry, scaleRoll),
                        IsTall = isTall,
                        Tint = entry.Tint,
                        Desaturate = entry.Desaturate
                    };
                    _placements.Add(placement);
                    RegisterOccluder(placement);
                }
            }

            PropCount = _placements.Count;
        }

        /// <summary>
        /// 건물처럼 크고 드문 소품을 놓는다. 막힌 칸을 무작위 순서로 훑으며, 바닥 원이 공터·길에서 충분히 떨어져 있고
        /// 다른 랜드마크와 간격이 있으며 북쪽 가까이에 걸을 수 있는 곳이 없는(카메라에서 바닥을 가리지 않는) 자리만 쓴다.
        /// </summary>
        private void PlanLandmarks(DungeonEnvironmentSettings env)
        {
            if (!HasChoice(env.Landmarks) || env.LandmarksPerRoom <= 0f)
            {
                return;
            }

            var rng = new System.Random(Seed ^ LandmarkSeedSalt);
            int target = Mathf.RoundToInt(Map.Rooms.Count * env.LandmarksPerRoom);
            var candidates = new List<Vector2Int>();
            for (int x = 0; x < Map.Width; x++)
            {
                for (int y = 0; y < Map.Height; y++)
                {
                    if (!Map.IsWalkable(x, y) && Map.DistanceToWalkable[x, y] >= env.LandmarkClearance + 1f)
                    {
                        candidates.Add(new Vector2Int(x, y));
                    }
                }
            }

            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            float tanPitch = Mathf.Tan(CameraPitch * Mathf.Deg2Rad);
            foreach (Vector2Int cell in candidates)
            {
                if (_landmarkFootprints.Count >= target)
                {
                    break;
                }

                DungeonPropEntry entry = PickWeighted(env.Landmarks, rng);
                float scale = RollScale(entry, (float)rng.NextDouble());
                float yaw = (float)rng.NextDouble() * 360f;
                PrefabInfo info = GetPrefabInfo(entry.Prefab);
                // 무작위로 돌려 놓으므로 모서리까지 감싸는 원으로 간격을 잰다.
                float radius = info.HalfDiagonal * scale;
                Vector2 position = Map.CellCenter(cell.x, cell.y);

                if (Map.DistanceToWalkable[cell.x, cell.y] < radius + env.LandmarkClearance
                    || cell.x - radius < 0f || cell.y - radius < 0f
                    || cell.x + radius > Map.Width || cell.y + radius > Map.Height
                    || IsInsideLandmark(position, radius + env.LandmarkSpacing))
                {
                    continue;
                }

                // 바닥 원의 북쪽 끝부터 꼭대기가 바닥을 가리는 거리까지 걸을 수 있는 곳이 없어야 한다.
                float shadowed = (info.Height * scale + env.HillHeight) / tanPitch + radius;
                if (HasWalkableToTheNorth(cell.x, cell.y, Mathf.Max(CameraClearance, shadowed), Mathf.CeilToInt(radius) + 1))
                {
                    continue;
                }

                var placement = new PropPlacement
                {
                    Prefab = entry.Prefab,
                    Position = position,
                    Yaw = yaw,
                    Scale = scale,
                    IsTall = true,
                    Tint = entry.Tint,
                    Desaturate = entry.Desaturate
                };
                _placements.Add(placement);
                RegisterOccluder(placement);
                _landmarkFootprints.Add(new Vector3(position.x, position.y, radius));
            }
        }

        /// <summary>점이 어떤 랜드마크의 바닥 원에서 margin 안쪽에 있는지.</summary>
        private bool IsInsideLandmark(Vector2 position, float margin)
        {
            foreach (Vector3 footprint in _landmarkFootprints)
            {
                float reach = footprint.z + margin;
                if ((position - new Vector2(footprint.x, footprint.y)).sqrMagnitude < reach * reach)
                {
                    return true;
                }
            }

            return false;
        }

        private static float RollScale(DungeonPropEntry entry, float roll)
        {
            return Mathf.Lerp(entry.Scale.x, Mathf.Max(entry.Scale.x, entry.Scale.y), roll);
        }

        private static bool HasChoice<T>(T[] choices) where T : DungeonPrefabChoice
        {
            if (choices == null)
            {
                return false;
            }

            foreach (T choice in choices)
            {
                if (choice != null && choice.Prefab != null && choice.Weight > 0f)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>가중치에 따라 하나를 뽑는다. 쓸 수 있는 항목이 없으면 null. 항목이 있으면 난수를 정확히 한 번 쓴다.</summary>
        private static T PickWeighted<T>(T[] choices, System.Random rng) where T : DungeonPrefabChoice
        {
            if (!HasChoice(choices))
            {
                return null;
            }

            float total = 0f;
            foreach (T choice in choices)
            {
                if (choice != null && choice.Prefab != null && choice.Weight > 0f)
                {
                    total += choice.Weight;
                }
            }

            float roll = (float)rng.NextDouble() * total;
            T last = null;
            foreach (T choice in choices)
            {
                if (choice == null || choice.Prefab == null || choice.Weight <= 0f)
                {
                    continue;
                }

                last = choice;
                roll -= choice.Weight;
                if (roll < 0f)
                {
                    return choice;
                }
            }

            return last;
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
                instance.transform.localPosition = GroundPoint(placement.Position);
                instance.transform.localRotation = Quaternion.Euler(0f, placement.Yaw, 0f);
                instance.transform.localScale = placement.Prefab.transform.localScale * placement.Scale;
                ApplyTint(instance, placement.Tint, placement.Desaturate);

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
            // 같은 프리팹이라도 색 보정이 다르면 머티리얼이 달라 따로 묶는다.
            var matricesByPrefab = new Dictionary<(GameObject, Color, float), List<Matrix4x4>>();
            var spheresByPrefab = new Dictionary<(GameObject, Color, float), List<Vector4>>();
            var tallPrefabs = new HashSet<(GameObject, Color, float)>();
            var skipped = new HashSet<GameObject>();
            bool hasBounds = false;

            foreach (PropPlacement placement in _placements)
            {
                if (!placement.Prefab.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null
                    || !placement.Prefab.TryGetComponent(out MeshRenderer _))
                {
                    if (skipped.Add(placement.Prefab))
                    {
                        Debug.LogWarning($"[Dungeon] {placement.Prefab.name}: 루트에 메시가 없어 묶어 그릴 수 없습니다.", this);
                    }

                    continue;
                }

                var key = (placement.Prefab, placement.Tint, placement.Desaturate);
                if (!matricesByPrefab.TryGetValue(key, out List<Matrix4x4> matrices))
                {
                    matrices = new List<Matrix4x4>();
                    matricesByPrefab.Add(key, matrices);
                    spheresByPrefab.Add(key, new List<Vector4>());
                }

                Vector3 local = GroundPoint(placement.Position);
                matrices.Add(transform.localToWorldMatrix * Matrix4x4.TRS(local,
                    Quaternion.Euler(0f, placement.Yaw, 0f) * placement.Prefab.transform.localRotation,
                    placement.Prefab.transform.localScale * placement.Scale));

                if (placement.IsTall)
                {
                    tallPrefabs.Add(key);
                }

                PrefabInfo info = GetPrefabInfo(placement.Prefab);
                float height = info.Height * placement.Scale;
                float radius = Mathf.Max(info.Radius * placement.Scale, height * 0.5f) * 1.5f;
                Vector3 center = transform.TransformPoint(local) + Vector3.up * (height * 0.5f);
                spheresByPrefab[key].Add(new Vector4(center.x, center.y, center.z, radius));

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

            DungeonEnvironmentSettings env = Env;
            foreach (KeyValuePair<(GameObject, Color, float), List<Matrix4x4>> entry in matricesByPrefab)
            {
                (GameObject prefab, Color tint, float desaturate) = entry.Key;
                Material[] sources = prefab.GetComponent<MeshRenderer>().sharedMaterials;
                var materials = new Material[sources.Length];
                for (int i = 0; i < sources.Length; i++)
                {
                    materials[i] = GetMaterialCopy(sources[i], tint, desaturate, true);
                }

                _groups.Add(new InstanceGroup
                {
                    Mesh = prefab.GetComponent<MeshFilter>().sharedMesh,
                    Materials = materials,
                    All = entry.Value.ToArray(),
                    Spheres = spheresByPrefab[entry.Key].ToArray(),
                    Visible = new Matrix4x4[entry.Value.Count],
                    CastShadows = env.PropsCastShadows && (env.LowPropsCastShadows || tallPrefabs.Contains(entry.Key))
                });
            }

            if (_groups.Count > 0 && !_renderingHooked)
            {
                RenderPipelineManager.beginCameraRendering += RenderProps;
                _renderingHooked = true;
            }
        }

        /// <summary>
        /// 원본 머티리얼의 사본. 인스턴싱을 켜거나 색을 보정할 때 쓴다. 원본 머티리얼 에셋은 건드리지 않는다.
        /// 색을 보정하지 않고 인스턴싱도 필요 없으면 원본을 그대로 돌려준다.
        /// </summary>
        private Material GetMaterialCopy(Material source, Color tint, float desaturate, bool instancing)
        {
            bool recolor = tint != Color.white || desaturate > 0f;
            if (source == null || (!recolor && !instancing))
            {
                return source;
            }

            var key = (source, tint, desaturate, instancing);
            if (!_materialCopies.TryGetValue(key, out Material copy))
            {
                copy = Track(new Material(source)
                {
                    name = source.name + (instancing ? " (Instanced)" : " (Tinted)"),
                    enableInstancing = instancing || source.enableInstancing
                });

                if (recolor)
                {
                    if (copy.HasProperty(BaseColorId))
                    {
                        copy.SetColor(BaseColorId, Recolor(copy.GetColor(BaseColorId), tint, desaturate));
                    }

                    if (copy.HasProperty(ColorId))
                    {
                        copy.SetColor(ColorId, Recolor(copy.GetColor(ColorId), tint, desaturate));
                    }
                }

                _materialCopies.Add(key, copy);
            }

            return copy;
        }

        /// <summary>원래 색을 회색 쪽으로 desaturate만큼 옮긴 뒤 tint를 곱한다. 둘 다 기본값이면 원래 색 그대로다.</summary>
        private static Color Recolor(Color color, Color tint, float desaturate)
        {
            float luminance = color.r * 0.299f + color.g * 0.587f + color.b * 0.114f;
            return Color.Lerp(color, new Color(luminance, luminance, luminance, color.a), desaturate) * tint;
        }

        /// <summary>오브젝트로 만든 소품·장애물·문의 머티리얼을 색을 보정한 사본으로 바꾼다.</summary>
        private void ApplyTint(GameObject instance, Color tint, float desaturate)
        {
            if (tint == Color.white && desaturate <= 0f)
            {
                return;
            }

            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = GetMaterialCopy(materials[i], tint, desaturate, false);
                }

                renderer.sharedMaterials = materials;
            }
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
            var occluder = new Occluder
            {
                Position = transform.TransformPoint(GroundPoint(placement.Position)),
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

        /// <summary>바닥의 한 점이 지형이나 경계 소품에 가려 카메라에서 보이지 않는지.</summary>
        private bool IsOccludedByBoundary(Vector3 point, Vector3 towardCamera, float maxDistance)
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

        /// <summary>프리팹의 바닥 반지름, 높이, 충돌체 유무. 프리팹 하나당 한 번만 읽는다.</summary>
        private PrefabInfo GetPrefabInfo(GameObject prefab)
        {
            if (_prefabInfos.TryGetValue(prefab, out PrefabInfo info))
            {
                return info;
            }

            info = new PrefabInfo { Radius = 0.5f, HalfDiagonal = 0.71f, Height = 1f };
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }

                info.Radius = (bounds.extents.x + bounds.extents.z) * 0.5f;
                info.HalfDiagonal = new Vector2(bounds.extents.x, bounds.extents.z).magnitude;
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
