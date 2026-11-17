using UnityEngine;

namespace TopDownBase.Dungeon.Experiments
{
    /// <summary>
    /// 경계를 Unity Terrain의 높낮이로 표현하는 테스트.
    /// 걸을 수 있는 곳은 높이 0, 경계는 가장자리에서 SlopeWidth에 걸쳐 BoundaryHeight까지 올라간다.
    /// 충돌과 NavMesh는 Terrain이 맡는다. 지형 데이터는 메모리에만 있어 씬에 저장되지 않는다.
    /// </summary>
    public class TerrainBoundaryBuilder : LayoutTestBuilder
    {
        [Header("Terrain")]
        [Min(0.5f)] public float BoundaryHeight = 4f;
        [Tooltip("경계 가장자리에서 꼭대기까지의 수평 거리(유닛). 높이맵은 수직 벽을 만들 수 없어 이만큼의 폭이 필요하다.")]
        [Min(0.5f)] public float SlopeWidth = 2.5f;
        [Min(0f)] public float NoiseHeight = 0.6f;
        [Min(1f)] public float NoiseScale = 9f;
        [Tooltip("높이맵 해상도. 2의 거듭제곱 + 1이어야 한다.")]
        public int HeightmapResolution = 257;
        public Color FloorColor = new Color(0.52f, 0.47f, 0.38f);
        public Color BoundaryColor = new Color(0.3f, 0.42f, 0.27f);

        private TerrainCollider _terrainCollider;

        protected override void BuildBoundary(Transform root, System.Random rng)
        {
            float maxHeight = BoundaryHeight + NoiseHeight;
            TerrainData data = CreateTerrainData(HeightmapResolution, maxHeight);
            int resolution = data.heightmapResolution;

            float noiseOffsetX = (float)rng.NextDouble() * 1000f;
            float noiseOffsetZ = (float)rng.NextDouble() * 1000f;
            var heights = new float[resolution, resolution];
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float mapX = (float)x / (resolution - 1) * Map.Width;
                    float mapZ = (float)z / (resolution - 1) * Map.Height;
                    heights[z, x] = HeightAt(mapX, mapZ, noiseOffsetX, noiseOffsetZ) / maxHeight;
                }
            }

            data.SetHeights(0, 0, heights);
            PaintLayers(data);

            Terrain terrain = CreateTerrainObject(root, data);
            _terrainCollider = terrain.GetComponent<TerrainCollider>();
        }

        protected override bool IsOccludedByBoundary(Vector3 point, Vector3 towardCamera, float maxDistance)
        {
            return _terrainCollider != null
                   && _terrainCollider.Raycast(new Ray(point, towardCamera), out _, maxDistance);
        }

        protected override void OnClear()
        {
            _terrainCollider = null;
        }

        /// <summary>
        /// 걸을 수 있는 영역의 가장자리에서 멀어질수록 높아진다. 바닥 쪽을 가파르게, 꼭대기 쪽을 완만하게 해
        /// 걸을 수 있는 영역의 끝이 분명하게 보이도록 한다.
        /// </summary>
        private float HeightAt(float mapX, float mapZ, float noiseOffsetX, float noiseOffsetZ)
        {
            // 거리는 칸 중심 기준이므로, 걸을 수 있는 칸의 가장자리는 0.5 지점이다.
            float fromEdge = SampleDistance(mapX, mapZ) - 0.5f;
            if (fromEdge <= 0f)
            {
                return 0f;
            }

            float t = Mathf.Clamp01(fromEdge / SlopeWidth);
            float height = BoundaryHeight * (1f - (1f - t) * (1f - t));

            float onTop = Mathf.Clamp01((fromEdge - SlopeWidth) / SlopeWidth);
            float noise = Mathf.PerlinNoise(mapX / NoiseScale + noiseOffsetX, mapZ / NoiseScale + noiseOffsetZ);
            return height + NoiseHeight * noise * onTop;
        }

        /// <summary>칸 중심에 저장된 거리 값을 주변 네 칸으로 보간한다.</summary>
        private float SampleDistance(float mapX, float mapZ)
        {
            float u = Mathf.Clamp(mapX - 0.5f, 0f, Map.Width - 1f);
            float v = Mathf.Clamp(mapZ - 0.5f, 0f, Map.Height - 1f);
            int x0 = Mathf.FloorToInt(u);
            int y0 = Mathf.FloorToInt(v);
            int x1 = Mathf.Min(x0 + 1, Map.Width - 1);
            int y1 = Mathf.Min(y0 + 1, Map.Height - 1);
            float fx = u - x0;
            float fy = v - y0;

            float[,] distance = Map.DistanceToWalkable;
            float bottom = Mathf.Lerp(distance[x0, y0], distance[x1, y0], fx);
            float top = Mathf.Lerp(distance[x0, y1], distance[x1, y1], fx);
            return Mathf.Lerp(bottom, top, fy);
        }

        /// <summary>바닥과 경계를 단색 지형 레이어 두 개로 구분한다.</summary>
        private void PaintLayers(TerrainData data)
        {
            data.terrainLayers = new[] { CreateSolidLayer("Floor", FloorColor), CreateSolidLayer("Boundary", BoundaryColor) };

            int resolution = data.alphamapResolution;
            var weights = new float[resolution, resolution, 2];
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float mapX = (x + 0.5f) / resolution * Map.Width;
                    float mapZ = (z + 0.5f) / resolution * Map.Height;
                    float boundary = Mathf.Clamp01(SampleDistance(mapX, mapZ) - 0.5f);
                    weights[z, x, 0] = 1f - boundary;
                    weights[z, x, 1] = boundary;
                }
            }

            data.SetAlphamaps(0, 0, weights);
        }
    }
}
