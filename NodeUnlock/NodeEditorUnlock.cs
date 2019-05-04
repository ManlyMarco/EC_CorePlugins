using BepInEx;
using BepInEx.Harmony;
using EC.Core.Internal;
using Harmony;
using HEdit;
using UniRx;
using YS_Node;

namespace EC.Core.Fixes.NodeUnlock
{
    [BepInPlugin(GUID, PluginName, Version)]
    public class NodeEditorUnlock : BaseUnityPlugin
    {
        public const string GUID = "EC.Core.Fixes.NodeUnlock";
        public const string PluginName = "Node Editor Unlock";
        public const string Version = Metadata.PluginsVersion;

        private void Start() => HarmonyWrapper.PatchAll(typeof(NodeEditorUnlock));

        [HarmonyPrefix, HarmonyPatch(typeof(NodeSettingCanvas), nameof(NodeSettingCanvas.NodeRestriction))]
        public static bool NodeRestrictionPrefix()
        {
            return false;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(NodeUI), "Start")]
        public static void NodeUIStartPostfix(NodeUI __instance)
        {
            Traverse.Create(__instance).Field("limitOver").GetValue<BoolReactiveProperty>().Dispose();
        }
    }
}
