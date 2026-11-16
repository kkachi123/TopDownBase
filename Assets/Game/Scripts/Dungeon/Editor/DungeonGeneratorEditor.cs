using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TopDownBase.Dungeon.Editor
{
    /// <summary>
    /// DungeonGenerator 인스펙터. 기본 필드 아래에 맵 생성 버튼과 현재 상태 안내를 붙인다.
    /// Play를 누르지 않고도 편집 모드에서 맵을 만들고 지울 수 있다.
    /// </summary>
    [CustomEditor(typeof(DungeonGenerator))]
    public class DungeonGeneratorEditor : UnityEditor.Editor
    {
        private const string StyleSheetPath = "Assets/Game/Scripts/Dungeon/Editor/DungeonGeneratorEditor.uss";

        private HelpBox _status;
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
            DungeonGenerator generator = Generator;
            action(generator);

            if (!Application.isPlaying)
            {
                // 생성물과 바뀐 시드가 씬 저장 대상임을 에디터에 알린다. 되돌리기(Undo)는 지원하지 않는다.
                EditorUtility.SetDirty(generator);
                EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
            }

            serializedObject.Update();
            Refresh();
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
            else if (!string.IsNullOrEmpty(generator.LastReport))
            {
                _status.text = generator.LastReport;
                bool unmet = generator.Layout != null && !generator.Layout.MeetsRequirements;
                _status.messageType = unmet ? HelpBoxMessageType.Warning : HelpBoxMessageType.Info;
            }
            else
            {
                _status.text = $"씬에 저장된 맵이 있습니다. 방 {generator.Rooms.Count}개, 시드 {generator.Seed}.";
                _status.messageType = HelpBoxMessageType.Info;
            }

            if (!generator.GenerateOnAwake)
            {
                _playBehaviour.text = hasMap
                    ? "Play 시: 씬에 있는 맵을 그대로 사용합니다. NavMesh만 다시 빌드합니다."
                    : "Play 시: 맵을 생성하지 않습니다. 먼저 위 버튼으로 맵을 만들어 두세요.";
            }
            else if (generator.RandomizeSeedOnAwake)
            {
                _playBehaviour.text = "Play 시: 새 시드로 다시 생성합니다. 지금 보이는 맵은 대체됩니다.";
            }
            else
            {
                _playBehaviour.text = $"Play 시: 시드 {generator.Seed}로 다시 생성합니다. 같은 시드는 같은 맵을 만듭니다.";
            }
        }
    }
}
