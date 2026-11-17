using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TopDownBase.Dungeon.Experiments.Editor
{
    /// <summary>
    /// 경계 표현 테스트 빌더의 인스펙터. 기본 필드 아래에 생성 버튼과 마지막 결과를 붙인다.
    /// 생성물은 씬에 저장되지 않으므로 씬을 변경된 것으로 표시하지 않는다.
    /// </summary>
    [CustomEditor(typeof(LayoutTestBuilder), true)]
    public class LayoutTestBuilderEditor : UnityEditor.Editor
    {
        private const string StyleSheetPath = "Assets/Game/Scripts/Dungeon/Editor/DungeonGeneratorEditor.uss";

        private HelpBox _status;
        private Button _clearButton;

        private LayoutTestBuilder Builder => (LayoutTestBuilder)target;

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);
            if (styleSheet != null)
            {
                root.styleSheets.Add(styleSheet);
            }

            InspectorElement.FillDefaultInspector(root, serializedObject, this);

            var actions = new VisualElement { name = "layoutTestActions" };
            actions.AddToClassList("dungeon-actions");

            var title = new Label("배치 테스트");
            title.AddToClassList("dungeon-actions__title");
            actions.Add(title);

            var row = new VisualElement();
            row.AddToClassList("dungeon-actions__row");
            Button generateButton = CreateButton("generateButton", "현재 시드로 생성", builder => builder.Generate());
            Button randomButton = CreateButton("generateRandomButton", "무작위 시드로 생성",
                builder => builder.GenerateWithRandomSeed());
            _clearButton = CreateButton("clearButton", "지우기", builder => builder.Clear());
            row.Add(generateButton);
            row.Add(randomButton);
            row.Add(_clearButton);
            actions.Add(row);

            _status = new HelpBox(string.Empty, HelpBoxMessageType.Info);
            _status.AddToClassList("dungeon-actions__info");
            actions.Add(_status);

            var note = new HelpBox("테스트용 생성물은 씬에 저장되지 않습니다. Play를 누르거나 스크립트가 다시 컴파일되면 사라지고, " +
                                   "Play 중에는 새로 만들어집니다.", HelpBoxMessageType.None);
            note.AddToClassList("dungeon-actions__info");
            actions.Add(note);

            root.Add(actions);

            bool inScene = Builder.gameObject.scene.IsValid();
            generateButton.SetEnabled(inScene);
            randomButton.SetEnabled(inScene);

            Refresh();
            return root;
        }

        private Button CreateButton(string elementName, string text, Action<LayoutTestBuilder> action)
        {
            var button = new Button(() => Run(action)) { name = elementName, text = text };
            button.AddToClassList("dungeon-actions__button");
            return button;
        }

        private void Run(Action<LayoutTestBuilder> action)
        {
            action(Builder);

            // 무작위 시드로 생성하면 Seed 필드가 바뀐다. 인스펙터 표시만 맞춘다.
            serializedObject.Update();
            Refresh();
        }

        private void Refresh()
        {
            if (_status == null)
            {
                return;
            }

            LayoutTestBuilder builder = Builder;
            bool hasMap = builder.HasGeneratedMap;
            _clearButton.SetEnabled(hasMap);

            if (!hasMap)
            {
                _status.text = "생성된 맵이 없습니다.";
                _status.messageType = HelpBoxMessageType.Info;
                return;
            }

            _status.text = builder.LastReport;
            bool connected = builder.Map != null && builder.Map.IsFullyConnected(true, builder.AgentRadius);
            _status.messageType = connected ? HelpBoxMessageType.Info : HelpBoxMessageType.Warning;
        }
    }
}
