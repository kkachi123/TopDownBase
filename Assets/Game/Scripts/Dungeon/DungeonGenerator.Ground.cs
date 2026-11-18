using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TopDownBase.Dungeon
{
    /// <summary>
    /// 지면과 이동 제한. 걸을 수 있는 곳은 높이 0의 평지이고, 경계 바깥에만 낮은 언덕을 둔다.
    /// 이동을 막는 것은 걸을 수 없는 칸을 덮는 보이지 않는 상자다.
    /// </summary>
    public partial class DungeonGenerator
    {
        private float[,] _signedDistance;
        private Vector2 _noiseOffset;
        private TerrainCollider _terrainCollider;

        /// <summary>걸을 수 없는 칸을 덮는 충돌 상자 수. 측정 기록용이다.</summary>
        public int BlockerCount { get; private set; }

        /// <summary>지면, 이동 제한 상자, 경계 소품을 만든다.</summary>
        private void BuildGround(Transform root, System.Random rng)
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
            BuildProps(root, rng);
        }

        private void ClearGround()
        {
            _signedDistance = null;
            _terrainCollider = null;
            BlockerCount = 0;
        }

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

        /// <summary>생성기 기준 위치의 지면 높이를 포함한 로컬 좌표.</summary>
        private Vector3 GroundPoint(Vector2 mapPosition)
        {
            return new Vector3(mapPosition.x,
                GroundHeightAt(mapPosition.x - Map.Origin.x, mapPosition.y - Map.Origin.y),
                mapPosition.y);
        }

        private void BuildTerrainGround(Transform root)
        {
            float maxHeight = Mathf.Max(HillHeight, 0.1f);
            int resolution = Mathf.ClosestPowerOfTwo(Mathf.Clamp(HeightmapResolution - 1, 32, 1024)) + 1;
            // 해상도를 먼저 정해야 size가 의도한 값으로 유지된다.
            var data = Track(new TerrainData { name = "DungeonTerrain" });
            data.heightmapResolution = resolution;
            data.size = new Vector3(Map.Width, maxHeight, Map.Height);

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

            // 지형 오브젝트를 맵의 남서쪽 모서리에 놓는다. 지형 데이터는 메모리에만 있다.
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

            _terrainCollider = terrainObject.GetComponent<TerrainCollider>();
        }

        /// <summary>한 가지 색으로 칠하는 지형 레이어.</summary>
        private TerrainLayer CreateSolidLayer(string layerName, Color color)
        {
            var texture = Track(new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = $"Dungeon_{layerName}" });
            texture.SetPixels(new[] { color, color, color, color });
            texture.Apply();

            return Track(new TerrainLayer
            {
                name = $"Dungeon_{layerName}",
                diffuseTexture = texture,
                tileSize = new Vector2(4f, 4f),
                metallic = 0f,
                smoothness = 0f
            });
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

            var mesh = Track(new Mesh { name = "DungeonWalkable" });
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
    }
}
