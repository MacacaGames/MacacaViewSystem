using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Text;
using CloudMacaca.ViewSystem;

public class ViewSystemEditor : Editor
{

    [MenuItem("CloudMacaca/ViewSystem/Bake ViewPage and ViewState to script", false, 0)]
    static void BakeAllViewPageName()
    {
        BuildScriptWithSelectedTerms();
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
    static void BuildScriptWithSelectedTerms()
    {
        EditorApplication.update -= BuildScriptWithSelectedTerms;
        var sb = new StringBuilder();
        sb.AppendLine("using UnityEngine;");
        sb.AppendLine();
        sb.AppendLine("namespace CloudMacaca.ViewSystem");
        sb.AppendLine("{");
        sb.AppendLine("	public static class ViewSystemScriptable");
        sb.AppendLine("	{");
        sb.AppendLine();

        BuildScriptWithViewPages(sb);
        BuildScriptWithViewStates(sb);
        sb.AppendLine();
        sb.AppendLine("	}");

     


        sb.AppendLine("}");


        string ScriptFile = GetPathToGeneratedScriptLocalization();

        var filePath = Application.dataPath + ScriptFile.Substring("Assets".Length);

        System.IO.File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);

        AssetDatabase.ImportAsset(ScriptFile);
    }

    static void BuildScriptWithViewPages(StringBuilder sb)
    {
        var vps = viewController.viewPage;
        if (vps == null)
        {
            return;
        }
        if (vps.Count == 0)
        {
            return;
        }
        sb.AppendLine("		public static class ViewPages");
        sb.AppendLine("		{");
        foreach (var item in vps)
        {
            sb.AppendLine("		    public static string  " + item.name + " = \"" + item.name + "\";");
            sb.AppendLine();
        }
        sb.AppendLine("		}");
    }
    static void BuildScriptWithViewStates(StringBuilder sb)
    {
        var vss = viewController.viewStates;
        if (vss == null)
        {
            return;
        }
        if (vss.Count == 0)
        {
            return;
        }
        sb.AppendLine("		public static class ViewStates");
        sb.AppendLine("		{");
        foreach (var item in vss)
        {
            sb.AppendLine("		    public static string  " + item.name + " = \"" + item.name + "\";");
            sb.AppendLine();
        }
        sb.AppendLine("		}");
    }

    static string GetPathToGeneratedScriptLocalization()
    {
        string[] assets = AssetDatabase.FindAssets("ViewSystemScriptable");
        if (assets.Length > 0)
        {
            try
            {
                string FilePath = AssetDatabase.GUIDToAssetPath(assets[0]);
                return FilePath;
            }
            catch (System.Exception)
            { }
        }

        return "Assets/ViewSystemScriptable.cs";
    }
}
