using TopDownBase.Dungeon.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TopDownBase.Dungeon.Experiments.Editor
{
    /// <summary>
    /// 경계 표현 테스트 씬 두 개를 만들거나 여는 메뉴.
    /// 씬에는 빌더·임시 이동체·카메라·조명만 저장하고, 맵은 Play나 인스펙터 버튼으로 만든다.
    /// </summary>
    public static class LayoutTestSceneBuilder
    {
        public const string SceneFolder = "Assets/Scenes/Experiments";
        public const string TerrainScenePath = SceneFolder + "/TerrainBoundaryTest.unity";
        public const string NatureScenePath = SceneFolder + "/NatureLayoutTest.unity";

        private const string PropFolder = "Assets/Game/Environment/PolyStyle/Prefabs/Environments/";
        private const string NatureVolumeProfilePath = "Assets/Game/Environment/PolyStyle/Settings/NatureVolumeProfile.asset";

        [MenuItem("TopDownBase/Layout Tests/Create Or Open Terrain Boundary Scene")]
        public static void OpenTerrainSceneFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                CreateOrOpenTerrainScene();
            }
        }

        [MenuItem("TopDownBase/Layout Tests/Create Or Open Nature Layout Scene")]
        public static void OpenNatureSceneFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                CreateOrOpenNatureScene();
            }
        }

        /// <summary>Terrain 높낮이로 경계를 만드는 씬. 방 모양의 기본값은 현재 던전과 같은 직사각형이다.</summary>
        public static void CreateOrOpenTerrainScene()
        {
            if (TryOpenExisting(TerrainScenePath))
            {
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var builder = new GameObject("TerrainBoundaryBuilder").AddComponent<TerrainBoundaryBuilder>();
            builder.Shape = WalkableShape.Rectangle;
            builder.BoundaryWidth = 6f;
            builder.PathWidth = 6f;
            builder.PathBend = 0f;
            builder.ObstaclePrefabs = LoadProps("Stone_1", "Stone_2", "Stone_3");

            DungeonPrototypeSceneBuilder.CreateRig(builder);
            SaveNewScene(scene, TerrainScenePath);
        }

        /// <summary>소품으로 경계를 만드는 씬. 방 모양의 기본값은 불규칙 공터와 굽은 길이다.</summary>
        public static void CreateOrOpenNatureScene()
        {
            if (TryOpenExisting(NatureScenePath))
            {
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var builder = new GameObject("NatureBoundaryBuilder").AddComponent<NatureBoundaryBuilder>();
            builder.Shape = WalkableShape.Clearing;
            builder.BoundaryWidth = 5f;
            builder.PathWidth = 5f;
            builder.PathBend = 2f;
            // Rock_*: Vegetation Pack 2의 바위에 URP 머티리얼을 입히고 0.7배로 줄인 변형.
            // 원래 크기로는 바닥 가림이 17%까지 올라간다. Stone_*(Starter Pack)은 청록색 덩어리 모양이다.
            builder.ObstaclePrefabs = LoadProps("Rock_1", "Rock_2", "Rock_3");
            builder.ObstaclePrefabDiameter = 1.5f;
            builder.LowProps = LoadProps("Rock_1", "Rock_2", "Rock_3", "Grass_1", "Grass_5", "Grass_10");
            builder.TallProps = LoadProps("Tree_1", "Tree_2", "Tree_3");

            Camera camera = DungeonPrototypeSceneBuilder.CreateRig(builder);
            AddPostProcessing(camera);
            SaveNewScene(scene, NatureScenePath);
        }

        /// <summary>
        /// POLY STYLE 데모의 후처리(대비·채도·화이트밸런스·블룸 등)를 사본 프로필로 건다.
        /// 이 에셋들은 후처리를 전제로 색을 잡아서, 없으면 칙칙하게 보인다.
        /// </summary>
        public static void AddPostProcessing(Camera camera)
        {
            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(NatureVolumeProfilePath);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }

        private static bool TryOpenExisting(string scenePath)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                return false;
            }

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            return true;
        }

        private static void SaveNewScene(Scene scene, string scenePath)
        {
            if (!AssetDatabase.IsValidFolder(SceneFolder))
            {
                AssetDatabase.CreateFolder("Assets/Scenes", "Experiments");
            }

            EditorSceneManager.SaveScene(scene, scenePath);
        }

        private static GameObject[] LoadProps(params string[] names)
        {
            var props = new GameObject[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                props[i] = AssetDatabase.LoadAssetAtPath<GameObject>(PropFolder + names[i] + ".prefab");
            }

            return props;
        }
    }
}
