using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CPPRogue.Tools
{
    /// <summary>
    /// 打包脚本：编辑器菜单 CRogue → 打包 Windows EXE，
    /// 或命令行 -executeMethod CPPRogue.Tools.BuildScripts.BuildWindows 调用。
    /// 产物输出到 Builds/Windows/（整个文件夹才是游戏，分享时打包整个目录）。
    /// </summary>
    public static class BuildScripts
    {
        private const string Scene = "Assets/Scenes/SampleScene.scene";
        private const string OutputPath = "Builds/Windows/CPPRogue.exe";

        [MenuItem("CRogue/打包 Windows EXE")]
        public static void BuildWindows()
        {
            BuildReport report = BuildPipeline.BuildPlayer(
                new[] { Scene },
                OutputPath,
                BuildTarget.StandaloneWindows64,
                BuildOptions.None);

            BuildSummary summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                // Burst 调试符号目录不用于分发，打包后清掉——产物文件夹即拷即发
                string outputDir = System.IO.Path.GetDirectoryName(OutputPath);
                string debugInfoDir = System.IO.Path.Combine(outputDir, "CPPRogue_BurstDebugInformation_DoNotShip");
                if (System.IO.Directory.Exists(debugInfoDir))
                    System.IO.Directory.Delete(debugInfoDir, true);

                Debug.Log($"打包成功：{OutputPath}（{summary.totalSize / (1024 * 1024)} MB）");
            }
            else
            {
                Debug.LogError($"打包失败：{summary.result}（错误 {summary.totalErrors} 个，详见 Console）");
                // 批处理模式下用非零退出码报告失败（菜单模式不退出编辑器）
                if (System.Environment.CommandLine.Contains("-batchmode"))
                    EditorApplication.Exit(1);
            }
        }
    }
}
