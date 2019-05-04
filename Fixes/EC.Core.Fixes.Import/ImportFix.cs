using System.Collections.Generic;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Harmony;
using EC.Core.Internal;
using Harmony;

namespace EC.Core.Fixes.Import
{
    [BepInPlugin(GUID, PluginName, Version)]
    public class ImportFix : BaseUnityPlugin
    {
        public const string GUID = "EC.Core.Fixes.Import";
        public const string PluginName = "Import Fixes";
        public const string Version = Metadata.PluginsVersion;

        private void Start() => HarmonyWrapper.PatchAll(typeof(ImportFix));

        /// <summary>
        /// Prevent items with sideloader-assigned IDs from being removed
        /// </summary>
        [HarmonyPrefix, HarmonyPatch(typeof(ChaFileControl), nameof(ChaFileControl.CheckDataRange))]
        public static bool CheckDataRangePrefix(ref bool __result)
        {
            __result = true;
            return false;
        }

        /// <summary>
        /// Prevent items with sideloader-assigned IDs from being removed
        /// </summary>
        [HarmonyPrefix, HarmonyPatch(typeof(ChaFileControl), nameof(ChaFileControl.CheckDataRangeCoordinate), typeof(ChaFileCoordinate), typeof(int), typeof(List<string>))]
        public static bool CheckDataRangeCoordinatePrefix(ref bool __result)
        {
            __result = true;
            return false;
        }
        
        /// <summary>
        /// Fix null exception when importing characters with modded clothes under some conditions
        /// </summary>
        [HarmonyTranspiler, HarmonyPatch(typeof(ChaFileControl), nameof(ChaFileControl.CheckUsedPackageCoordinate))]
        public static IEnumerable<CodeInstruction> ImportNullFixTpl(IEnumerable<CodeInstruction> instructions)
        {
            var target = AccessTools.Property(typeof(ListInfoBase), nameof(ListInfoBase.Kind)).GetMethod;
            var replacement = AccessTools.Method(typeof(ImportFix), nameof(SafeGetKind));

            foreach (var instruction in instructions)
            {
                if (Equals(instruction.operand, target))
                    yield return new CodeInstruction(OpCodes.Call, replacement);
                else
                    yield return instruction;
            }
        }

        private static int SafeGetKind(ListInfoBase instance)
        {
            if (instance == null) return -9999;
            return instance.Kind;
        }
    }
}
