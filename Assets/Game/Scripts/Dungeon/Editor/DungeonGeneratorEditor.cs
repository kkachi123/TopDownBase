using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TopDownBase.Dungeon.Editor
{
    /// <summary>
    /// DungeonGenerator 인스펙터. 기본 필드 아래에 맵 생성 버튼과 현재 상태 안내를 붙인다.
    /// Play를 누르지 않고도 편집 모드에서 맵을 만들고 지울 수 있다.
    /// 생성물은 씬에 저장되지 않으므로 씬을 변경된 것으로 표시하지 않는다.
    /// </summary>
    [CustomEditor(typeof(DungeonGenerator))]
    public class DungeonGeneratorEditor : UnityEditor.Editor
    {
        private const string StyleSheetPath = "Assets/Game/Scripts/Dungeon/Editor/DungeonGeneratorEditor.uss";

        private HelpBox _status;
        private HelpBox _portal;
        private HelpBox _playBehaviour;
        private Button _clearButton;

        private DungeonGenerator Generator => (DungeonGenerator)target;

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);
            if (styleSheet != null)
            {
                root.styleSheets.Add(styleSheet);
            }

            InspectorElement.FillDefaultInspector(root, serializedObject, this);

            var actions = new VisualElement { name = "dungeonActions" };
            actions.AddToClassList("dungeon-actions");

            var title = new Label("맵 생성");
            title.AddToClassList("dungeon-actions__title");
            actions.Add(title);

            var row = new VisualElement();
            row.AddToClassList("dungeon-actions__row");
            Button generateButton = CreateButton("generateButton", "현재 시드로 생성", generator => generator.Generate());
            Button randomButton = CreateButton("generateRandomButton", "무작위 시드로 생성",
                generator => generator.GenerateWithRandomSeed());
            _clearButton = CreateButton("clearButton", "지우기", generator => generator.Clear());
            row.Add(generateButton);
            row.Add(randomButton);
            row.Add(_clearButton);
            actions.Add(row);

            _status = new HelpBox(string.Empty, HelpBoxMessageType.Info);
            _status.AddToClassList("dungeon-actions__info");
            actions.Add(_status);

            _portal = new HelpBox(string.Empty, HelpBoxMessageType.Info);
            _portal.AddToClassList("dungeon-actions__info");
            actions.Add(_portal);

            _playBehaviour = new HelpBox(string.Empty, HelpBoxMessageType.None);
            _playBehaviour.AddToClassList("dungeon-actions__info");
            actions.Add(_playBehaviour);

            root.Add(actions);

            // 프리팹 에셋에는 생성물을 넣을 씬이 없으므로 씬에 놓인 오브젝트에서만 생성한다.
            bool inScene = Generator.gameObject.scene.IsValid();
            generateButton.SetEnabled(inScene);
            randomButton.SetEnabled(inScene);

            // GenerateOnAwake 등 값이 바뀌면 안내 문구를 바로 갱신한다.
            root.TrackSerializedObjectValue(serializedObject, _ => Refresh());
            Refresh();

            return root;
        }

        private Button CreateButton(string elementName, string text, Action<DungeonGenerator> action)
        {
            var button = new Button(() => Run(action)) { name = elementName, text = text };
            button.AddToClassList("dungeon-actions__button");
            return button;
        }

        private void Run(Action<DungeonGenerator> action)
        {
            action(Generator);

            // 무작위 시드로 생성하면 Seed 필드가 바뀐다. 인스펙터 표시만 맞춘다.
            // 생성물은 저장 대상이 아니므로 씬을 변경된 것으로 표시하지 않는다(바뀐 시드도 저장되지 않는다).
            serializedObject.Update();
            Refresh();
        }

        /// <summary>포털이 불러올 보스 씬과 열쇠 상태를 알려 준다. 씬 이름이 비었거나 빌드 목록에 없으면 경고한다.</summary>
        private void RefreshPortal(DungeonGenerator generator, bool hasMap)
        {
            string sceneName = generator.BossSceneName;
            if (string.IsNullOrEmpty(sceneName))
            {
                _portal.text = "Boss Scene Name이 비어 있어 포털이 씬을 불러오지 않습니다.";
                _portal.messageType = HelpBoxMessageType.Warning;
                return;
            }

            if (!IsEnabledInBuildSettings(sceneName))
            {
                _portal.text = $"'{sceneName}' 씬이 Build Settings의 씬 목록에 없거나 꺼져 있습니다. 등록해야 포털이 동작합니다.";
                _portal.messageType = HelpBoxMessageType.Warning;
                return;
            }

            string keyText;
            if (!generator.LockPortalWithKey)
            {
                keyText = "잠금 없음";
            }
            else if (!hasMap)
            {
                keyText = "아이템 방의 열쇠로 열림";
            }
            else
            {
                keyText = generator.KeyPickup != null ? "아이템 방의 열쇠로 열림" : "아이템 방이 없어 열린 상태";
            }

            _portal.text = $"보스 포털: '{sceneName}' 씬으로 이동. {keyText}.";
            _portal.messageType = HelpBoxMessageType.Info;
        }

        private static bool IsEnabledInBuildSettings(string sceneName)
        {
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled && System.IO.Path.GetFileNameWithoutExtension(scene.path) == sceneName)
                {
                    return true;
                }
            }

            return false;
        }

        private void Refresh()
        {
            if (_status == null)
            {
                return;
            }

            DungeonGenerator generator = Generator;
            bool inScene = generator.gameObject.scene.IsValid();
            bool hasMap = generator.HasGeneratedMap;
            _clearButton.SetEnabled(inScene && hasMap);

            if (!inScene)
            {
                _status.text = "씬에 놓인 오브젝트에서만 생성할 수 있습니다.";
                _status.messageType = HelpBoxMessageType.Warning;
            }
            else if (!hasMap)
            {
                _status.text = "생성된 맵이 없습니다.";
                _status.messageType = HelpBoxMessageType.Info;
            }
            else
            {
                _status.text = generator.LastReport;
                bool unmet = generator.Layout != null && !generator.Layout.MeetsRequirements;
                _status.messageType = unmet ? HelpBoxMessageType.Warning : HelpBoxMessageType.Info;
            }

            RefreshPortal(generator, hasMap);

            const string notSaved = " 편집 모드에서 만든 맵은 씬에 저장되지 않으며, Play 진입이나 스크립트 재컴파일 때 사라집니다.";
            if (!generator.GenerateOnAwake)
            {
                _playBehaviour.text = "Play 시: 맵을 생성하지 않습니다. 스크립트에서 Generate를 불러야 합니다." + notSaved;
            }
            else if (generator.RandomizeSeedOnAwake)
            {
                _playBehaviour.text = "Play 시: 새 시드로 생성합니다." + notSaved;
            }
            else
            {
                _playBehaviour.text = $"Play 시: 시드 {generator.Seed}로 생성합니다. 같은 시드는 같은 맵을 만듭니다." + notSaved;
            }
        }
    }
}
