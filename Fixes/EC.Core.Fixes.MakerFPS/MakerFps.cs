using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Harmony;
using EC.Core.Internal;
using Harmony;
using UnityEngine;
using UnityEngine.UI;

namespace EC.Core.Fixes.MakerFPS
{
    [BepInPlugin(GUID, PluginName, Version)]
    public class MakerFps : BaseUnityPlugin
    {
        public const string GUID = "EC.Core.Fixes.MakerFPS";
        public const string PluginName = "Maker FPS optimization";
        public const string Version = Metadata.PluginsVersion;

        private void Start()
        {
            if (!Utilities.FixesConfig.Wrap(Utilities.ConfigSectionFixes, "Improve maker FPS",
                "Improves FPS in character maker at the cost of slower switching between tabs.", SystemInfo.processorFrequency < 2700).Value)
                return;

            HarmonyWrapper.PatchAll(typeof(MakerFps));
        }

        [HarmonyPostfix, HarmonyPatch(typeof(CustomScene), "Start")]
        public static void MakerStartHook(CustomScene __instance)
        {
            __instance.StartCoroutine(OnMakerLoaded());
        }

        private static IEnumerator OnMakerLoaded()
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            var treeTop = GameObject.Find("CvsMenuTree");

            foreach (Transform mainTab in treeTop.transform)
            {
                var topMenuToggle = _canvasObjectLinks.TryGetValue(mainTab.name, out var topTabName)
                    ? GameObject.Find(topTabName)?.GetComponent<Toggle>()
                    : null;

                var updateTabCallbacks = new List<Action>();
                foreach (Transform subTab in mainTab)
                {
                    var toggle = subTab.GetComponent<Toggle>();
                    if (toggle == null) continue;

                    // Tab pages have raycast controllers on them, buttons have only image
                    var innerContent = subTab.Cast<Transform>().FirstOrDefault(x => x.GetComponent<UI_RaycastCtrl>() != null)?.gameObject;
                    if (innerContent == null) continue;

                    void SetTabActive(bool val)
                    {
                        innerContent.SetActive(val && (topMenuToggle == null || topMenuToggle.isOn));
                    }

                    toggle.onValueChanged.AddListener(SetTabActive);
                    updateTabCallbacks.Add(() => SetTabActive(toggle.isOn));
                }

                topMenuToggle?.onValueChanged.AddListener(
                    val =>
                    {
                        foreach (var callback in updateTabCallbacks)
                            callback();
                    });

                foreach (var callback in updateTabCallbacks)
                    callback();
            }
        }

        /// <summary>
        /// Because Illusion can't make consistent names. There's probably a better way.
        /// </summary>
        private static readonly Dictionary<string, string> _canvasObjectLinks = new Dictionary<string, string>
        {
            {"00_FaceTop"      , "tglFace"       },
            {"01_BodyTop"      , "tglBody"       },
            {"02_HairTop"      , "tglHair"       },
            {"03_ClothesTop"   , "tglCoordinate" },
            {"04_AccessoryTop" , "tglAccessories"},
            {"05_ParameterTop" , "tglParameter"  },
            {"06_SystemTop"    , "tglSystem"     },
        };
    }
}
