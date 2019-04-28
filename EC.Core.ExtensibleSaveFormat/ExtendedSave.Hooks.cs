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
using MessagePack;

namespace EC.Core.ExtensibleSaveFormat
{
    public partial class ExtendedSave
    {
        private static class Hooks
        {
            public static readonly string Marker = "KKEx";
            public static readonly int Version = 3;

            private static bool cardReadEventCalled;

            public static void InstallHooks()
            {
                HarmonyWrapper.PatchAll(typeof(Hooks));
            }

            #region ChaFile

            #region Loading

            [HarmonyPrefix]
            [HarmonyPatch(typeof(ChaFile), "LoadFile", typeof(BinaryReader), typeof(int), typeof(bool), typeof(bool))]
            public static void ChaFileLoadFilePreHook(ChaFile __instance, BinaryReader br, int lang, bool noLoadPNG, bool noLoadStatus)
            {
                cardReadEventCalled = false;
            }

            public static void ChaFileLoadFileHook(ChaFile file, BlockHeader header, BinaryReader reader)
            {
                var info = header.SearchInfo(Marker);

                if (info != null && info.version == Version.ToString())
                {
                    var originalPosition = reader.BaseStream.Position;
                    var basePosition = originalPosition - header.lstInfo.Sum(x => x.size);

                    reader.BaseStream.Position = basePosition + info.pos;

                    var data = reader.ReadBytes((int) info.size);

                    reader.BaseStream.Position = originalPosition;

                    cardReadEventCalled = true;

                    try
                    {
                        var dictionary = MessagePackSerializer.Deserialize<Dictionary<string, PluginData>>(data);
                        internalCharaDictionary.Set(file, dictionary);
                    }
                    catch (Exception e)
                    {
                        internalCharaDictionary.Set(file, new Dictionary<string, PluginData>());
                        _logger.Log(LogLevel.Warning, $"Invalid or corrupted extended data in card \"{file.charaFileName}\" - {e.Message}");
                    }

                    cardReadEvent(file);
                }
                else
                    internalCharaDictionary.Set(file, new Dictionary<string, PluginData>());
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

                var blockHeaderLocalBuilder = (LocalBuilder) newInstructionSet[searchInfoIndex - 2].operand; //get the localbuilder for the blockheader

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

            [HarmonyPostfix]
            [HarmonyPatch(typeof(ChaFile), "LoadFile", typeof(BinaryReader), typeof(int), typeof(bool), typeof(bool))]
            public static void ChaFileLoadFilePostHook(ChaFile __instance, bool __result, BinaryReader br, int lang, bool noLoadPNG, bool noLoadStatus)
            {
                if (!__result)
                    return;

                //Compatibility for ver 1 and 2 ext save data
                if (br.BaseStream.Position != br.BaseStream.Length)
                {
                    var originalPosition = br.BaseStream.Position;

                    try
                    {
                        var marker = br.ReadString();
                        var version = br.ReadInt32();

                        if (marker == "KKEx" && version == 2)
                        {
                            var length = br.ReadInt32();

                            if (length > 0)
                            {
                                var bytes = br.ReadBytes(length);
                                var dictionary = MessagePackSerializer.Deserialize<Dictionary<string, PluginData>>(bytes);

                                cardReadEventCalled = true;
                                internalCharaDictionary.Set(__instance, dictionary);

                                cardReadEvent(__instance);
                            }
                        }
                        else
                            br.BaseStream.Position = originalPosition;
                    }
                    catch (EndOfStreamException)
                    {
                        /* Incomplete/non-existant data */
                    }
                    catch (SystemException)
                    {
                        /* Invalid/unexpected deserialized data */
                    }
                }

                //If the event wasn't called at this point, it means the card doesn't contain any data, but we still need to call the even for consistency.
                if (cardReadEventCalled == false)
                {
                    internalCharaDictionary.Set(__instance, new Dictionary<string, PluginData>());
                    cardReadEvent(__instance);
                }
            }

            #endregion

            #region Saving

            private static byte[] currentlySavingData;

            [HarmonyPrefix]
            [HarmonyPatch(typeof(ChaFile), "SaveFile", typeof(BinaryWriter), typeof(bool), typeof(int))]
            public static void ChaFileSaveFilePreHook(ChaFile __instance, bool __result, BinaryWriter bw, bool savePng, int lang)
            {
                cardWriteEvent(__instance);
            }

            public static void ChaFileSaveFileHook(ChaFile file, BlockHeader header, ref long[] array3)
            {
                var extendedData = GetAllExtendedData(file);
                if (extendedData == null)
                {
                    currentlySavingData = null;
                    return;
                }

                currentlySavingData = MessagePackSerializer.Serialize(extendedData);

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
                if (!__result || currentlySavingData == null)
                    return;

                bw.Write(currentlySavingData);
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

                var blockHeaderLocalBuilder = (LocalBuilder) newInstructionSet[blockHeaderIndex + 1].operand; //get the local index for the block header
                var array3LocalBuilder = (LocalBuilder) newInstructionSet[array3Index + 1].operand; //get the local index for array3

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
                var set = false;
                var instructionsList = instructions.ToList();
                for (var i = 0; i < instructionsList.Count; i++)
                {
                    var inst = instructionsList[i];
                    if (set == false && inst.opcode == OpCodes.Ldc_I4_1 && instructionsList[i + 1].opcode == OpCodes.Stloc_1 && instructionsList[i + 2].opcode == OpCodes.Leave)
                    {
                        yield return new CodeInstruction(OpCodes.Ldarg_0);
                        yield return new CodeInstruction(OpCodes.Ldloc_0);
                        yield return new CodeInstruction(OpCodes.Call, typeof(Hooks).GetMethod(nameof(ChaFileCoordinateLoadHook)));
                        set = true;
                    }

                    yield return inst;
                }
            }

            public static void ChaFileCoordinateLoadHook(ChaFileCoordinate coordinate, BinaryReader br)
            {
                try
                {
                    var marker = br.ReadString();
                    var version = br.ReadInt32();

                    var length = br.ReadInt32();

                    if (marker == Marker && version == Version && length > 0)
                    {
                        var bytes = br.ReadBytes(length);
                        var dictionary = MessagePackSerializer.Deserialize<Dictionary<string, PluginData>>(bytes);

                        internalCoordinateDictionary.Set(coordinate, dictionary);
                    }
                    else
                        internalCoordinateDictionary.Set(coordinate, new Dictionary<string, PluginData>()); //Overriding with empty data just in case there is some remnant from former loads.
                }
                catch (EndOfStreamException)
                {
                    /* Incomplete/non-existant data */
                    internalCoordinateDictionary.Set(coordinate, new Dictionary<string, PluginData>());
                }
                catch (InvalidOperationException)
                {
                    /* Invalid/unexpected deserialized data */
                    internalCoordinateDictionary.Set(coordinate, new Dictionary<string, PluginData>());
                }
                coordinateReadEvent(coordinate); //Firing the event in any case
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
                coordinateWriteEvent(file);

                _logger.Log(LogLevel.Debug, "Coordinate hook!");

                var extendedData = GetAllExtendedData(file);
                if (extendedData == null)
                    return;

                var data = MessagePackSerializer.Serialize(extendedData);

                bw.Write(Marker);
                bw.Write(Version);
                bw.Write(data.Length);
                bw.Write(data);
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
