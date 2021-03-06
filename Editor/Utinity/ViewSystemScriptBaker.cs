﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Text;
using MacacaGames.ViewSystem;
namespace MacacaGames.ViewSystem
{
    public class ViewSystemScriptBaker : Editor
    {
        public static void BakeAllViewPageName(List<ViewPage> vps, List<ViewState> vss,List<string> bp)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine("namespace MacacaGames.ViewSystem");
            sb.AppendLine("{");
            sb.AppendLine("	public struct ViewSystemScriptable");
            sb.AppendLine("	{");
            sb.AppendLine();
            BuildBreakPoints(sb, bp);
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
        static void BuildBreakPoints(StringBuilder sb, List<string> bp)
        {
            if (bp == null)
            {
                return;
            }
            if (bp.Count == 0)
            {
                return;
            }
            sb.AppendLine("		public struct BreakPoints");
            sb.AppendLine("		{");
            foreach (var item in bp)
            {
                sb.AppendLine("		    public const string  " + item + " = \"" + item + "\";");
                sb.AppendLine();
            }
            sb.AppendLine("		}");
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

            MacacaGames.ViewSystem.VisualEditor.ViewSystemDataReaderV2.CheckAndCreateResourceFolder();
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