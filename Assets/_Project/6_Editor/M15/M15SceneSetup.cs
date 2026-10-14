using System.Collections.Generic;
using Card.Bootstrap.Replay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Card.Editor.M15
{
    /// <summary>
    /// M15-T1 回放场景一键生成（M6-T1 模式，只在编辑器使用）：
    /// 生成 Replay.unity（只挂 <see cref="ReplaySceneBootstrap"/>，运行时自建 UI）
    /// 并追加写入 Build Settings。不手写场景 YAML，避免脆弱引用与 Awake 时序问题。
    /// </summary>
    public static class M15SceneSetup
    {
        private const string ReplayPath = "Assets/Scenes/Replay.unity";

        [MenuItem("Card/M15/生成回放场景")]
        public static void GenerateReplayScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("ReplayBootstrap");
            go.AddComponent<ReplaySceneBootstrap>();
            EditorSceneManager.SaveScene(scene, ReplayPath);

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes)
            {
                new EditorBuildSettingsScene(ReplayPath, enabled: true),
            };
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("M15-T1", "回放场景已生成并写入 Build Settings。", "OK");
        }
    }
}
