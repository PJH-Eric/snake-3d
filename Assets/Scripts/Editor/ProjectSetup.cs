#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Snake3D.EditorTools
{
    /// <summary>
    /// 第一次開啟專案時自動套用必要設定（只做一次，之後可從選單 Snake3D ▸ 套用專案設定 重跑）。
    /// 所有設定都集中在這裡，換機器或重新產生 ProjectSettings 時不會漏。
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        const string DoneKey = "Snake3D.ProjectSetup.v1";
        public const string ScenePath = "Assets/Scenes/Main.unity";

        static ProjectSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (BuildPipeline.isBuildingPlayer) return;
                if (EditorPrefs.GetBool(DoneKey + Application.dataPath, false)) return;
                Apply();
            };
        }

        [MenuItem("Snake3D/套用專案設定")]
        public static void Apply()
        {
            PlayerSettings.productName = "3D貪吃蛇";
            PlayerSettings.companyName = "Eric";
            PlayerSettings.colorSpace = ColorSpace.Gamma;             // WebGL 相容性最好、行動裝置省效能
            PlayerSettings.runInBackground = false;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;   // GitHub Pages 不能設 Content-Encoding，不壓縮最穩
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.template = "PROJECT:SnakeTemplate";
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, true);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);

            UseLegacyInput();
            EnsureScene();
            AssetDatabase.SaveAssets();
            EditorPrefs.SetBool(DoneKey + Application.dataPath, true);
            Debug.Log("[Snake3D] 專案設定已套用。");
        }

        /// <summary>遊戲只用舊版 Input（Input.GetKey），確保 Active Input Handling = Input Manager (Old)。</summary>
        static void UseLegacyInput()
        {
            try
            {
                var objs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
                if (objs == null || objs.Length == 0) return;
                var so = new SerializedObject(objs[0]);
                var p = so.FindProperty("activeInputHandler");
                if (p != null && p.intValue != 0) { p.intValue = 0; so.ApplyModifiedPropertiesWithoutUndo(); }
            }
            catch (System.Exception e) { Debug.LogWarning("[Snake3D] 無法設定 Active Input Handling：" + e.Message); }
        }

        /// <summary>場景只是空殼：遊戲 UI 與 3D 場景都由 App.Boot 在執行時產生。</summary>
        public static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory("Assets/Scenes");
                var sc = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(sc, ScenePath);
            }
            var list = EditorBuildSettings.scenes;
            foreach (var s in list) if (s.path == ScenePath) return;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
#endif
