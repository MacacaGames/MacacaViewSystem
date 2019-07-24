using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using CloudMacaca.ViewSystem;
using System.Linq;
using Unity.Linq;

public class ViewSwitcherEditor : EditorWindow
{
    static EditorWindow _editor;

    [MenuItem("CloudMacaca/ViewSystem/ViewSwitcher")]
    public static void Init()
    {
        EditorWindow window = GetWindow<ViewSwitcherEditor>();

    }
    List<bool> firstPageSetting = new List<bool>();
    void OnFocus()
    {
        try
        {
            var a = (ViewElementPool)FindObjectOfType(typeof(ViewElementPool));
            poolTransform = a.transform;
        }
        catch
        {

        }


    }
    public static void Normalized()
    {
        if (poolTransform == null)
        {
            Debug.LogError("Please Set ViewElementPool");
            return;
        }
        ViewElement[] allElements = FindObjectsOfType<ViewElement>();
        foreach (ViewElement viewElement in allElements)
        {
            var rt = viewElement.GetComponent<RectTransform>();
            rt.SetParent(poolTransform);
        }
    }

    static ViewController _viewController;
    static ViewController viewController
    {
        get
        {
            if (_viewController == null)
                _viewController = FindObjectOfType<ViewController>();
            return _viewController;
        }
    }

    static ViewPage currentViewPage;

    Vector2 scroll = Vector2.zero;
    static Transform poolTransform;
    private void OnGUI()
    {
        poolTransform = (Transform)EditorGUILayout.ObjectField(poolTransform, typeof(Transform), true);
        EditorGUILayout.BeginHorizontal();
        // if (GUILayout.Button("Set to Init or Normized"))
        // {
        //     SetupToInitPage();
        // }
        if (GUILayout.Button("Normized Only"))
        {
            Normalized();
        }
        
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);


        List<ViewPage> viewPages = viewController.viewPage;

        scroll = EditorGUILayout.BeginScrollView(scroll);

        foreach (ViewPage viewPage in viewPages)
        {
            bool isCurrentViewPage = currentViewPage == viewPage;

            EditorGUILayout.BeginHorizontal();

            if (isCurrentViewPage)
            {
                GUI.backgroundColor = Color.grey;
            }
            if (GUILayout.Button(viewPage.name))
            {
                if (!isCurrentViewPage)
                    OnChangePage(viewPage);
            }
            if (isCurrentViewPage)
            {
                GUI.backgroundColor = Color.white;
            }
            if (GUILayout.Button("Highligh Object", GUILayout.Width(100)))
            {

                HighlightObject(viewPage);
            }
            EditorGUILayout.EndHorizontal();

        }

        EditorGUILayout.EndScrollView();
    }
    static void HighlightObject(ViewPage viewPage)
    {
        foreach (var item in viewPage.viewPageItems)
        {
            EditorGUIUtility.PingObject(item.viewElement);
        }
    }
    static void OnChangePage(ViewPage viewPage)
    {
        if (poolTransform == null)
        {
            Debug.LogError("Please Set ViewElementPool");
            return;
        }

        Normalized();

        currentViewPage = viewPage;


        //打開所有相關 ViewElements
        ViewState viewPagePresetTemp;
        List<ViewPageItem> viewItemForNextPage = new List<ViewPageItem>();

        //從 ViewPagePreset 尋找 (ViewState)
        if (!string.IsNullOrEmpty(viewPage.viewState))
        {
            viewPagePresetTemp = viewController.viewStates.SingleOrDefault(m => m.name == viewPage.viewState);
            if (viewPagePresetTemp != null)
            {
                viewItemForNextPage.AddRange(viewPagePresetTemp.viewPageItems);
            }
        }

        //從 ViewPage 尋找
        viewItemForNextPage.AddRange(viewPage.viewPageItems);


        //打開相對應物件
        foreach (ViewPageItem item in viewItemForNextPage)
        {
            item.viewElement.gameObject.SetActive(true);
            var rectTransform = item.viewElement.GetComponent<RectTransform>();
            rectTransform.SetParent(item.parent, true);
            rectTransform.anchoredPosition3D = Vector3.zero;
            rectTransform.localScale = Vector3.one;

            //item.viewElement.SampleToLoopState();
            if (item.viewElement.transition != ViewElement.TransitionType.Animator)
                continue;


            Animator animator = item.viewElement.animator;
            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            foreach (AnimationClip clip in clips)
            {
                if (clip.name.ToLower().Contains(item.viewElement.AnimationStateName_Loop.ToLower()))
                {
                    clip.SampleAnimation(animator.gameObject, 0);
                }
            }
        }
    }
    void OnDestroy()
    {
        //SetupToInitPage();
    }

   
}