using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class ViewSystemEditHelper : EditorWindow
{
    string defaultAnimationFilesFoldPath = "";
    string newAnimationFileName = "";
    string targetDirectory = "";
    string[] fileEntries;
    // string[] newFileGUID;
    // string[] oldFileGUID;
    Dictionary<string, string> newFileGUID = new Dictionary<string, string>();
    Dictionary<string, string> oldFileGUID = new Dictionary<string, string>();

    [MenuItem("CloudMacaca/ViewSystem/ViewSystem Edit Helper")]
    public static void ShowWindow()
    {
        GetWindow<ViewSystemEditHelper>("ViewSystem Edit Helper");
    }


    void OnGUI()
    {
        // 自動生成 View Element 動畫檔案
        GUILayout.Label("Generate View Element Animation Files", EditorStyles.boldLabel);
        newAnimationFileName = EditorGUILayout.TextField("Animation Name", newAnimationFileName);
        if (GUILayout.Button("Create Animation File"))
        {

            // -------------------- 第一步驟 創造動畫檔 -------------------- //

            // 尋找預設動畫檔的資料夾
            var findfoldPath = Directory.GetDirectories(Application.dataPath, "_DefaultAnimationFiles_ViewSystem", SearchOption.AllDirectories);
            if (findfoldPath.Count() == 0)
            {
                Debug.LogWarning("請創建「_DefaultAnimationFiles_ViewSystem」資料夾，並放置預設的 Animation Clip 及 Controller");
                return;
            }
            else if (findfoldPath.Count() > 1)
            {
                Debug.LogWarning("偵測到兩個以上的「_DefaultAnimationFiles_ViewSystem」資料夾：");
                for (int i = 0; i < findfoldPath.Length; i++)
                {
                    Debug.LogWarning(findfoldPath[i]);
                }
                return;
            }
            else
            {
                // 設定找到的預設資料夾，並置換為相對路徑，以免後續步驟出錯
                defaultAnimationFilesFoldPath = findfoldPath[0];
                string splitKey = Path.AltDirectorySeparatorChar + "Assets";
                string[] splitedString = System.Text.RegularExpressions.Regex.Split(defaultAnimationFilesFoldPath, splitKey, RegexOptions.IgnoreCase);
                Debug.Log("Find Default Animation Path：" + defaultAnimationFilesFoldPath);
                defaultAnimationFilesFoldPath = "Assets" + splitedString[1];
                Debug.Log("Cut into relative Path：" + defaultAnimationFilesFoldPath);
            }

            // 檢查有無輸入名稱
            if (string.IsNullOrEmpty(newAnimationFileName))
            {
                Debug.LogWarning("未輸入動畫名稱");
                return;
            }

            // 複製一份預設檔案至 newAnimationFileName 資料夾
            targetDirectory = "Assets/0_Game/UI/Animation/" + newAnimationFileName;
            FileUtil.CopyFileOrDirectory(defaultAnimationFilesFoldPath, targetDirectory);

            // 將所有動畫檔案改名
            fileEntries = Directory.GetFiles(targetDirectory);
            string newNamedFilePath;
            foreach (string filePath in fileEntries)
            {
                if (filePath.Contains(".controller"))
                {
                    newNamedFilePath = targetDirectory + Path.DirectorySeparatorChar + newAnimationFileName + '.' + filePath.Split('.').Last();
                }
                else
                {
                    newNamedFilePath = targetDirectory + Path.DirectorySeparatorChar + newAnimationFileName + '_' + filePath.Split('_').Last();
                }
                System.IO.File.Move(filePath, newNamedFilePath);
            }

            // 匯入資源，產生新的 GUID
            AssetDatabase.Refresh();
            Debug.Log("Create 「" + newAnimationFileName + "」 Animation File Completed");


            // -------------------- 第二步驟 替換新 Controller 的 Motion Animation Clip -------------------- //

            if (string.IsNullOrEmpty(newAnimationFileName))
            {
                Debug.LogWarning("未輸入動畫名稱");
            }
            else
            {
                newFileGUID.Clear();
                oldFileGUID.Clear();

                // 抓取原始檔案的 GUID
                fileEntries = Directory.GetFiles(defaultAnimationFilesFoldPath);
                foreach (string filePath in fileEntries)
                {
                    if (filePath.Contains(".controller"))
                    {
                        continue;
                    }
                    string key = filePath.Split('_').Last();
                    oldFileGUID.Add(key, AssetDatabase.AssetPathToGUID(filePath));
                    Debug.Log("Get：" + filePath + "|| Key=" + key + "  ||  oldGUID=" + oldFileGUID[key]);
                }

                // 抓取新生成的 GUID
                fileEntries = Directory.GetFiles(targetDirectory);
                foreach (string filePath in fileEntries)
                {
                    if (filePath.Contains(".controller"))
                    {
                        continue;
                    }
                    string key = filePath.Split('_').Last();
                    newFileGUID.Add(key, AssetDatabase.AssetPathToGUID(filePath));
                    Debug.Log("Get：" + filePath + "|| Key=" + key + "  ||  newGUID=" + newFileGUID[key]);
                }

                // 尋找 Controller 檔案，讀取文字
                AssetDatabase.StartAssetEditing();
                var animationControllerAsset = Directory.GetFiles(Application.dataPath + "/0_Game/UI/Animation/" + newAnimationFileName)
                    .FirstOrDefault(s => s.EndsWith(".controller"));
                var content = File.ReadAllText(animationControllerAsset);
                Debug.Log("Find: " + Application.dataPath + "/0_Game/UI/Animation/" + newAnimationFileName + "/" + newAnimationFileName + ".controller");

                // 置換 GUID
                foreach (var item in oldFileGUID)
                {
                    if (string.IsNullOrEmpty(item.Value))
                    {
                        continue;
                    }
                    content = content.Replace(item.Value, newFileGUID[item.Key]);
                    Debug.Log("Replace：" + item + " To：" + newFileGUID[item.Key]);
                }
                Debug.Log("content" + content);

                // 存檔
                File.WriteAllText(animationControllerAsset, content);
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
                Debug.Log("Replace 「" + newAnimationFileName + "」 Animation Controller Motion Clip Completed");

                // Highlight Animator
                string animatorPath = "Assets/0_Game/UI/Animation/" + newAnimationFileName + "/" + newAnimationFileName + ".controller";
                UnityEditor.EditorGUIUtility.PingObject((Object)AssetDatabase.LoadAssetAtPath(animatorPath, typeof(AnimatorController)));
            }
        }



    }
}