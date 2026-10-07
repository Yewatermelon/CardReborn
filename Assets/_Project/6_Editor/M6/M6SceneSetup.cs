using System.IO;
using Card.Bootstrap.Battle;
using Card.Bootstrap.Menu;
using Card.Core;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Card.Editor.M6
{
    /// <summary>
    /// M6-T1 场景与运行时配置一键生成（只在编辑器使用）：
    /// 1) 把生成物 JSON 复制到 StreamingAssets/CardConfig（运行时 ConfigFileLoader 读取点）；
    /// 2) 生成 MainMenu/Battle 两个场景（各只挂一个引导器），并写入 Build Settings。
    /// 不手写场景 YAML，避免脆弱引用与 Awake 时序问题。
    /// </summary>
    public static class M6SceneSetup
    {
        private const string ScenesDir = "Assets/Scenes";
        private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
        private const string BattlePath = "Assets/Scenes/Battle.unity";
        private const string SourceConfigDir = "Assets/_Project/Config";
        private const string TargetConfigDir = "Assets/StreamingAssets/CardConfig";

        private static readonly string[] ConfigFiles =
        {
            "cards.json", "heroes.json", "hero_powers.json",
            "rarity_weights.json", "gacha.json", "rules.json",
        };

        [MenuItem("Card/M6/1. 部署运行时配置到 StreamingAssets")]
        public static void DeployRuntimeConfig()
        {
            Directory.CreateDirectory(TargetConfigDir);
            foreach (string file in ConfigFiles)
            {
                string source = Path.Combine(SourceConfigDir, file);
                string target = Path.Combine(TargetConfigDir, file);
                File.Copy(source, target, overwrite: true);
            }

            AssetDatabase.Refresh();
            GameLog.Info(LogChannel.Boot, "M6-T1：运行时配置已部署到 " + TargetConfigDir);
            EditorUtility.DisplayDialog("M6-T1", "运行时配置已部署到 StreamingAssets/CardConfig", "OK");
        }

        [MenuItem("Card/M6/2. 生成主菜单与对战场景")]
        public static void GenerateScenes()
        {
            EnsureChineseFontAsset();
            EnsureFolder("Assets", "Scenes");
            BuildScene(MainMenuPath, "MainMenuBootstrap", typeof(MainMenuBootstrap));
            BuildScene(BattlePath, "BattleBootstrap", typeof(BattleSceneBootstrap));

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainMenuPath, enabled: true),
                new EditorBuildSettingsScene(BattlePath, enabled: true),
            };
            AssetDatabase.SaveAssets();
            GameLog.Info(LogChannel.Boot, "M6-T1：场景已生成并写入 Build Settings（MainMenu 序 0）。");
            EditorUtility.DisplayDialog("M6-T1", "主菜单与对战场景已生成并写入 Build Settings。", "OK");
        }

        private static void BuildScene(string path, string rootName, System.Type bootstrapType)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject(rootName);
            go.AddComponent(bootstrapType);
            EditorSceneManager.SaveScene(scene, path);
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + child))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private const string FontPath = "Assets/_Project/4_Presentation/Art/Fonts/NotoSansCJKsc-Regular.otf";
        private const string FontAssetPath = "Assets/_Project/4_Presentation/Art/Fonts/UiChineseSDF.asset";

        /// <summary>
        /// 确保存在中文 TMP 字体资产并设为 TMP 全局默认（运行时零加载代码）。
        /// 编辑器里 OS 动态字体走 FontEngine.LoadFontFace(Font) 必失败（无内嵌字体数据），
        /// 因此字体面必须来自工程内导入的字体资产（Noto Sans CJK SC，SIL OFL 1.1 可随仓库分发）。
        /// </summary>
        private static void EnsureChineseFontAsset()
        {
            TMP_FontAsset? existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null)
            {
                SetDefaultFont(existing);
                return;
            }

            Font source = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (source == null)
            {
                EditorUtility.DisplayDialog(
                    "M6-T1", "未找到中文字体源文件：\n" + FontPath + "\n请确认字体已入库。", "OK");
                return;
            }

            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(source);
            AssetDatabase.CreateAsset(asset, FontAssetPath);
            if (asset.atlasTextures != null && asset.atlasTextures.Length > 0 && asset.atlasTextures[0] != null)
            {
                AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            }

            if (asset.material != null)
            {
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            }

            AssetDatabase.SaveAssets();
            SetDefaultFont(asset);
            GameLog.Info(LogChannel.Boot, "M6-T1：中文字体资产已生成并设为 TMP 默认：" + FontAssetPath);
        }

        private static void SetDefaultFont(TMP_FontAsset asset)
        {
            if (TMP_Settings.defaultFontAsset == asset)
            {
                return;
            }

            // defaultFontAsset 只读（TMP 3.0.7），经 SerializedObject 写其序列化字段并持久化。
            var settings = new SerializedObject(TMP_Settings.instance);
            settings.FindProperty("m_defaultFontAsset").objectReferenceValue = asset;
            settings.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }
    }
}
