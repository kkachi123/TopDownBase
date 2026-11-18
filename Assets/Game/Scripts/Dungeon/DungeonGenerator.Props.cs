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
        private readonly Dictionary<Material, Material> _instancedMaterials = new Dictionary<Material, Material>();
        private readonly Plane[] _frustumPlanes = new Plane[6];
        private Bounds _propBounds;
        private bool _renderingHooked;

        /// <summary>놓인 소품 수. 측정 기록용이다.</summary>
        public int PropCount { get; private set; }

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
            _instancedMaterials.Clear();
            _occluders.Clear();
            _placements.Clear();
            // 프리팹 크기는 인스펙터에서 바뀔 수 있으므로 생성마다 다시 읽는다.
            _prefabInfos.Clear();
            PropCount = 0;
        }

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
                    // 첫 줄은 대각선 거리(1.41)까지다.
                    bool rim = Map.DistanceToWalkable[x, y] <= 1.5f;
                    float density = rim ? Mathf.Max(EdgeDensity, RimDensity) : edge ? EdgeDensity : FillDensity;
                    if (rng.NextDouble() >= density)
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

                    var placement = new PropPlacement
                    {
                        Prefab = useTall ? TallProps[tallIndex] : LowProps[lowIndex],
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
                instance.transform.localPosition = GroundPoint(placement.Position);
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
                        Debug.LogWarning($"[Dungeon] {placement.Prefab.name}: 루트에 메시가 없어 묶어 그릴 수 없습니다.", this);
                    }

                    continue;
                }

                if (!matricesByPrefab.TryGetValue(placement.Prefab, out List<Matrix4x4> matrices))
                {
                    matrices = new List<Matrix4x4>();
                    matricesByPrefab.Add(placement.Prefab, matrices);
                    spheresByPrefab.Add(placement.Prefab, new List<Vector4>());
                }

                Vector3 local = GroundPoint(placement.Position);
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
