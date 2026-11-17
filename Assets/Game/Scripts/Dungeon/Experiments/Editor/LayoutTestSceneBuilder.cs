using TopDownBase.Dungeon.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
            builder.ObstaclePrefabs = LoadProps("Stone_1", "Stone_2", "Stone_3");
            builder.LowProps = LoadProps("Stone_1", "Stone_2", "Stone_3", "Grass_1", "Grass_5", "Grass_10");
            builder.TallProps = LoadProps("Tree_1", "Tree_2", "Tree_3");

            DungeonPrototypeSceneBuilder.CreateRig(builder);
            SaveNewScene(scene, NatureScenePath);
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
