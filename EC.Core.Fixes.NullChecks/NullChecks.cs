using System.Collections.Generic;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Harmony;
using EC.Core.Internal;
using Harmony;

namespace EC.Core.Fixes.NullChecks
{
    [BepInPlugin(GUID, PluginName, Version)]
    public class NullChecks : BaseUnityPlugin
    {
        public const string GUID = "EC.Core.Fixes.NullChecks";
        public const string PluginName = "Null Checks";
        public const string Version = Metadata.PluginsVersion;

        private void Start() => HarmonyWrapper.PatchAll(typeof(NullChecks));
        /// <summary>
        /// Prevents null ChaCustomHairComponent rendAccessory objects from causing errors
        /// Ported from https://github.com/DeathWeasel1337/KK_Plugins KK_CutsceneLockupFix
        /// </summary>
        [HarmonyPrefix, HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeSettingHairAcsColor))]
        public static bool ChangeSettingHairAcsColorPrefix(int parts, ChaControl __instance)
        {
            int HairAcsColorNum = __instance.GetHairAcsColorNum(parts);
            if (HairAcsColorNum == 0)
                return false;

            ChaCustomHairComponent customHairComponent = __instance.GetCustomHairComponent(parts);
            if (null == customHairComponent)
                return false;

            for (int i = 0; i < customHairComponent.rendAccessory.Length; i++)
                for (int j = 0; j < HairAcsColorNum; j++)
                    if (customHairComponent.rendAccessory[i] == null) //Added null check
                        return false;

            return true;
        }
        /// <summary>
        /// Prevents null ChaCustomHairComponent trfLength objects from causing errors every update
        /// Ported from https://github.com/Keelhauled/KoikatuPlugins FixCompilation
        /// </summary>
        [HarmonyPrefix, HarmonyPatch(typeof(ChaCustomHairComponent), "Update")]
        public static bool ChaCustomHairComponentUpdatePrefix(ChaCustomHairComponent __instance)
        {
            if (__instance.trfLength != null)
                for (int i = 0; i < __instance.trfLength.Length; i++)
                    if (__instance.trfLength[i] == null) //Added null check
                        return false;

            return true;
        }

        /// <summary>
        /// Fix null exception when importing characters with modded clothes under some conditions
        /// </summary>
        [HarmonyTranspiler, HarmonyPatch(typeof(ChaFileControl), nameof(ChaFileControl.CheckUsedPackageCoordinate))]
        public static IEnumerable<CodeInstruction> ImportNullFixTpl(IEnumerable<CodeInstruction> instructions)
        {
            var target = AccessTools.Property(typeof(ListInfoBase), nameof(ListInfoBase.Kind)).GetMethod;
            var replacement = AccessTools.Method(typeof(NullChecks), nameof(SafeGetKind));

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
