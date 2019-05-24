using BepInEx.Logging;
using Harmony;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace EC.Core.ResourceRedirector
{
    internal static class Hooks
    {
        public static void InstallHooks() => BepInEx.Harmony.HarmonyWrapper.PatchAll(typeof(Hooks));

        #region List Loading
        [HarmonyPrefix, HarmonyPatch(typeof(ChaListControl), nameof(ChaListControl.CheckItemID), new[] { typeof(int), typeof(int) })]
        public static bool CheckItemIDHook(int category, int id, ref byte __result, ChaListControl __instance)
        {
            int pid = ListLoader.CalculateGlobalID(category, id);

            byte result = __instance.CheckItemID(pid);

            if (result > 0)
            {
                __result = result;
                return false;
            }

            return true;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(ChaListControl), nameof(ChaListControl.AddItemID), new[] { typeof(int), typeof(int), typeof(byte) })]
        public static bool AddItemIDHook(int category, int id, byte flags, ChaListControl __instance)
        {
            int pid = ListLoader.CalculateGlobalID(category, id);

            byte result = __instance.CheckItemID(pid);

            if (result > 0)
            {
                __instance.AddItemID(pid, flags);
                return false;
            }

            return true;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(ChaListControl), "LoadListInfoAll")]
        public static void LoadListInfoAllPostHook(ChaListControl __instance)
        {
            if (ResourceRedirector.EmulationEnabled)
            {
                string listPath = Path.Combine(ResourceRedirector.EmulatedDir, @"list\characustom");

                if (Directory.Exists(listPath))
                    foreach (string csvPath in Directory.GetFiles(listPath, "*.csv", SearchOption.AllDirectories))
                    {
                        var chaListData = ListLoader.LoadCSV(File.OpenRead(csvPath));
                        ListLoader.ExternalDataList.Add(chaListData);
                    }
            }

            ListLoader.LoadAllLists(__instance);
        }
        #endregion

        #region Asset Loading
        [HarmonyPrefix, HarmonyPatch(typeof(AssetBundleManager), nameof(AssetBundleManager.LoadAsset), new[] { typeof(string), typeof(string), typeof(Type), typeof(string) })]
        public static bool LoadAssetPreHook(ref AssetBundleLoadAssetOperation __result, ref string assetBundleName, ref string assetName, Type type, string manifestAssetBundleName)
        {
            __result = ResourceRedirector.HandleAsset(assetBundleName, assetName, type, manifestAssetBundleName, ref __result);

            //Redirect KK vanilla assets to EC vanilla assets
            if (__result == null && !ResourceRedirector.AssetBundleExists(assetBundleName) && assetBundleName.EndsWith(".unity3d") && assetBundleName.StartsWith("chara/"))
            {
                string temp = assetBundleName.Replace(".unity3d", "");
                if (temp.Length >= 2)
                {
                    temp = temp.Substring(temp.Length - 2, 2);
                    if (int.TryParse(temp, out _))
                    {
                        if (assetBundleName.StartsWith("chara/thumb/") && !assetBundleName.StartsWith($"chara/thumb/{temp}/"))
                        {
                            temp = assetBundleName.Replace("chara/thumb/", $"chara/thumb/{temp}/");
                            __result = AssetBundleManager.LoadAsset(temp, assetName, type, manifestAssetBundleName);
                            return false;
                        }
                        else if (assetBundleName.StartsWith("chara/") && !assetBundleName.StartsWith($"chara/{temp}/"))
                        {
                            temp = assetBundleName.Replace("chara/", $"chara/{temp}/");
                            __result = AssetBundleManager.LoadAsset(temp, assetName, type, manifestAssetBundleName);
                            return false;
                        }
                    }
                }
            }

            if (__result == null)
            {
                if (!ResourceRedirector.AssetBundleExists(assetBundleName))
                {
                    //An asset that does not exist is being requested from from an asset bundle that does not exist
                    //Redirect to an asset bundle the does exist so that the game does not attempt to open a non-existant file and cause errors
                    ResourceRedirector.Logger.Log(LogLevel.Debug, $"Asset {assetName} does not exist in asset bundle {assetBundleName}.");
                    assetBundleName = "chara/00/mt_ramp_00.unity3d";
                    assetName = "dummy";
                }
                return true;
            }

            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(AssetBundleManager), "LoadAssetAsync", new[] { typeof(string), typeof(string), typeof(Type), typeof(string) })]
        public static bool LoadAssetAsyncPreHook(ref AssetBundleLoadAssetOperation __result, ref string assetBundleName, ref string assetName, Type type, string manifestAssetBundleName)
        {
            __result = ResourceRedirector.HandleAsset(assetBundleName, assetName, type, manifestAssetBundleName, ref __result);

            //Redirect KK vanilla assets to EC vanilla assets
            if (__result == null && !ResourceRedirector.AssetBundleExists(assetBundleName) && assetBundleName.EndsWith(".unity3d") && assetBundleName.StartsWith("chara/"))
            {
                string temp = assetBundleName.Replace(".unity3d", "");
                if (temp.Length >= 2)
                {
                    temp = temp.Substring(temp.Length - 2, 2);
                    if (int.TryParse(temp, out _))
                    {
                        if (assetBundleName.StartsWith("chara/thumb/") && !assetBundleName.StartsWith($"chara/thumb/{temp}/"))
                        {
                            temp = assetBundleName.Replace("chara/thumb/", $"chara/thumb/{temp}/");
                            __result = AssetBundleManager.LoadAssetAsync(temp, assetName, type, manifestAssetBundleName);
                            return false;
                        }
                        else if (assetBundleName.StartsWith("chara/") && !assetBundleName.StartsWith($"chara/{temp}/"))
                        {
                            temp = assetBundleName.Replace("chara/", $"chara/{temp}/");
                            __result = AssetBundleManager.LoadAssetAsync(temp, assetName, type, manifestAssetBundleName);
                            return false;
                        }
                    }
                }
            }

            if (__result == null)
            {
                if (!ResourceRedirector.AssetBundleExists(assetBundleName))
                {
                    //An asset that does not exist is being requested from from an asset bundle that does not exist
                    //Redirect to an asset bundle the does exist so that the game does not attempt to open a non-existant file and cause errors
                    ResourceRedirector.Logger.Log(LogLevel.Debug, $"Asset {assetName} does not exist in asset bundle {assetBundleName}.");
                    assetBundleName = "chara/00/mt_ramp_00.unity3d";
                    assetName = "dummy";
                }
                return true;
            }
            else
                return false;
        }
        #endregion

        [HarmonyTranspiler, HarmonyPatch(typeof(AssetBundleManager), nameof(AssetBundleManager.LoadAssetBundleInternal))]
        public static IEnumerable<CodeInstruction> LoadAssetBundleInternalTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionsList = instructions.ToList();
            MethodInfo LoadMethod = typeof(AssetBundle).GetMethod(nameof(AssetBundle.LoadFromFile), AccessTools.all, null, new[] { typeof(string) }, null);

            int IndexLoadFromFile = instructionsList.FindIndex(instruction => instruction.opcode == OpCodes.Call && (MethodInfo)instruction.operand == LoadMethod);

            //Switch out a LoadFromFile call
            if (IndexLoadFromFile > 0)
                instructionsList[IndexLoadFromFile].operand = typeof(ResourceRedirector).GetMethod(nameof(ResourceRedirector.HandleAssetBundle), AccessTools.all);

            return instructions;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(AssetBundleCheck), nameof(AssetBundleCheck.IsFile))]
        public static void IsFileHook(string assetBundleName, string fileName, ref bool __result)
        {
            if (ResourceRedirector.EmulationEnabled && __result == false)
            {
                string dir = Path.Combine(ResourceRedirector.EmulatedDir, assetBundleName.Replace('/', '\\').Replace(".unity3d", ""));

                if (Directory.Exists(dir))
                    __result = true;
            }

            //Redirect KK vanilla assets to EC vanilla assets
            if (__result == false && assetBundleName.EndsWith(".unity3d") && assetBundleName.StartsWith("chara/"))
            {
                string temp = assetBundleName.Replace(".unity3d", "");
                if (temp.Length >= 2)
                {
                    temp = temp.Substring(temp.Length - 2, 2);
                    if (int.TryParse(temp, out _))
                    {
                        if (assetBundleName.StartsWith("chara/thumb/") && !assetBundleName.StartsWith($"chara/thumb/{temp}/"))
                        {
                            temp = assetBundleName.Replace("chara/thumb/", $"chara/thumb/{temp}/");
                            __result = AssetBundleCheck.IsFile(temp, fileName);
                        }
                        else if (!assetBundleName.StartsWith($"chara/{temp}/") && !assetBundleName.StartsWith($"chara/{temp}/"))
                        {
                            temp = assetBundleName.Replace("chara/", $"chara/{temp}/");
                            __result = AssetBundleCheck.IsFile(temp, fileName);
                        }
                    }
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(AssetBundleData))]
        [HarmonyPatch(nameof(AssetBundleData.isFile), MethodType.Getter)]
        public static void IsFileHook2(ref bool __result, AssetBundleData __instance)
        {
            if (ResourceRedirector.EmulationEnabled && __result == false)
            {
                string dir = Path.Combine(ResourceRedirector.EmulatedDir, __instance.bundle.Replace('/', '\\').Replace(".unity3d", ""));

                if (Directory.Exists(dir))
                    __result = true;
            }

            //Redirect KK vanilla assets to EC vanilla assets
            if (__result == false && __instance.bundle.EndsWith(".unity3d") && __instance.bundle.StartsWith("chara/"))
            {
                string temp = __instance.bundle.Replace(".unity3d", "");
                if (temp.Length >= 2)
                {
                    temp = temp.Substring(temp.Length - 2, 2);
                    if (int.TryParse(temp, out _))
                    {
                        if (__instance.bundle.StartsWith("chara/thumb/") && !__instance.bundle.StartsWith($"chara/thumb/{temp}/"))
                        {
                            temp = __instance.bundle.Replace("chara/thumb/", $"chara/thumb/{temp}/");
                            __result = AssetBundleCheck.IsFile(temp);
                        }
                        else if (!__instance.bundle.StartsWith($"chara/{temp}/") && !__instance.bundle.StartsWith($"chara/{temp}/"))
                        {
                            temp = __instance.bundle.Replace("chara/", $"chara/{temp}/");
                            __result = AssetBundleCheck.IsFile(temp);
                        }
                    }
                }
            }


        }
    }
}