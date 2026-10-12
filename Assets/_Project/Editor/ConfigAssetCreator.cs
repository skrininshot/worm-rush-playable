using System.IO;
using UnityEditor;
using UnityEngine;
using WormRush.Config;

namespace WormRush.EditorTools
{
    /// <summary>
    /// Makes sure Assets/_Project/Resources/PlayableConfig.asset exists and points at the circle shader,
    /// so a fresh clone runs without hand-made assets. Runs on every editor load; also callable headless:
    /// Unity -batchmode -executeMethod WormRush.EditorTools.ConfigAssetCreator.EnsureExists
    /// </summary>
    [InitializeOnLoad]
    public static class ConfigAssetCreator
    {
        const string ResourcesFolder = "Assets/_Project/Resources";
        const string AssetPath = ResourcesFolder + "/PlayableConfig.asset";
        const string ShaderPath = "Assets/_Project/View/Shaders/Circle.shader";

        static ConfigAssetCreator()
        {
            EditorApplication.delayCall += EnsureExists;
        }

        [MenuItem("Tools/Worm Rush/Ensure PlayableConfig")]
        public static void EnsureExists()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            PlayableConfig config = AssetDatabase.LoadAssetAtPath<PlayableConfig>(AssetPath);

            if (config == null)
            {
                if (!Directory.Exists(ResourcesFolder)) Directory.CreateDirectory(ResourcesFolder);
                config = ScriptableObject.CreateInstance<PlayableConfig>();
                config.CircleShader = shader;
                AssetDatabase.CreateAsset(config, AssetPath);
                AssetDatabase.SaveAssets();
                Debug.Log("Created " + AssetPath);
            }
            else if (config.CircleShader == null && shader != null)
            {
                config.CircleShader = shader;
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }
        }
    }
}
