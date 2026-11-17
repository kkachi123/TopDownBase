using TopDownBase.Dungeon.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TopDownBase.Dungeon.Editor
{
    /// <summary>
    /// 던전 생성 프로토타입 씬을 만들거나 여는 메뉴.
    /// 씬에는 생성기·임시 이동체·카메라·조명만 저장하고, 던전 형상은 Play 시 생성한다.
    /// </summary>
    public static class DungeonPrototypeSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/DungeonPrototype.unity";

        [MenuItem("TopDownBase/Dungeon Prototype/Create Or Open Scene")]
        public static void CreateOrOpenFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            CreateOrOpen();
        }

        /// <summary>씬 파일이 있으면 열고, 없으면 새로 구성해 저장한다. 기존 파일을 덮어쓰지 않는다.</summary>
        public static void CreateOrOpen()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

            var generatorObject = new GameObject("DungeonGenerator");
            var generator = generatorObject.AddComponent<DungeonGenerator>();

            // 포털과 열쇠는 Player 태그에 반응한다.
            var walkerObject = new GameObject("PrototypeWalker") { tag = DungeonPortal.TravellerTag };
            var controller = walkerObject.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = new Vector3(0f, 1f, 0f);
            var walker = walkerObject.AddComponent<DungeonPrototypeWalker>();
            walker.Generator = generator;

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(walkerObject.transform, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 300f;
            cameraObject.AddComponent<AudioListener>();
            var prototypeCamera = cameraObject.AddComponent<DungeonPrototypeCamera>();
            prototypeCamera.Target = walkerObject.transform;
            prototypeCamera.Generator = generator;

            EditorSceneManager.SaveScene(scene, ScenePath);
        }
    }
}
