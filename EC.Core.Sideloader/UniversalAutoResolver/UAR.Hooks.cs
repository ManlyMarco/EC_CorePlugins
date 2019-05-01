using BepInEx.Harmony;
using BepInEx.Logging;
using EC.Core.ExtensibleSaveFormat;
using Harmony;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EC.Core.Sideloader.UniversalAutoResolver
{
    public static class Hooks
    {
        private static bool DoingImport = true;

        public static void InstallHooks()
        {
            ExtendedSave.CardBeingLoaded += ExtendedCardLoad;
            ExtendedSave.CardBeingSaved += ExtendedCardSave;
            ExtendedSave.CardBeingImported += ExtendedCardImport;

            ExtendedSave.CoordinateBeingLoaded += ExtendedCoordinateLoad;
            ExtendedSave.CoordinateBeingSaved += ExtendedCoordinateSave;
            ExtendedSave.CoordinateBeingImported += ExtendedCoordinateImport;

            HarmonyWrapper.PatchAll(typeof(Hooks));
        }
        /// <summary>
        /// Re-enable sideloader card and coordinate saving once import is finished
        /// </summary>
        [HarmonyPostfix, HarmonyPatch(typeof(ConvertChaFileScene), "OnDestroy")]
        public static void ConvertChaFileSceneEnd() => DoingImport = false;

        #region ChaFile

        private static void IterateCardPrefixes(Action<Dictionary<CategoryProperty, StructValue<int>>, object, ICollection<ResolveInfo>, string> action, ChaFile file, ICollection<ResolveInfo> extInfo)
        {
            action(StructReference.ChaFileFaceProperties, file.custom.face, extInfo, "");
            action(StructReference.ChaFileBodyProperties, file.custom.body, extInfo, "");
            action(StructReference.ChaFileHairProperties, file.custom.hair, extInfo, "");
            action(StructReference.ChaFileMakeupProperties, file.custom.face.baseMakeup, extInfo, "");

            string prefix = $"outfit.";

            action(StructReference.ChaFileClothesProperties, file.coordinate.clothes, extInfo, prefix);

            for (int acc = 0; acc < file.coordinate.accessory.parts.Length; acc++)
            {
                string accPrefix = $"{prefix}accessory{acc}.";

                action(StructReference.ChaFileAccessoryPartsInfoProperties, file.coordinate.accessory.parts[acc], extInfo, accPrefix);
            }
        }

        private static void ExtendedCardImport(Dictionary<string, PluginData> importedExtendedData)
        {
            if (importedExtendedData.TryGetValue("com.bepis.sideloader.universalautoresolver", out var pluginData))
            {
                if (pluginData != null && pluginData.data.ContainsKey("info"))
                {
                    var tmpExtInfo = (object[])pluginData.data["info"];
                    var extInfo = tmpExtInfo.Select(x => ResolveInfo.Deserialize((byte[])x)).ToList();

                    for (int i = 0; i < extInfo.Count;)
                    {
                        if (extInfo[i].Property.StartsWith("outfit0") && extInfo[i].Property.EndsWith("ClothesShoesInner"))
                        {
                            //KK had inner shoes, EC does not
                            extInfo.RemoveAt(i);
                        }
                        else if (extInfo[i].Property.StartsWith("outfit0"))

                        {
                            extInfo[i].Property = extInfo[i].Property.Replace("outfit0", "outfit");

                            //KK originally had only one emblem
                            if (extInfo[i].Property.EndsWith("Emblem"))
                                extInfo[i].Property += "0";

                            //KK has multiple shoes slots, convert to one shoes slot
                            extInfo[i].Property = extInfo[i].Property.Replace("ClothesShoesOuter", "ClothesShoes");

                            i++;
                        }
                        else if (extInfo[i].Property.StartsWith("outfit"))
                        {
                            //Remove all the excess outfits
                            extInfo.RemoveAt(i);
                        }
                        else
                            i++;
                    }

                    importedExtendedData[UniversalAutoResolver.UARExtID] = new PluginData
                    {
                        data = new Dictionary<string, object>
                        {
                            ["info"] = extInfo.Select(x => x.Serialize()).ToList()
                        }
                    };
                }
            }

            if (Sideloader.DebugLogging.Value && importedExtendedData.TryGetValue(UniversalAutoResolver.UARExtID, out var extData))
            {
                if (extData == null || !extData.data.ContainsKey("info"))
                {
                    Sideloader.Logger.Log(LogLevel.Debug, "Imported card data: No sideloader marker found");
                }
                else
                {
                    var tmpExtInfo = (List<byte[]>)extData.data["info"];
                    var extInfo = tmpExtInfo.Select(ResolveInfo.Deserialize).ToList();

                    Sideloader.Logger.Log(LogLevel.Debug, $"Imported card data: Sideloader marker found, external info count: {extInfo.Count}");

                    foreach (ResolveInfo info in extInfo)
                        Sideloader.Logger.Log(LogLevel.Debug, $"External info: {info.GUID} : {info.Property} : {info.Slot}");
                }
            }
        }

        private static void ExtendedCardLoad(ChaFile file)
        {
            Sideloader.Logger.Log(LogLevel.Debug, $"Loading card [{file.charaFileName}]");

            var extData = ExtendedSave.GetExtendedDataById(file, UniversalAutoResolver.UARExtID);
            List<ResolveInfo> extInfo;

            if (extData == null || !extData.data.ContainsKey("info"))
            {
                Sideloader.Logger.Log(LogLevel.Debug, "No sideloader marker found");
                extInfo = null;
            }
            else
            {
                var tmpExtInfo = (object[])extData.data["info"];
                extInfo = tmpExtInfo.Select(x => ResolveInfo.Deserialize((byte[])x)).ToList();

                Sideloader.Logger.Log(LogLevel.Debug, $"Sideloader marker found, external info count: {extInfo.Count}");

                if (Sideloader.DebugLogging.Value)
                {
                    foreach (ResolveInfo info in extInfo)
                        Sideloader.Logger.Log(LogLevel.Debug, $"External info: {info.GUID} : {info.Property} : {info.Slot}");
                }
            }

            IterateCardPrefixes(UniversalAutoResolver.ResolveStructure, file, extInfo);
        }

        private static void ExtendedCardSave(ChaFile file)
        {
            if (DoingImport) return;

            List<ResolveInfo> resolutionInfo = new List<ResolveInfo>();

            void IterateStruct(Dictionary<CategoryProperty, StructValue<int>> dict, object obj, IEnumerable<ResolveInfo> extInfo, string propertyPrefix = "")
            {
                foreach (var kv in dict)
                {
                    int slot = kv.Value.GetMethod(obj);

                    //No need to attempt a resolution info lookup for empty accessory slots and pattern slots
                    if (slot == 0)
                        continue;

                    //Check if it's a vanilla item
                    if (slot < 100000000)
                        if (ResourceRedirector.ListLoader.InternalDataList[kv.Key.Category].ContainsKey(slot))
                            continue;

                    //For accessories, make sure we're checking the appropriate category
                    if (kv.Key.Category.ToString().Contains("ao_"))
                    {
                        ChaFileAccessory.PartsInfo AccessoryInfo = (ChaFileAccessory.PartsInfo)obj;

                        if ((int)kv.Key.Category != AccessoryInfo.type)
                        {
                            //If the current category does not match the accessory's category do not attempt a resolution info lookup
                            continue;
                        }
                    }

                    var info = UniversalAutoResolver.TryGetResolutionInfo(kv.Key.ToString(), slot);

                    if (info == null)
                        continue;

                    var newInfo = info.DeepCopy();
                    newInfo.Property = $"{propertyPrefix}{newInfo.Property}";

                    kv.Value.SetMethod(obj, newInfo.Slot);

                    resolutionInfo.Add(newInfo);
                }
            }

            IterateCardPrefixes(IterateStruct, file, null);

            ExtendedSave.SetExtendedDataById(file, UniversalAutoResolver.UARExtID, new PluginData
            {
                data = new Dictionary<string, object>
                {
                    ["info"] = resolutionInfo.Select(x => x.Serialize()).ToList()
                }
            });
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ChaFile), "SaveFile", typeof(BinaryWriter), typeof(bool), typeof(int))]
        public static void ChaFileSaveFilePostHook(ChaFile __instance)
        {
            if (DoingImport) return;

            Sideloader.Logger.Log(LogLevel.Debug, $"Reloading card [{__instance.charaFileName}]");

            var extData = ExtendedSave.GetExtendedDataById(__instance, UniversalAutoResolver.UARExtID);

            var tmpExtInfo = (List<byte[]>)extData.data["info"];
            var extInfo = tmpExtInfo.Select(ResolveInfo.Deserialize).ToList();

            Sideloader.Logger.Log(LogLevel.Debug, $"External info count: {extInfo.Count}");

            if (Sideloader.DebugLogging.Value)
            {
                foreach (ResolveInfo info in extInfo)
                    Sideloader.Logger.Log(LogLevel.Debug, $"External info: {info.GUID} : {info.Property} : {info.Slot}");
            }

            void ResetStructResolveStructure(Dictionary<CategoryProperty, StructValue<int>> propertyDict, object structure, IEnumerable<ResolveInfo> extInfo2, string propertyPrefix = "")
            {
                foreach (var kv in propertyDict)
                {
                    var extResolve = extInfo.FirstOrDefault(x => x.Property == $"{propertyPrefix}{kv.Key.ToString()}");

                    if (extResolve != null)
                        kv.Value.SetMethod(structure, extResolve.LocalSlot);
                }
            }

            IterateCardPrefixes(ResetStructResolveStructure, __instance, extInfo);
        }

        #endregion

        #region ChaFileCoordinate

        private static void IterateCoordinatePrefixes(Action<Dictionary<CategoryProperty, StructValue<int>>, object, ICollection<ResolveInfo>, string> action, ChaFileCoordinate coordinate, ICollection<ResolveInfo> extInfo)
        {
            action(StructReference.ChaFileClothesProperties, coordinate.clothes, extInfo, "");

            for (int acc = 0; acc < coordinate.accessory.parts.Length; acc++)
            {
                string accPrefix = $"accessory{acc}.";

                action(StructReference.ChaFileAccessoryPartsInfoProperties, coordinate.accessory.parts[acc], extInfo, accPrefix);
            }
        }

        private static void ExtendedCoordinateImport(Dictionary<string, PluginData> importedExtendedData)
        {
            if (importedExtendedData.TryGetValue("com.bepis.sideloader.universalautoresolver", out var pluginData))
            {
                if (pluginData != null && pluginData.data.ContainsKey("info"))
                {
                    var tmpExtInfo = (object[])pluginData.data["info"];
                    var extInfo = tmpExtInfo.Select(x => ResolveInfo.Deserialize((byte[])x)).ToList();

                    for (int i = 0; i < extInfo.Count;)
                    {
                        Sideloader.Logger.Log(LogLevel.Debug, $"External info: {extInfo[i].GUID} : {extInfo[i].Property} : {extInfo[i].Slot} : {extInfo[i].CategoryNo}");
                        if (extInfo[i].Property.EndsWith("ClothesShoesInner"))
                        {
                            //KK had inner shoes, EC does not
                            extInfo.RemoveAt(i);
                        }
                        else
                        {
                            extInfo[i].Property = extInfo[i].Property.Replace("outfit0", "outfit");

                            //KK originally had only one emblem
                            if (extInfo[i].Property.EndsWith("Emblem"))
                                extInfo[i].Property += "0";

                            //KK has multiple shoes slots, convert to one shoes slot
                            extInfo[i].Property = extInfo[i].Property.Replace("ClothesShoesOuter", "ClothesShoes");

                            i++;
                        }
                    }

                    importedExtendedData[UniversalAutoResolver.UARExtID] = new PluginData
                    {
                        data = new Dictionary<string, object>
                        {
                            ["info"] = extInfo.Select(x => x.Serialize()).ToList()
                        }
                    };
                }
            }

            if (Sideloader.DebugLogging.Value && importedExtendedData.TryGetValue(UniversalAutoResolver.UARExtID, out var extData))
            {
                if (extData == null || !extData.data.ContainsKey("info"))
                {
                    Sideloader.Logger.Log(LogLevel.Debug, "Imported coordinate data: No sideloader marker found");
                }
                else
                {
                    var tmpExtInfo = (List<byte[]>)extData.data["info"];
                    var extInfo = tmpExtInfo.Select(ResolveInfo.Deserialize).ToList();

                    Sideloader.Logger.Log(LogLevel.Debug, $"Imported coordinate data: Sideloader marker found, external info count: {extInfo.Count}");

                    foreach (ResolveInfo info in extInfo)
                        Sideloader.Logger.Log(LogLevel.Debug, $"External info: {info.GUID} : {info.Property} : {info.Slot} : {info.CategoryNo}");
                }
            }
        }

        private static void ExtendedCoordinateLoad(ChaFileCoordinate file)
        {
            Sideloader.Logger.Log(LogLevel.Debug, $"Loading coordinate [{file.coordinateName}]");

            var extData = ExtendedSave.GetExtendedDataById(file, UniversalAutoResolver.UARExtID);
            List<ResolveInfo> extInfo;

            if (extData == null || !extData.data.ContainsKey("info"))
            {
                Sideloader.Logger.Log(LogLevel.Debug, "No sideloader marker found");
                extInfo = null;
            }
            else
            {
                var tmpExtInfo = (object[])extData.data["info"];
                extInfo = tmpExtInfo.Select(x => ResolveInfo.Deserialize((byte[])x)).ToList();

                Sideloader.Logger.Log(LogLevel.Debug, $"Sideloader marker found, external info count: {extInfo.Count}");

                if (Sideloader.DebugLogging.Value)
                {
                    foreach (ResolveInfo info in extInfo)
                        Sideloader.Logger.Log(LogLevel.Debug, $"External info: {info.GUID} : {info.Property} : {info.Slot}");
                }
            }

            IterateCoordinatePrefixes(UniversalAutoResolver.ResolveStructure, file, extInfo);
        }

        private static void ExtendedCoordinateSave(ChaFileCoordinate file)
        {
            if (DoingImport) return;

            List<ResolveInfo> resolutionInfo = new List<ResolveInfo>();

            void IterateStruct(Dictionary<CategoryProperty, StructValue<int>> dict, object obj, IEnumerable<ResolveInfo> extInfo, string propertyPrefix = "")
            {
                foreach (var kv in dict)
                {
                    int slot = kv.Value.GetMethod(obj);

                    //No need to attempt a resolution info lookup for empty accessory slots and pattern slots
                    if (slot == 0)
                        continue;

                    //Check if it's a vanilla item
                    if (slot < 100000000)
                        if (ResourceRedirector.ListLoader.InternalDataList[kv.Key.Category].ContainsKey(slot))
                            continue;

                    //For accessories, make sure we're checking the appropriate category
                    if (kv.Key.Category.ToString().Contains("ao_"))
                    {
                        ChaFileAccessory.PartsInfo AccessoryInfo = (ChaFileAccessory.PartsInfo)obj;

                        if ((int)kv.Key.Category != AccessoryInfo.type)
                        {
                            //If the current category does not match the accessory's category do not attempt a resolution info lookup
                            continue;
                        }
                    }

                    var info = UniversalAutoResolver.TryGetResolutionInfo(kv.Key.ToString(), slot);

                    if (info == null)
                        continue;

                    var newInfo = info.DeepCopy();
                    newInfo.Property = $"{propertyPrefix}{newInfo.Property}";

                    kv.Value.SetMethod(obj, newInfo.Slot);

                    resolutionInfo.Add(newInfo);
                }
            }

            IterateCoordinatePrefixes(IterateStruct, file, null);

            ExtendedSave.SetExtendedDataById(file, UniversalAutoResolver.UARExtID, new PluginData
            {
                data = new Dictionary<string, object>
                {
                    ["info"] = resolutionInfo.Select(x => x.Serialize()).ToList()
                }
            });
        }

        [HarmonyPostfix, HarmonyPatch(typeof(ChaFileCoordinate), nameof(ChaFileCoordinate.SaveFile), new[] { typeof(string), typeof(int) })]
        public static void ChaFileCoordinateSaveFilePostHook(ChaFileCoordinate __instance, string path)
        {
            if (DoingImport) return;

            Sideloader.Logger.Log(LogLevel.Debug, $"Reloading coordinate [{path}]");

            var extData = ExtendedSave.GetExtendedDataById(__instance, UniversalAutoResolver.UARExtID);

            var tmpExtInfo = (List<byte[]>)extData.data["info"];
            var extInfo = tmpExtInfo.Select(ResolveInfo.Deserialize).ToList();

            Sideloader.Logger.Log(LogLevel.Debug, $"External info count: {extInfo.Count}");

            if (Sideloader.DebugLogging.Value)
            {
                foreach (ResolveInfo info in extInfo)
                    Sideloader.Logger.Log(LogLevel.Debug, $"External info: {info.GUID} : {info.Property} : {info.Slot}");
            }

            void ResetStructResolveStructure(Dictionary<CategoryProperty, StructValue<int>> propertyDict, object structure, IEnumerable<ResolveInfo> extInfo2, string propertyPrefix = "")
            {
                foreach (var kv in propertyDict)
                {
                    var extResolve = extInfo.FirstOrDefault(x => x.Property == $"{propertyPrefix}{kv.Key.ToString()}");

                    if (extResolve != null)
                        kv.Value.SetMethod(structure, extResolve.LocalSlot);
                }
            }

            IterateCoordinatePrefixes(ResetStructResolveStructure, __instance, extInfo);
        }

        #endregion
    }
}