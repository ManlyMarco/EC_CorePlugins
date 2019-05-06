using System.Linq;
using BepInEx;
using ChaCustom;
using Config;
using EC.Core.Internal;
using Harmony;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EC.Core.Fixes.ShaderDropdown
{
    /// <summary>
    /// Adaptation of plugin koikoi.happy.nu.fix_shader_dropdown
    /// </summary>
    [BepInPlugin(GUID, "Fix Shader Dropdown Menu", Version)]
    public class FixShaderDropdown : BaseUnityPlugin
    {
        public const string GUID = "EC.Core.Fixes.ShaderDropdown";
        public const string Version = Metadata.PluginsVersion;

        private void Awake()
        {
            if (!Utilities.FixesConfig.Wrap(Utilities.ConfigSectionFixes, "Fix shader dropdown menu",
                "Fixes the shader selection menu going off-screen when there are many modded shaders installed.", true).Value)
                return;

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene s, LoadSceneMode l)
        {
            if (Singleton<Manager.Scene>.Instance.NowSceneNames.Any(sName => sName == "Config"))
            {
                var tmpDropdown = Traverse.Create(Singleton<GraphicSetting>.Instance).Field("rampIDDropdown").GetValue<TMP_Dropdown>();
                tmpDropdown.template.pivot = new Vector2(0.5f, 0f);
                tmpDropdown.template.anchorMin = new Vector2(0f, 0.86f);
            }
            else if (Singleton<Manager.Scene>.Instance.NowSceneNames.Any(sName => sName == "CustomScene"))
            {
                var tmpDropdown = Traverse.Create(Singleton<CustomConfig>.Instance).Field("ddRamp").GetValue<TMP_Dropdown>();
                tmpDropdown.template.pivot = new Vector2(0.5f, 0f);
                tmpDropdown.template.anchorMin = new Vector2(0f, 0.86f);
            }
        }
    }
}
