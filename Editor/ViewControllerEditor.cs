// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using UnityEditor;
// using UnityEditorInternal;
// using CloudMacaca;

// [CustomEditor(typeof(ViewController))]

// public class ViewControllerEditor : Editor {

// 	private ViewController viewController = null;
//     private SerializedProperty m_AnimTriggerProperty;
// 	public ReorderableList list = null;

//     void OnEnable()
//     {
//         viewController = (ViewController)target;
// 		list =  new ReorderableList(viewController.viewPages,typeof(List<ViewPage>),true,false,true,true);
// 		list.drawElementCallback = ReorderableListDrawItem;
// 		list.drawHeaderCallback = (Rect rect) => {  
// 			rect.width = 60;
// 			rect.x += 0;
// 			// if (GUI.Button(rect, ReorderableList.defaultBehaviours.iconToolbarPlus, ReorderableList.defaultBehaviours.preButton))
// 			// {
// 			// 	ReorderableList.defaultBehaviours.DoAddButton(list);
// 			// }
			
// 		};
// 		//list.headerHeight = EditorGUIUtility.singleLineHeight * 2;
		
// 		list.elementHeight = EditorGUIUtility.singleLineHeight*5;	

//     }
// 	void ReorderableListDrawItem (Rect rect, int index, bool isActive, bool isFocused)
// 	{
// 		var spacing = 10f;
	
// 		//Get one item
// 		var item = viewController.viewPages[index];

// 		Rect arect = rect;

// 		arect.height = EditorGUIUtility.singleLineHeight;
// 		arect.width = rect.width*0.25f;
// 		arect.x = rect.x;
// 		arect.y = rect.y;
// 		//EditorGUI.PropertyField(arect, item.FindPropertyRelative("Name"), GUIContent.none);
// 		item.name = EditorGUI.TextField(arect,"名稱",item.name);

// 		arect.width = rect.width*0.25f;
// 		arect.x += rect.width * 0.25f;
//  		item.viewPageType = (ViewPage.ViewPageType)EditorGUI.EnumPopup(arect,"類型",item.viewPageType);



// 		ReorderableList childList = null;
// 		childList =  new ReorderableList(viewController.viewPages,typeof(List<ViewPageItem>),true,false,true,true);
// 		childList.drawElementCallback = (Rect cRect, int cIndex, bool cIsActive, bool cIsFocused) => {


// 		};
// 		childList.DoLayoutList();
// 		//EditorGUI.LabelField(rect, planeList[index].name);
// 	}
// 	public override void OnInspectorGUI()
//     {
// 		list.DoLayoutList();
// 	}
// }
