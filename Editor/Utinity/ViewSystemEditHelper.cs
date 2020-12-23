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
    enum ElementType
    {
        Normal,
        Button,
        Toggle
    }
    ElementType elementType;

    Dictionary<string, string> newFileGUID = new Dictionary<string, string>();
    Dictionary<string, string> oldFileGUID = new Dictionary<string, string>();

    [MenuItem("MacacaGames/ViewSystem/ViewSystem Edit Helper")]
    public static void ShowWindow()
    {
        GetWindow<ViewSystemEditHelper>("ViewSystem Edit Helper");
    }
    string ViewSystemEditHelperAnimationFilePath = "_Game/UI/Animation/";
    // void SetAnimationFilePath(){
    //     EditorPrefs.SetString("ViewSystemEditHelperAnimationFilePath", ViewSystemEditHelperAnimationFilePath);
    // }


    void OnGUI()
    {
        // 自動生成 View Element 動畫檔案
        GUILayout.Label("Animation File Path", EditorStyles.boldLabel);
        ViewSystemEditHelperAnimationFilePath = EditorGUILayout.TextField("/", ViewSystemEditHelperAnimationFilePath);

        GUILayout.Label("Generate View Element Animation Files", EditorStyles.boldLabel);
        elementType = (ElementType)EditorGUILayout.EnumPopup("Element Type", elementType);
        newAnimationFileName = EditorGUILayout.TextField("Animation Name", newAnimationFileName);
        if (GUILayout.Button("Create Animation File"))
        {
            // 判斷物件類型
            string DefaultFoldName = "";
            switch (elementType)
            {
                case ElementType.Normal:
                    DefaultFoldName = "_DefaultAnimationFiles_ViewSystem";
                    break;

                case ElementType.Button:
                    DefaultFoldName = "_DefaultBtnAnimationFiles_ViewSystem";
                    break;

                case ElementType.Toggle:
                    DefaultFoldName = "_DefaultToggleAnimationFiles_ViewSystem";
                    break;
            }


            // -------------------- 第一步驟 創造動畫檔 -------------------- //

            // 尋找預設動畫檔的資料夾
            var findfoldPath = Directory.GetDirectories(Application.dataPath, DefaultFoldName, SearchOption.AllDirectories);
            if (findfoldPath.Count() == 0)
            {
                ViewSystemLog.LogWarning("請創建「" + DefaultFoldName + "」資料夾，並放置預設的 Animation Clip 及 Controller");
                return;
            }
            else if (findfoldPath.Count() > 1)
            {
                ViewSystemLog.LogWarning("偵測到兩個以上的「" + DefaultFoldName + "」資料夾：");
                for (int i = 0; i < findfoldPath.Length; i++)
                {
                    ViewSystemLog.LogWarning(findfoldPath[i]);
                }
                return;
            }
            else
            {
                // 設定找到的預設資料夾，並置換為相對路徑，以免後續步驟出錯
                defaultAnimationFilesFoldPath = findfoldPath[0];
                string splitKey = Path.AltDirectorySeparatorChar + "Assets";
                string[] splitedString = System.Text.RegularExpressions.Regex.Split(defaultAnimationFilesFoldPath, splitKey, RegexOptions.IgnoreCase);
                ViewSystemLog.Log("Find Default Animation Path：" + defaultAnimationFilesFoldPath);
                defaultAnimationFilesFoldPath = "Assets" + splitedString[1];
                ViewSystemLog.Log("Cut into relative Path：" + defaultAnimationFilesFoldPath);
            }

            // 檢查有無輸入名稱
            if (string.IsNullOrEmpty(newAnimationFileName))
            {
                ViewSystemLog.LogWarning("未輸入動畫名稱");
                return;
            }

            // 複製一份預設檔案至 newAnimationFileName 資料夾
            targetDirectory = "Assets/" + ViewSystemEditHelperAnimationFilePath + newAnimationFileName;
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
            ViewSystemLog.Log("Create 「" + newAnimationFileName + "」 Animation File Completed");


            // -------------------- 第二步驟 替換新 Controller 的 Motion Animation Clip -------------------- //

            if (string.IsNullOrEmpty(newAnimationFileName))
            {
                ViewSystemLog.LogWarning("未輸入動畫名稱");
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
                    ViewSystemLog.Log("Get：" + filePath + "|| Key=" + key + "  ||  oldGUID=" + oldFileGUID[key]);
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
                    ViewSystemLog.Log("Get：" + filePath + "|| Key=" + key + "  ||  newGUID=" + newFileGUID[key]);
                }

                // 尋找 Controller 檔案，讀取文字
                AssetDatabase.StartAssetEditing();
                var animationControllerAsset = Directory.GetFiles(Application.dataPath + "/" + ViewSystemEditHelperAnimationFilePath + newAnimationFileName)
                    .FirstOrDefault(s => s.EndsWith(".controller"));
                var content = File.ReadAllText(animationControllerAsset);
                ViewSystemLog.Log("Find: " + Application.dataPath + "/" + ViewSystemEditHelperAnimationFilePath + newAnimationFileName + "/" + newAnimationFileName + ".controller");

                // 置換 GUID
                foreach (var item in oldFileGUID)
                {
                    if (string.IsNullOrEmpty(item.Value))
                    {
                        continue;
                    }
                    content = content.Replace(item.Value, newFileGUID[item.Key]);
                    ViewSystemLog.Log("Replace：" + item + " To：" + newFileGUID[item.Key]);
                }
                ViewSystemLog.Log("content" + content);

                // 存檔
                File.WriteAllText(animationControllerAsset, content);
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
                ViewSystemLog.Log("Replace 「" + newAnimationFileName + "」 Animation Controller Motion Clip Completed");

                // Highlight Animator
                string animatorPath = "Assets/" + ViewSystemEditHelperAnimationFilePath + newAnimationFileName + "/" + newAnimationFileName + ".controller";
                UnityEditor.EditorGUIUtility.PingObject((Object)AssetDatabase.LoadAssetAtPath(animatorPath, typeof(AnimatorController)));
            }
        }



    }
}