// using UnityEngine;
// using UnityEditor;
// using UnityEditor.SceneManagement;
// using UnityEngine.SceneManagement;

// // From https://gist.github.com/JohannesDeml/5802473b569718c9c86de906b7039aec
// // Original https://gist.github.com/ulrikdamm/338392c3b0900de225ec6dd10864cab4
// // Adds a "Edit Prefab" option in the Assets menu (or right clicking an asset in the project browser).
// // This opens an empty scene with your prefab where you can edit it.
// // Put this script in your project as Assets/Editor/EditPrefab.cs


// public class EditPrefabInScene
// {
// 	private static Object GetPrefab(Object selection)
// 	{
// 		if (selection == null)
// 		{
// 			return null;
// 		}
// 		var prefabInstanceStatus = PrefabUtility.GetPrefabInstanceStatus(selection);
// 		if (prefabInstanceStatus == PrefabInstanceStatus.Connected)
// 		{
// 			var prefab = PrefabUtility.GetPrefabParent(selection);
// 			return prefab;
// 		}

// 		if (prefabType == PrefabType.Prefab)
// 		{
// 			return selection;
// 		}

// 		return null;
// 	}

// 	[MenuItem("Assets/Edit prefab")]
// 	public static void EditPrefab()
// 	{
// 		var prefab = GetPrefab(Selection.activeObject);

// 		EditorSceneManager.SaveOpenScenes();
// 		var currentScenes = new SceneSetupWrapper();
// 		currentScenes.TakeSnapshot();

// 		var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
// 		var instance = (GameObject) PrefabUtility.InstantiatePrefab(prefab, scene);
// 		Selection.activeObject = instance;
// 		SceneView.lastActiveSceneView.FrameSelected();

// 		var returnHandler = new ReturnToSceneGUI();
// 		returnHandler.PreviousSceneSetup = currentScenes;
// 		returnHandler.PrefabInstance = instance;
// 	}

// 	[MenuItem("Assets/Edit prefab", true)]
// 	public static bool EditPrefabValidator()
// 	{
// 		var prefab = GetPrefab(Selection.activeObject);
// 		return prefab != null;
// 	}
// }

// public sealed class SceneSetupWrapper
// {
// 	private string[] scenePaths;
// 	private bool[] scenesLoaded;
// 	private int activeSceneIndex;

// 	public void TakeSnapshot()
// 	{
// 		int sceneCount = SceneManager.sceneCount;
// 		scenePaths = new string[sceneCount];
// 		scenesLoaded = new bool[sceneCount];
// 		var activeScene = SceneManager.GetActiveScene();
// 		for (int i = 0; i < sceneCount; i++)
// 		{
// 			var scene = SceneManager.GetSceneAt(i);
// 			scenePaths[i] = scene.path;
// 			scenesLoaded[i] = scene.isLoaded;
// 			if (scene == activeScene)
// 			{
// 				activeSceneIndex = i;
// 			}
// 		}
// 	}

// 	public void OpenSetup()
// 	{
// 		if (scenePaths.Length == 0)
// 		{
// 			ViewSystemLog.LogError("Can't open scene setup, no data stored.");
// 			return;
// 		}
// 		try
// 		{
// 			bool canceled = false;
// 			int sceneCount = scenePaths.Length;
// 			canceled = EditorUtility.DisplayCancelableProgressBar("Loading scenes",
// 					"Loading scenes from Setup", (float)0 / sceneCount);

// 			Scene activeScene = EditorSceneManager.OpenScene(scenePaths[0]);

// 			if (canceled)
// 			{
// 				EditorUtility.ClearProgressBar();
// 				return;
// 			}
// 			for (int i = 1; i < sceneCount; i++)
// 			{
// 				canceled = EditorUtility.DisplayCancelableProgressBar("Loading scenes",
// 					"Loading scenes from Setup", (float)i / sceneCount);
// 				if (canceled)
// 				{
// 					EditorUtility.ClearProgressBar();
// 					return;
// 				}
// 				var openSceneMode = scenesLoaded[i] ? OpenSceneMode.Additive : OpenSceneMode.AdditiveWithoutLoading;
// 				var scene = EditorSceneManager.OpenScene(scenePaths[i], openSceneMode);
// 				if (activeSceneIndex == i)
// 				{
// 					activeScene = scene;
// 				}
// 			}
// 			SceneManager.SetActiveScene(activeScene);
// 		}
// 		finally
// 		{
// 			EditorUtility.ClearProgressBar();
// 		}
// 	}
// }

// public sealed class ReturnToSceneGUI
// {
// 	public SceneSetupWrapper PreviousSceneSetup;
// 	public GameObject PrefabInstance;
// 	private GUIStyle style;

// 	public ReturnToSceneGUI()
// 	{
// 		SceneView.onSceneGUIDelegate += RenderSceneGUI;
// 		style = new GUIStyle();
// 		style.margin = new RectOffset(10, 10, 10, 10);
// 	}

// 	public void RenderSceneGUI(SceneView sceneview)
// 	{
// 		Handles.BeginGUI();
// 		GUILayout.BeginArea(new Rect(20, 20, 180, 300), style);
// 		var rect = EditorGUILayout.BeginVertical();
// 		GUI.Box(rect, GUIContent.none);

// 		if (GUILayout.Button("Apply and return", new GUILayoutOption[0]))
// 		{
// 			PrefabUtility.ReplacePrefab(PrefabInstance, PrefabUtility.GetPrefabParent(PrefabInstance), ReplacePrefabOptions.ConnectToPrefab);
// 			SceneView.onSceneGUIDelegate -= RenderSceneGUI;
// 			PreviousSceneSetup.OpenSetup();
// 		}

// 		if (GUILayout.Button("Discard changes", new GUILayoutOption[0]))
// 		{
// 			SceneView.onSceneGUIDelegate -= RenderSceneGUI;
// 			PreviousSceneSetup.OpenSetup();
// 		}

// 		EditorGUILayout.EndVertical();
// 		GUILayout.EndArea();
// 		Handles.EndGUI();
// 	}
// }