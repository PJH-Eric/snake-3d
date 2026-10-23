#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Snake3D.EditorTools
{
    /// <summary>
    /// WebGL 建置入口。
    ///   選單：Snake3D ▸ 建置 WebGL
    ///   批次：Unity -batchmode -quit -projectPath . -executeMethod Snake3D.EditorTools.BuildTool.BuildWebGL -logFile -
    /// 環境變數 GAME_SERVER_URL 會在建置時寫入 Assets/Resources/ServerUrl.txt（已被 .gitignore 忽略），
    /// 沒設定時線上模式會提示「尚未設定伺服器」，單機模式不受影響。
    /// </summary>
    public static class BuildTool
    {
        const string ServerFile = "Assets/Resources/ServerUrl.txt";
        const string OutDir = "Build/WebGL";

        [MenuItem("Snake3D/建置 WebGL")]
        public static void BuildWebGL()
        {
            ProjectSetup.Apply();
            string url = (Environment.GetEnvironmentVariable("GAME_SERVER_URL") ?? "").Trim();
            WriteServerUrl(url);
            try
            {
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                    Debug.LogWarning("[Snake3D] 切換到 WebGL 平台沒有回報成功，仍嘗試建置。");
                var opts = new BuildPlayerOptions
                {
                    scenes = new[] { ProjectSetup.ScenePath },
                    locationPathName = OutDir,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None
                };
                var rep = BuildPipeline.BuildPlayer(opts);
                var sum = rep.summary;
                Debug.Log("[Snake3D] 建置結果：" + sum.result + "，大小 " + (sum.totalSize / 1048576f).ToString("0.0") + " MB，輸出 " + OutDir);
                if (sum.result != BuildResult.Succeeded)
                {
                    if (Application.isBatchMode) EditorApplication.Exit(1);
                    throw new Exception("WebGL 建置失敗：" + sum.result);
                }
                CopySeoAssets();
            }
            finally { CleanServerUrl(); }
        }

        static void WriteServerUrl(string url)
        {
            Directory.CreateDirectory("Assets/Resources");
            File.WriteAllText(ServerFile, url, new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(ServerFile);
        }

        static void CleanServerUrl()
        {
            if (File.Exists(ServerFile)) { AssetDatabase.DeleteAsset(ServerFile); }
        }

        /// <summary>把 Tools/seo 裡的 favicon、og-image、manifest 複製到輸出資料夾（若存在）。</summary>
        static void CopySeoAssets()
        {
            const string src = "Assets/WebGLTemplates/SnakeTemplate/static";
            if (!Directory.Exists(src)) return;
            foreach (var f in Directory.GetFiles(src))
                File.Copy(f, Path.Combine(OutDir, Path.GetFileName(f)), true);
        }
    }
}
#endif
