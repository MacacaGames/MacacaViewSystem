using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Text;
using System;
using System.IO;
using System.Linq;
using MacacaGames.ViewSystem;
namespace MacacaGames.ViewSystem
{
    public class ViewSystemScriptBaker : Editor
    {
        public static void BakeAllViewPageName(List<ViewPage> vps, List<ViewState> vss, List<string> bp)
        {
            BakeAllViewPageName(vps, vss, bp, GetPathToGeneratedScriptLocalization());
        }

        /// <summary>Bake to an explicit Assets-relative path without loading an editor window.</summary>
        public static void BakeAllViewPageName(List<ViewPage> vps, List<ViewState> vss,
            List<string> bp, string scriptAssetPath)
        {
            if (string.IsNullOrEmpty(scriptAssetPath) || !scriptAssetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !scriptAssetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Use an Assets-relative .cs output path.", nameof(scriptAssetPath));
            var filePath = Path.GetFullPath(scriptAssetPath);
            var assetsRoot = Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar;
            if (!filePath.StartsWith(assetsRoot, StringComparison.Ordinal))
                throw new ArgumentException("The output path must remain under Assets/.", nameof(scriptAssetPath));

            var source = GenerateSource(vps, vss, bp);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            File.WriteAllText(filePath, source, new UTF8Encoding(false));
            AssetDatabase.Refresh();
        }

        /// <summary>Generate validated source without writing assets or opening a window.</summary>
        public static string GenerateSource(List<ViewPage> vps, List<ViewState> vss, List<string> bp)
        {
            ValidateNames(vps?.Select(page => page.name), "ViewPages");
            ValidateNames(vss?.Select(state => state.name), "ViewStates");
            ValidateNames(bp, "BreakPoints");
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


            return sb.ToString();
        }

        static readonly HashSet<string> Keywords = new HashSet<string>(
            ("abstract as base bool break byte case catch char checked class const continue decimal default " +
             "delegate do double else enum event explicit extern false finally fixed float for foreach goto " +
             "if implicit in int interface internal is lock long namespace new null object operator out override " +
             "params private protected public readonly ref return sbyte sealed short sizeof stackalloc static " +
             "string struct switch this throw true try typeof uint ulong unchecked unsafe ushort using virtual " +
             "void volatile while __arglist __makeref __reftype __refvalue").Split(' '), StringComparer.Ordinal);

        internal static bool IsValidName(string name, string container)
        {
            if (string.IsNullOrEmpty(name)) return false;
            bool escaped = name[0] == '@';
            var identifier = escaped ? name.Substring(1) : name;
            if (identifier.Length == 0 || identifier == container || (!escaped && Keywords.Contains(identifier)))
                return false;
            if (identifier[0] != '_' && !char.IsLetter(identifier[0])) return false;
            return identifier.Skip(1).All(character => character == '_' || char.IsLetterOrDigit(character));
        }

        static void ValidateNames(IEnumerable<string> names, string container)
        {
            if (names == null) return;
            var identifiers = new HashSet<string>(StringComparer.Ordinal);
            foreach (var name in names)
            {
                if (!IsValidName(name, container))
                    throw new ArgumentException($"'{name}' cannot be baked as a C# name in {container}.");
                if (!identifiers.Add(name.TrimStart('@')))
                    throw new ArgumentException($"Duplicate C# name '{name}' in {container}.");
            }
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

            MacacaGames.ViewSystem.VisualEditor.ViewSystemDataReader.CheckAndCreateResourceFolder();
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