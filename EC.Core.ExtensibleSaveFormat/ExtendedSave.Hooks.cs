using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Harmony;
using BepInEx.Logging;
using ChaCustom;
using Harmony;
using HEdit;

namespace EC.Core.ExtensibleSaveFormat
{
    public partial class ExtendedSave
    {
        private static class Hooks
        {
            public static readonly string Marker = "KKEx";
            public static readonly int Version = 3;

            public static void InstallHooks()
            {
                HarmonyWrapper.PatchAll(typeof(Hooks));
            }

            #region ChaFile

            #region Loading

            public static void ChaFileLoadFileHook(ChaFile file, BlockHeader header, BinaryReader reader)
            {
                var info = header.SearchInfo(Marker);

                if (info != null && info.version == Version.ToString())
                {
                    var originalPosition = reader.BaseStream.Position;
                    try
                    {
                        var basePosition = originalPosition - header.lstInfo.Sum(x => x.size);

                        reader.BaseStream.Position = basePosition + info.pos;

                        var data = reader.ReadBytes((int)info.size);

                        reader.BaseStream.Position = originalPosition;

                        var dictionary = MessagePackDeserialize<Dictionary<string, PluginData>>(data);
                        _internalCharaDictionary.Set(file, dictionary);
                    }
                    catch (Exception e)
                    {
                        _internalCharaDictionary.Set(file, new Dictionary<string, PluginData>());
                        _logger.Log(LogLevel.Warning, $"Invalid or corrupted extended data in card \"{file.charaFileName}\" - {e.Message}");
                        reader.BaseStream.Position = originalPosition;
                    }
                }
                else
                {
                    _internalCharaDictionary.Set(file, new Dictionary<string, PluginData>());
                }

                CardReadEvent(file);
            }

            [HarmonyTranspiler]
            [HarmonyPatch(typeof(ChaFile), "LoadFile", typeof(BinaryReader), typeof(int), typeof(bool), typeof(bool))]
            public static IEnumerable<CodeInstruction> ChaFileLoadFileTranspiler(IEnumerable<CodeInstruction> instructions)
            {
                var newInstructionSet = new List<CodeInstruction>(instructions);

                //get the index of the first searchinfo call
                var searchInfoIndex = newInstructionSet.FindIndex(instruction => CheckCallVirtName(instruction, "SearchInfo"));

                //get the index of the last seek call
                var lastSeekIndex = newInstructionSet.FindLastIndex(instruction => CheckCallVirtName(instruction, "Seek"));

                var blockHeaderLocalBuilder = (LocalBuilder)newInstructionSet[searchInfoIndex - 2].operand; //get the localbuilder for the blockheader

                //insert our own hook right after the last seek
                newInstructionSet.InsertRange(
                    lastSeekIndex + 2, //we insert AFTER the NEXT instruction, which is right before the try block exit
                    new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0), //push the ChaFile instance
                        new CodeInstruction(OpCodes.Ldloc_S, blockHeaderLocalBuilder), //push the BlockHeader instance
                        new CodeInstruction(OpCodes.Ldarg_1, blockHeaderLocalBuilder), //push the binaryreader instance
                        new CodeInstruction(OpCodes.Call, typeof(Hooks).GetMethod(nameof(ChaFileLoadFileHook))) //call our hook
                    });

                return newInstructionSet;
            }

            #endregion

            #region ImportingKK

            [HarmonyPostfix]
            [HarmonyPatch(typeof(ConvertChaFile), nameof(ConvertChaFile.ConvertCharaFile))]
            public static void ConvertChaFilePostHook(ChaFileControl cfc, KoikatsuCharaFile.ChaFile kkfile)
            {
                // Move data from import dictionary to normal dictionary before the imported cards are saved so the imported data is written
                var result = _internalCharaImportDictionary.Get(kkfile);
                if (result != null)
                {
                    CardImportEvent(result);
                    _internalCharaDictionary.Set(cfc, result);
                }
            }

            public static void KKChaFileLoadFileHook(KoikatsuCharaFile.ChaFile file, BlockHeader header, BinaryReader reader)
            {
                var info = header.SearchInfo(Marker);

                if (info != null && info.version == Version.ToString())
                {
                    var originalPosition = reader.BaseStream.Position;
                    var basePosition = originalPosition - header.lstInfo.Sum(x => x.size);

                    reader.BaseStream.Position = basePosition + info.pos;

                    var data = reader.ReadBytes((int)info.size);

                    reader.BaseStream.Position = originalPosition;

                    try
                    {
                        var dictionary = MessagePackDeserialize<Dictionary<string, PluginData>>(data);
                        _internalCharaImportDictionary.Set(file, dictionary);
                    }
                    catch (Exception e)
                    {
                        _logger.Log(LogLevel.Warning, $"Invalid or corrupted extended data in card \"{file.charaFileName}\" - {e.Message}");
                        reader.BaseStream.Position = originalPosition;
                    }
                }
            }

            [HarmonyTranspiler]
            [HarmonyPatch(typeof(KoikatsuCharaFile.ChaFile), "LoadFile", typeof(BinaryReader), typeof(bool), typeof(bool))]
            public static IEnumerable<CodeInstruction> KKChaFileLoadFileTranspiler(IEnumerable<CodeInstruction> instructions)
            {
                var newInstructionSet = new List<CodeInstruction>(instructions);

                //get the index of the first searchinfo call
                var searchInfoIndex = newInstructionSet.FindIndex(instruction => CheckCallVirtName(instruction, "SearchInfo"));

                //get the index of the last seek call
                var lastSeekIndex = newInstructionSet.FindLastIndex(instruction => CheckCallVirtName(instruction, "Seek"));

                var blockHeaderLocalBuilder = (LocalBuilder)newInstructionSet[searchInfoIndex - 2].operand; //get the localbuilder for the blockheader

                //insert our own hook right after the last seek
                newInstructionSet.InsertRange(
                    lastSeekIndex + 2, //we insert AFTER the NEXT instruction, which is right before the try block exit
                    new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0), //push the ChaFile instance
                        new CodeInstruction(OpCodes.Ldloc_S, blockHeaderLocalBuilder), //push the BlockHeader instance
                        new CodeInstruction(OpCodes.Ldarg_1, blockHeaderLocalBuilder), //push the binaryreader instance
                        new CodeInstruction(OpCodes.Call, typeof(Hooks).GetMethod(nameof(KKChaFileLoadFileHook))) //call our hook
                    });

                return newInstructionSet;
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(KoikatsuCharaFile.ChaFile), "LoadFile", typeof(BinaryReader), typeof(bool), typeof(bool))]
            public static void KKChaFileLoadFilePostHook(KoikatsuCharaFile.ChaFile __instance, bool __result, BinaryReader br, bool noLoadPNG, bool noLoadStatus)
            {
                if (!__result)
                    return;

                //Compatibility for ver 1 and 2 ext save data
                if (br.BaseStream.Position != br.BaseStream.Length)
                {
                    long originalPosition = br.BaseStream.Position;

                    try
                    {
                        string marker = br.ReadString();
                        int version = br.ReadInt32();

                        if (marker == "KKEx" && version == 2)
                        {
                            int length = br.ReadInt32();

                            if (length > 0)
                            {
                                byte[] bytes = br.ReadBytes(length);
                                var dictionary = MessagePackDeserialize<Dictionary<string, PluginData>>(bytes);

                                _internalCharaImportDictionary.Set(__instance, dictionary);
                            }
                        }
                        else
                        {
                            br.BaseStream.Position = originalPosition;
                        }
                    }
                    catch (EndOfStreamException)
                    {
                        /* Incomplete/non-existant data */
                        br.BaseStream.Position = originalPosition;
                    }
                    catch (SystemException)
                    {
                        /* Invalid/unexpected deserialized data */
                        br.BaseStream.Position = originalPosition;
                    }
                }
            }

            #endregion

            #region Saving

            private static byte[] currentlySavingData;

            [HarmonyPrefix]
            [HarmonyPatch(typeof(ChaFile), "SaveFile", typeof(BinaryWriter), typeof(bool), typeof(int))]
            public static void ChaFileSaveFilePreHook(ChaFile __instance, bool __result, BinaryWriter bw, bool savePng, int lang)
            {
                CardWriteEvent(__instance);
            }

            public static void ChaFileSaveFileHook(ChaFile file, BlockHeader header, ref long[] array3)
            {
                var extendedData = GetAllExtendedData(file);
                if (extendedData == null || extendedData.Count == 0)
                {
                    currentlySavingData = null;
                    return;
                }

                try
                {
                    currentlySavingData = MessagePackSerialize(extendedData);
                    if (currentlySavingData.LongLength == 0)
                    {
                        currentlySavingData = null;
                        return;
                    }
                }
                catch (Exception e)
                {
                    _logger.Log(LogLevel.Warning, $"Failed to save extended data in card. {e.Message}");
                    currentlySavingData = null;
                    return;
                }

                //get offset
                var offset = array3.Sum();
                var length = currentlySavingData.LongLength;

                //insert our custom data length at the end
                Array.Resize(ref array3, array3.Length + 1);
                array3[array3.Length - 1] = length;

                //add info about our data to the block header
                var info = new BlockHeader.Info
                {
                    name = Marker,
                    version = Version.ToString(),
                    pos = offset,
                    size = length
                };

                header.lstInfo.Add(info);
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(ChaFile), "SaveFile", typeof(BinaryWriter), typeof(bool), typeof(int))]
            public static void ChaFileSaveFilePostHook(ChaFile __instance, bool __result, BinaryWriter bw, bool savePng, int lang)
            {
                if (!__result || currentlySavingData == null || currentlySavingData.LongLength == 0)
                    return;

                var originalLength = bw.BaseStream.Length;
                var originalPosition = bw.BaseStream.Position;
                try
                {
                    bw.Write(currentlySavingData);
                }
                catch (Exception e)
                {
                    _logger.Log(LogLevel.Warning, $"Failed to save extended data in card. {e.Message}");
                    bw.BaseStream.Position = originalPosition;
                    bw.BaseStream.SetLength(originalLength);
                }
            }

            [HarmonyTranspiler]
            [HarmonyPatch(typeof(ChaFile), "SaveFile", typeof(BinaryWriter), typeof(bool), typeof(int))]
            public static IEnumerable<CodeInstruction> ChaFileSaveFileTranspiler(IEnumerable<CodeInstruction> instructions)
            {
                var newInstructionSet = new List<CodeInstruction>(instructions);

                //get the index of the last blockheader creation
                var blockHeaderIndex = newInstructionSet.FindLastIndex(instruction => CheckNewObjTypeName(instruction, "BlockHeader"));

                //get the index of array3 (which contains data length info)
                var array3Index = newInstructionSet.FindIndex(
                    instruction =>
                    {
                        //find first int64 array
                        return instruction.opcode == OpCodes.Newarr &&
                               instruction.operand.ToString() == "System.Int64";
                    });

                var blockHeaderLocalBuilder = (LocalBuilder)newInstructionSet[blockHeaderIndex + 1].operand; //get the local index for the block header
                var array3LocalBuilder = (LocalBuilder)newInstructionSet[array3Index + 1].operand; //get the local index for array3

                //insert our own hook right after the blockheader creation
                newInstructionSet.InsertRange(
                    blockHeaderIndex + 2, //we insert AFTER the NEXT instruction, which is the store local for the blockheader
                    new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0), //push the ChaFile instance
                        new CodeInstruction(OpCodes.Ldloc_S, blockHeaderLocalBuilder), //push the BlockHeader instance 
                        new CodeInstruction(OpCodes.Ldloca_S, array3LocalBuilder), //push the array3 instance as ref
                        new CodeInstruction(OpCodes.Call, typeof(Hooks).GetMethod(nameof(ChaFileSaveFileHook))) //call our hook
                    });

                return newInstructionSet;
            }

            #endregion

            #endregion

            #region ChaFileCoordinate

            #region Loading

            [HarmonyTranspiler]
            [HarmonyPatch(typeof(ChaFileCoordinate), nameof(ChaFileCoordinate.LoadFile), typeof(Stream), typeof(int))]
            public static IEnumerable<CodeInstruction> ChaFileCoordinateLoadTranspiler(IEnumerable<CodeInstruction> instructions)
            {
                var instructionList = instructions.ToList();

                var usingFinishIndex = instructionList.FindIndex(instruction => instruction.opcode == OpCodes.Leave);
                while (usingFinishIndex > 0)
                {
                    instructionList.InsertRange(usingFinishIndex, new[]
                    {
                        // Load instance of ChaFileCoordinate
                        new CodeInstruction(OpCodes.Ldarg_0),
                        // Load BinaryReader local var
                        new CodeInstruction(OpCodes.Ldloc_0),
                        new CodeInstruction(OpCodes.Call, typeof(Hooks).GetMethod(nameof(ChaFileCoordinateLoadHook)))

                    });

                    usingFinishIndex = instructionList.FindIndex(usingFinishIndex + 4, instruction => instruction.opcode == OpCodes.Leave);
                }

                return instructionList;
            }

            public static void ChaFileCoordinateLoadHook(ChaFileCoordinate coordinate, BinaryReader br)
            {
                var originalPosition = br.BaseStream.Position;
                try
                {
                    var marker = br.ReadString();
                    var version = br.ReadInt32();
                    var length = br.ReadInt32();
                    if (marker == Marker && version == Version && length > 0)
                    {
                        var bytes = br.ReadBytes(length);
                        var dictionary = MessagePackDeserialize<Dictionary<string, PluginData>>(bytes);
                        _internalCoordinateDictionary.Set(coordinate, dictionary);
                    }
                    else
                    {
                        _internalCoordinateDictionary.Set(coordinate, new Dictionary<string, PluginData>());
                        br.BaseStream.Position = originalPosition;
                    }
                }
                catch (EndOfStreamException)
                {
                    /* Incomplete/non-existant data */
                    _internalCoordinateDictionary.Set(coordinate, new Dictionary<string, PluginData>());
                    br.BaseStream.Position = originalPosition;
                }
                catch (InvalidOperationException)
                {
                    /* Invalid/unexpected deserialized data */
                    _internalCoordinateDictionary.Set(coordinate, new Dictionary<string, PluginData>());
                    br.BaseStream.Position = originalPosition;
                }

                CoordinateReadEvent(coordinate);
            }

            #endregion

            #region ImportKK

            [HarmonyPostfix]
            [HarmonyPatch(typeof(ConvertChaFile), nameof(ConvertChaFile.ConvertCoordinateFile))]
            public static void ConvertCoordinateFile(ChaFileCoordinate emcoorde, KoikatsuCharaFile.ChaFileCoordinate kkcoorde)
            {
                // Move data from import dictionary to normal dictionary before the imported cards are saved so the imported data is written
                var result = _internalCoordinateImportDictionary.Get(kkcoorde);
                if (result != null)
                {
                    CoordinateImportEvent(result);
                    _internalCoordinateDictionary.Set(emcoorde, result);
                }
            }

            [HarmonyTranspiler]
            [HarmonyPatch(typeof(KoikatsuCharaFile.ChaFileCoordinate), nameof(KoikatsuCharaFile.ChaFileCoordinate.LoadFile), typeof(Stream), typeof(bool))]
            public static IEnumerable<CodeInstruction> KKChaFileCoordinateLoadTranspiler(IEnumerable<CodeInstruction> instructions)
            {
                var instructionList = instructions.ToList();

                var usingFinishIndex = instructionList.FindIndex(instruction => instruction.opcode == OpCodes.Leave);
                while (usingFinishIndex > 0)
                {
                    instructionList.InsertRange(usingFinishIndex, new[]
                    {
                        // Load instance of ChaFileCoordinate
                        new CodeInstruction(OpCodes.Ldarg_0),
                        // Load BinaryReader local var
                        new CodeInstruction(OpCodes.Ldloc_0),
                        new CodeInstruction(OpCodes.Call, typeof(Hooks).GetMethod(nameof(KKChaFileCoordinateLoadHook)))

                    });

                    usingFinishIndex = instructionList.FindIndex(usingFinishIndex + 4, instruction => instruction.opcode == OpCodes.Leave);
                }

                return instructionList;
            }

            public static void KKChaFileCoordinateLoadHook(KoikatsuCharaFile.ChaFileCoordinate coordinate, BinaryReader br)
            {
                var originalPosition = br.BaseStream.Position;
                try
                {
                    var marker = br.ReadString();
                    var version = br.ReadInt32();
                    var length = br.ReadInt32();
                    if (marker == Marker && version == Version && length > 0)
                    {
                        var bytes = br.ReadBytes(length);
                        var dictionary = MessagePackDeserialize<Dictionary<string, PluginData>>(bytes);
                        _internalCoordinateImportDictionary.Set(coordinate, dictionary);
                    }
                    else
                    {
                        br.BaseStream.Position = originalPosition;
                    }
                }
                catch (EndOfStreamException)
                {
                    /* Incomplete/non-existant data */
                    br.BaseStream.Position = originalPosition;
                }
                catch (InvalidOperationException)
                {
                    /* Invalid/unexpected deserialized data */
                    br.BaseStream.Position = originalPosition;
                }
            }

            #endregion

            #region Saving

            [HarmonyTranspiler]
            [HarmonyPatch(typeof(ChaFileCoordinate), nameof(ChaFileCoordinate.SaveFile), typeof(string), typeof(int))]
            public static IEnumerable<CodeInstruction> ChaFileCoordinateSaveTranspiler(IEnumerable<CodeInstruction> instructions)
            {
                var hooked = false;
                var instructionsList = instructions.ToList();
                for (var i = 0; i < instructionsList.Count; i++)
                {
                    var inst = instructionsList[i];
                    yield return inst;
                    if (!hooked && inst.opcode == OpCodes.Callvirt && instructionsList[i + 1].opcode == OpCodes.Leave) //find the end of the using(BinaryWriter) block
                    {
                        yield return new CodeInstruction(OpCodes.Ldarg_0); //push the ChaFileInstance
                        yield return new CodeInstruction(instructionsList[i - 2]); //push the BinaryWriter (copying the instruction to do so)
                        yield return new CodeInstruction(OpCodes.Call, typeof(Hooks).GetMethod(nameof(ChaFileCoordinateSaveHook))); //call our hook
                        hooked = true;
                    }
                }
            }

            public static void ChaFileCoordinateSaveHook(ChaFileCoordinate file, BinaryWriter bw)
            {
                CoordinateWriteEvent(file);

                _logger.Log(LogLevel.Debug, "Coordinate hook!");

                var extendedData = GetAllExtendedData(file);
                if (extendedData == null || extendedData.Count == 0)
                    return;

                var originalLength = bw.BaseStream.Length;
                var originalPosition = bw.BaseStream.Position;
                try
                {
                    var bytes = MessagePackSerialize(extendedData);

                    bw.Write(Marker);
                    bw.Write(Version);
                    bw.Write(bytes.Length);
                    bw.Write(bytes);
                }
                catch (Exception e)
                {
                    _logger.Log(LogLevel.Warning, $"Failed to save extended data in card. {e.Message}");
                    bw.BaseStream.Position = originalPosition;
                    bw.BaseStream.SetLength(originalLength);
                }
            }

            #endregion

            #endregion

            #region HEditData

            #region Loading

            // HEdit.HEditData
            [HarmonyPostfix]
            [HarmonyPatch(typeof(HEditData), nameof(HEditData.Load), new[] { typeof(BinaryReader), typeof(int), typeof(YS_Node.NodeControl), typeof(HEditData.InfoData), typeof(bool) })]
            private static bool HEditDataLoadHook(bool __result, HEditData __instance, ref BinaryReader _reader, ref int _loadKind, ref YS_Node.NodeControl _nodeControl, ref HEditData.InfoData _info, ref bool _isEdit)
            {
                var originalPosition = _reader.BaseStream.Position;
                try
                {
                    var marker = _reader.ReadString();
                    var version = _reader.ReadInt32();
                    var length = _reader.ReadInt32();
                    if (marker == Marker && version == Version && length > 0)
                    {
                        var bytes = _reader.ReadBytes(length);
                        var dictionary = MessagePackDeserialize<Dictionary<string, PluginData>>(bytes);
                        _internalHEditDataDictionary.Set(__instance, dictionary);
                    }
                    else
                    {
                        _internalHEditDataDictionary.Set(__instance, new Dictionary<string, PluginData>());
                        _reader.BaseStream.Position = originalPosition;
                    }
                }
                catch (EndOfStreamException)
                {
                    /* Incomplete/non-existant data */
                    _internalHEditDataDictionary.Set(__instance, new Dictionary<string, PluginData>());
                    _reader.BaseStream.Position = originalPosition;
                }
                catch (InvalidOperationException)
                {
                    /* Invalid/unexpected deserialized data */
                    _internalHEditDataDictionary.Set(__instance, new Dictionary<string, PluginData>());
                    _reader.BaseStream.Position = originalPosition;
                }

                HEditDataReadEvent(__instance);

                return __result;
            }

            #endregion

            #region Saving

            // HEdit.HEditData
            [HarmonyPostfix]
            [HarmonyPatch(typeof(HEditData), nameof(HEditData.Save), new[] { typeof(BinaryWriter), typeof(YS_Node.NodeControl), typeof(bool) })]
            private static bool HEditDataSaveHook(bool __result, HEditData __instance, ref BinaryWriter _writer, ref YS_Node.NodeControl _nodeControl, ref bool _isInitUserID)
            {
                HEditDataWriteEvent(__instance);

                _logger.Log(LogLevel.Debug, "MapInfo hook!");

                var extendedData = GetAllExtendedData(__instance);
                if (extendedData == null || extendedData.Count == 0)
                    return __result;

                var originalLength = _writer.BaseStream.Length;
                var originalPosition = _writer.BaseStream.Position;
                try
                {
                    var bytes = MessagePackSerialize(extendedData);

                    _writer.Write(Marker);
                    _writer.Write(Version);
                    _writer.Write(bytes.Length);
                    _writer.Write(bytes);
                }
                catch (Exception e)
                {
                    _logger.Log(LogLevel.Warning, $"Failed to save extended data in card. {e.Message}");
                    _writer.BaseStream.Position = originalPosition;
                    _writer.BaseStream.SetLength(originalLength);
                }

                return __result;
            }

            #endregion

            #endregion

            #region Helper

            public static bool CheckCallVirtName(CodeInstruction instruction, string name)
            {
                return instruction.opcode == OpCodes.Callvirt &&
                       //need to do reflection fuckery here because we can't access MonoMethod which is the operand type, not MehtodInfo like normal reflection
                       instruction.operand.GetType().GetProperty("Name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetGetMethod().Invoke(instruction.operand, null).ToString() == name;
            }

            public static bool CheckNewObjTypeName(CodeInstruction instruction, string name)
            {
                return instruction.opcode == OpCodes.Newobj &&
                       //need to do reflection fuckery here because we can't access MonoCMethod which is the operand type, not ConstructorInfo like normal reflection
                       instruction.operand.GetType().GetProperty("DeclaringType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetGetMethod().Invoke(instruction.operand, null).ToString() == name;
            }

            #endregion

            #region Extended Data Override Hooks

            //Prevent loading extended data when loading the list of characters in Chara Maker since it is irrelevant here
            [HarmonyPrefix]
            [HarmonyPatch(typeof(CustomCharaFile), "Initialize")]
            public static void CustomScenePreHook()
            {
                LoadEventsEnabled = false;
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(CustomCharaFile), "Initialize")]
            public static void CustomScenePostHook()
            {
                LoadEventsEnabled = true;
            }

            //Prevent loading extended data when loading the list of coordinates in Chara Maker since it is irrelevant here
            [HarmonyPrefix]
            [HarmonyPatch(typeof(CustomCoordinateFile), "Initialize")]
            public static void CustomCoordinatePreHook()
            {
                LoadEventsEnabled = false;
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(CustomCoordinateFile), "Initialize")]
            public static void CustomCoordinatePostHook()
            {
                LoadEventsEnabled = true;
            }

            #endregion
        }
    }
}
