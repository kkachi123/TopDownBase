using TopDownBase.Dungeon.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TopDownBase.Dungeon.Editor
{
    /// <summary>
    /// 던전 생성 프로토타입 씬을 만들거나 여는 메뉴.
    /// 씬에는 생성기·임시 이동체·카메라·조명·후처리만 저장하고, 던전 형상은 Play 시 생성한다.
    /// </summary>
    public static class DungeonPrototypeSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/DungeonPrototype.unity";

        /// <summary>기본 환경 세팅. 다른 환경은 같은 폴더에 있다.</summary>
        public const string ForestEnvironmentPath = "Assets/Game/Environment/Dungeon/Forest.asset";
        private const string NatureVolumeProfilePath = "Assets/Game/Environment/PolyStyle/Settings/NatureVolumeProfile.asset";

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

            var generator = new GameObject("DungeonGenerator").AddComponent<DungeonGenerator>();
            generator.RoomCount = 12;
            generator.BossSceneName = "SampleScene";
            ApplyDefaultEnvironment(generator);
            Camera camera = CreateRig(generator);
            AddPostProcessing(camera);

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        /// <summary>기본 환경(숲) 세팅 에셋을 생성기에 연결한다.</summary>
        public static void ApplyDefaultEnvironment(DungeonGenerator generator)
        {
            generator.Environment = AssetDatabase.LoadAssetAtPath<DungeonEnvironmentSettings>(ForestEnvironmentPath);
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

        /// <summary>
        /// 확인용 씬의 공통 구성: 조명, Player 태그가 붙은 임시 이동체, 따라가는 카메라.
        /// 이동체와 카메라를 주어진 맵 생성 컴포넌트에 연결하고, 만든 카메라를 돌려준다.
        /// </summary>
        public static Camera CreateRig(DungeonMapSource source)
        {
            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            // 따라가는 카메라가 대상에서 80유닛 떨어져 있어 화면이 그림자 거리(URP 에셋 50) 밖이다.
            // 켜 두면 그림자는 보이지 않고 그리는 비용만 들어 끈다(자연환경 과정 문서의 그림자 설정 측정).
            light.shadows = LightShadows.None;
            lightObject.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

            // 포털과 열쇠는 Player 태그에 반응한다.
            var walkerObject = new GameObject("PrototypeWalker") { tag = DungeonPortal.TravellerTag };
            var controller = walkerObject.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = new Vector3(0f, 1f, 0f);
            var walker = walkerObject.AddComponent<DungeonPrototypeWalker>();
            walker.Generator = source;

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
            prototypeCamera.Generator = source;
            return camera;
        }
    }
}
