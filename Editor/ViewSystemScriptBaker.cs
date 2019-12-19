using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Text;
using CloudMacaca.ViewSystem;
namespace CloudMacaca.ViewSystem
{
    public class ViewSystemScriptBaker : Editor
    {
        public static void BakeAllViewPageName(List<ViewPage> vps, List<ViewState> vss)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine("namespace CloudMacaca.ViewSystem");
            sb.AppendLine("{");
            sb.AppendLine("	public struct ViewSystemScriptable");
            sb.AppendLine("	{");
            sb.AppendLine();

            BuildScriptWithViewPages(sb, vps);
            BuildScriptWithViewStates(sb, vss);
            sb.AppendLine();
            sb.AppendLine("	}");

            sb.AppendLine("}");


            string ScriptFile = GetPathToGeneratedScriptLocalization();

            var filePath = Application.dataPath + ScriptFile.Substring("Assets".Length);

            System.IO.File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);

            AssetDatabase.ImportAsset(ScriptFile);
        }

        static void BuildScriptWithViewPages(StringBuilder sb, List<ViewPage> vps)
        {
            if (vps == null)
            {
                return;
            }
            if (vps.Count == 0)
            {
                return;
            }
            sb.AppendLine("		public struct ViewPages");
            sb.AppendLine("		{");
            foreach (var item in vps)
            {
                sb.AppendLine("		    public const string  " + item.name + " = \"" + item.name + "\";");
                sb.AppendLine();
            }
            sb.AppendLine("		}");
        }
        static void BuildScriptWithViewStates(StringBuilder sb, List<ViewState> vss)
        {
            if (vss == null)
            {
                return;
            }
            if (vss.Count == 0)
            {
                return;
            }
            sb.AppendLine("		public struct ViewStates");
            sb.AppendLine("		{");
            foreach (var item in vss)
            {
                sb.AppendLine("		    public const string  " + item.name + " = \"" + item.name + "\";");
                sb.AppendLine();
            }
            sb.AppendLine("		}");
        }

        static string GetPathToGeneratedScriptLocalization()
        {

            CloudMacaca.ViewSystem.NodeEditorV2.ViewSystemDataReaderV2.CheckAndCreateResourceFolder();
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

            return "Assets/ViewSystemResources/ViewSystemScriptable.cs";
        }
    }
}