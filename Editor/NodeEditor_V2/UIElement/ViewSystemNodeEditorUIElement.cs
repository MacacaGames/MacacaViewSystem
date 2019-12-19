using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;


public class ViewSystemNodeEditorUIElement : EditorWindow
{
    [MenuItem("Window/UIElements/ViewSystemNodeEditorUIElement")]
    public static void ShowExample()
    {
        ViewSystemNodeEditorUIElement wnd = GetWindow<ViewSystemNodeEditorUIElement>();
        wnd.titleContent = new GUIContent("ViewSystemNodeEditorUIElement");
    }

    public void OnEnable()
    {
        // Each editor window contains a root VisualElement object
        VisualElement root = rootVisualElement;

        // // VisualElements objects can contain other VisualElement following a tree hierarchy.
        // VisualElement label = new Label("Hello World! From C#");
        // root.Add(label);

        // Import UXML
        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/ViewSystem/Editor/NodeEditor_V2/UIElement/ViewSystemNodeEditorUIElement.uxml");
        VisualElement labelFromUXML = visualTree.CloneTree();
        labelFromUXML.style.flexGrow = 1;


        labelFromUXML.Q("toolbarRoot").Add(new IMGUIContainer(DrawToolbar));
        root.Add(labelFromUXML);



        // A stylesheet can be added to a VisualElement.
        // The style will be applied to the VisualElement and all of its children.
        var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/ViewSystem/Editor/NodeEditor_V2/UIElement/ViewSystemNodeEditorUIElement.uss");
        VisualElement labelWithStyle = new Label("Hello World! With Style");
        labelWithStyle.styleSheets.Add(styleSheet);
        // root.Add(labelWithStyle);
    }

    void DrawToolbar()
    {

    }
}