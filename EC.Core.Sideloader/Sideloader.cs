﻿using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EC.Core.Internal;
using EC.Core.ResourceRedirector;
using EC.Core.Sideloader.UniversalAutoResolver;
using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Win32;
using UnityEngine;

namespace EC.Core.Sideloader
{
    [BepInDependency(ResourceRedirector.ResourceRedirector.GUID)]
    [BepInDependency(ExtensibleSaveFormat.ExtendedSave.GUID)]
    [BepInPlugin(GUID, PluginName, Version)]
    public class Sideloader : BaseUnityPlugin
    {
        public const string GUID = "EC.Core.Sideloader";
        public const string PluginName = "Sideloader";
        public const string Version = Metadata.PluginsVersion;

        private const string GameName = "emotioncreators";

        protected List<ZipFile> Archives = new List<ZipFile>();

        public static readonly List<Manifest> LoadedManifests = new List<Manifest>();

        protected static Dictionary<string, ZipFile> PngList = new Dictionary<string, ZipFile>();
        protected static HashSet<string> PngFolderList = new HashSet<string>();
        protected static HashSet<string> PngFolderOnlyList = new HashSet<string>();

        internal new static ManualLogSource Logger;

        public static ConfigWrapper<bool> MissingModWarning { get; private set; }
        public static ConfigWrapper<bool> DebugLogging { get; private set; }
        public static ConfigWrapper<bool> DebugResolveInfoLogging { get; private set; }
        public static ConfigWrapper<string> AdditionalModsDirectory { get; private set; }

        public Sideloader()
        {
            Logger = base.Logger;

            //ilmerge
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                if (args.Name == "I18N, Version=2.0.0.0, Culture=neutral, PublicKeyToken=0738eb9f132ed756"
                 || args.Name == "I18N.West, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null")
                    return Assembly.GetExecutingAssembly();

                return null;
            };

            Hooks.InstallHooks();
            UniversalAutoResolver.Hooks.InstallHooks();
            ResourceRedirector.ResourceRedirector.AssetResolvers.Add(RedirectHook);

            MissingModWarning = Config.Wrap("General", "Show missing mod warnings", "Whether missing mod warnings will be displayed on screen. Messages will still be written to the log.", true);
            DebugLogging = Config.Wrap("Debug", "Debug logging", "Enable additional logging useful for debugging issues with Sideloader and sideloader mods.\n\n Warning: Will increase load and save times noticeably and will result in very large log sizes.", false);
            DebugResolveInfoLogging = Config.Wrap("Debug", "Debug resolve info logging", "Enable verbose logging for debugging issues with Sideloader and sideloader mods.\n\n Warning: Will increase game start up time and will result in very large log sizes.", false);

            AdditionalModsDirectory = Config.Wrap("General", "Additional mods directory", "Additional directory to load zipmods from.", FindKoiZipmodDir());

            var modDirectory = Path.Combine(Paths.GameRootPath, "mods");

            if (!Directory.Exists(modDirectory))
                Logger.Log(LogLevel.Warning, "Could not find the mods directory: " + modDirectory);

            if (!string.IsNullOrWhiteSpace(AdditionalModsDirectory.Value) && !Directory.Exists(AdditionalModsDirectory.Value))
                Logger.Log(LogLevel.Warning, "Could not find the additional mods directory specified in config: " + AdditionalModsDirectory.Value);

            LoadModsFromDirectories(modDirectory, AdditionalModsDirectory.Value);
        }

        private static string FindKoiZipmodDir()
        {
            try
            {
                // Don't look for the KK modpack if a copy of it is already installed in EC
                if (Directory.Exists(Path.Combine(Paths.GameRootPath, @"mods\Sideloader Modpack")))
                    return string.Empty;

                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Illusion\Koikatu\koikatu"))
                {
                    if (key?.GetValue("INSTALLDIR") is string dir)
                    {
                        dir = Path.Combine(dir, @"mods\Sideloader Modpack");
                        if (Directory.Exists(dir))
                            return dir;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError("Crash when trying to find Koikatsu mods directory");
                Logger.LogError(e);
            }
            return string.Empty;
        }

        /// <summary>
        /// Check if a mod with specified GUID has been loaded
        /// </summary>
        public bool IsModLoaded(string guid)
        {
            if (guid == null)
                return false;
            return LoadedManifests.Any(x => x.GUID == guid);
        }

        private void LoadModsFromDirectories(params string[] modDirectories)
        {
            string GetRelativeArchiveDir(string archiveDir)
            {
                if (archiveDir.StartsWith(Paths.GameRootPath, StringComparison.OrdinalIgnoreCase))
                    return archiveDir.Substring(Paths.GameRootPath.Length);
                return archiveDir;
            }

            // Look for mods, load their manifests
            var allMods = modDirectories.Where(Directory.Exists).SelectMany(GetZipmodsFromDirectory);

            var archives = new Dictionary<ZipFile, Manifest>();

            foreach (var archivePath in allMods)
            {
                ZipFile archive = null;
                try
                {
                    archive = new ZipFile(archivePath);

                    if (Manifest.TryLoadFromZip(archive, out Manifest manifest) && (manifest.Game.IsNullOrWhiteSpace() || manifest.Game.ToLower() == GameName))
                        archives.Add(archive, manifest);
                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Error, $"Failed to load archive \"{GetRelativeArchiveDir(archivePath)}\" with error: {ex.Message}");
                    Logger.Log(LogLevel.Debug, $"Error details: {ex}");
                    archive?.Close();
                }
            }

            // Handlie duplicate GUIDs and load unique mods
            foreach (var modGroup in archives.GroupBy(x => x.Value.GUID))
            {
                // Order by version if available, else use modified dates (less reliable)
                // If versions match, prefer mods inside folders or with more descriptive names so modpacks are preferred
                var orderedModsQuery = modGroup.All(x => !string.IsNullOrEmpty(x.Value.Version))
                    ? modGroup.OrderByDescending(x => x.Value.Version, new ManifestVersionComparer()).ThenByDescending(x => x.Key.Name.Length)
                    : modGroup.OrderByDescending(x => File.GetLastWriteTime(x.Key.Name));

                var orderedMods = orderedModsQuery.ToList();

                if (orderedMods.Count > 1)
                {
                    var modList = string.Join(", ", orderedMods.Select(x => '"' + GetRelativeArchiveDir(x.Key.Name) + '"').ToArray());
                    Logger.Log(LogLevel.Warning, $"Archives with identical GUIDs detected! Archives: {modList}");
                    Logger.Log(LogLevel.Warning, $"Only \"{GetRelativeArchiveDir(orderedMods[0].Key.Name)}\" will be loaded because it's the newest");

                    // Don't keep the duplicate archives in memory
                    foreach (var dupeMod in orderedMods.Skip(1))
                        dupeMod.Key.Close();
                }

                // Actually load the mods (only one per GUID, the newest one)
                var archive = orderedMods[0].Key;
                var manifest = orderedMods[0].Value;
                try
                {
                    Archives.Add(archive);
                    LoadedManifests.Add(manifest);

                    LoadAllUnityArchives(archive, archive.Name);
                    LoadAllLists(archive, manifest);
                    BuildPngFolderList(archive);

                    var trimmedName = manifest.Name?.Trim();
                    var displayName = !string.IsNullOrEmpty(trimmedName) ? trimmedName : Path.GetFileName(archive.Name);

                    Logger.Log(LogLevel.Info, $"Loaded {displayName} {manifest.Version ?? ""}");
                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Error, $"Failed to load archive \"{GetRelativeArchiveDir(archive.Name)}\" with error: {ex.Message}");
                    Logger.Log(LogLevel.Debug, $"Error details: {ex}");
                }
            }

            UniversalAutoResolver.UniversalAutoResolver.SetResolveInfos(_gatheredResolutionInfos);

            BuildPngOnlyFolderList();
        }

        private static IEnumerable<string> GetZipmodsFromDirectory(string modDirectory)
        {
            Logger.LogInfo("Loading mods from directory: " + modDirectory);
            return Directory.GetFiles(modDirectory, "*", SearchOption.AllDirectories)
                            .Where(x => x.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                                        x.EndsWith(".zipmod", StringComparison.OrdinalIgnoreCase));
        }

        protected void SetPossessNew(ChaListData data)
        {
            for (int i = 0; i < data.lstKey.Count; i++)
            {
                if (data.lstKey[i] == "Possess")
                {
                    foreach (var kv in data.dictList)
                    {
                        kv.Value[i] = "1";
                    }
                    break;
                }
            }
        }

        private readonly List<ResolveInfo> _gatheredResolutionInfos = new List<ResolveInfo>();

        protected void LoadAllLists(ZipFile arc, Manifest manifest)
        {
            List<ZipEntry> BoneList = new List<ZipEntry>();
            foreach (ZipEntry entry in arc)
            {
                if (entry.Name.StartsWith("abdata/list/characustom", StringComparison.OrdinalIgnoreCase) && entry.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var stream = arc.GetInputStream(entry);
                        var chaListData = ListLoader.LoadCSV(stream);

                        SetPossessNew(chaListData);
                        UniversalAutoResolver.UniversalAutoResolver.GenerateResolutionInfo(manifest, chaListData, _gatheredResolutionInfos);
                        ListLoader.ExternalDataList.Add(chaListData);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(LogLevel.Error, $"Failed to load list file \"{entry.Name}\" from archive \"{arc.Name}\" with error: {ex.Message}");
                        Logger.Log(LogLevel.Error, $"Error details: {ex}");
                    }
                }
            }
        }
        /// <summary>
        /// Construct a list of all folders that contain a .png
        /// </summary>
        protected void BuildPngFolderList(ZipFile arc)
        {
            foreach (ZipEntry entry in arc)
            {
                //Only list folders for .pngs in abdata folder
                //i.e. skip preview pics or character cards that might be included with the mod
                if (entry.Name.StartsWith("abdata/", StringComparison.OrdinalIgnoreCase) && entry.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    string assetBundlePath = entry.Name;
                    assetBundlePath = assetBundlePath.Remove(0, assetBundlePath.IndexOf('/') + 1); //Remove "abdata/"

                    //Make a list of all the .png files and archive they come from
                    if (PngList.ContainsKey(entry.Name))
                        Logger.Log(LogLevel.Warning, $"Duplicate asset detected! {assetBundlePath}");
                    else
                        PngList.Add(entry.Name, arc);

                    assetBundlePath = assetBundlePath.Remove(assetBundlePath.LastIndexOf('/')); //Remove the .png filename
                    if (!PngFolderList.Contains(assetBundlePath))
                    {
                        //Make a unique list of all folders that contain a .png
                        PngFolderList.Add(assetBundlePath);
                    }
                }
            }
        }
        /// <summary>
        /// Build a list of folders that contain .pngs but do not match an existing asset bundle
        /// </summary>
        protected void BuildPngOnlyFolderList()
        {
            foreach (string folder in PngFolderList) //assetBundlePath
            {
                string assetBundlePath = folder + ".unity3d";

                //The file exists at this location, no need to add a bundle
                if (File.Exists(Application.dataPath + "/../abdata/" + assetBundlePath))
                    continue;

                //Bundle has already been added by LoadAllUnityArchives
                if (BundleManager.Bundles.ContainsKey(assetBundlePath))
                    continue;

                PngFolderOnlyList.Add(folder);
            }
        }
        /// <summary>
        /// Check whether the asset bundle matches a folder that contains .png files and does not match an existing asset bundle
        /// </summary>
        public static bool IsPngFolderOnly(string assetBundleName)
        {
            var extStart = assetBundleName.LastIndexOf('.');
            var trimmedName = extStart >= 0 ? assetBundleName.Remove(extStart) : assetBundleName;
            return PngFolderOnlyList.Contains(trimmedName);
        }
        /// <summary>
        /// Check whether the .png file comes from a sideloader mod
        /// </summary>
        public static bool IsPng(string pngFile) => PngList.ContainsKey(pngFile);

        private static readonly MethodInfo locateZipEntryMethodInfo = typeof(ZipFile).GetMethod("LocateEntry", BindingFlags.NonPublic | BindingFlags.Instance);

        protected void LoadAllUnityArchives(ZipFile arc, string archiveFilename)
        {
            foreach (ZipEntry entry in arc)
            {
                if (entry.Name.EndsWith(".unity3d", StringComparison.OrdinalIgnoreCase))
                {
                    string assetBundlePath = entry.Name;

                    if (assetBundlePath.Contains('/'))
                        assetBundlePath = assetBundlePath.Remove(0, assetBundlePath.IndexOf('/') + 1);

                    AssetBundle getBundleFunc()
                    {
                        AssetBundle bundle;

                        if (entry.CompressionMethod == CompressionMethod.Stored)
                        {
                            long index = (long)locateZipEntryMethodInfo.Invoke(arc, new object[] { entry });

                            if (DebugLogging.Value)
                                Logger.Log(LogLevel.Debug, $"Streaming {entry.Name} ({archiveFilename}) unity3d file from disk, offset {index}");

                            bundle = AssetBundle.LoadFromFile(archiveFilename, 0, (ulong)index);
                        }
                        else
                        {
                            Logger.Log(LogLevel.Debug, $"Cannot stream {entry.Name} ({archiveFilename}) unity3d file from disk, loading to RAM instead");
                            var stream = arc.GetInputStream(entry);

                            byte[] buffer = new byte[entry.Size];

                            stream.Read(buffer, 0, (int)entry.Size);

                            BundleManager.RandomizeCAB(buffer);

                            bundle = AssetBundle.LoadFromMemory(buffer);
                        }

                        if (bundle == null)
                        {
                            Logger.Log(LogLevel.Error, $"Asset bundle \"{entry.Name}\" ({Path.GetFileName(archiveFilename)}) failed to load. It might have a conflicting CAB string.");
                        }

                        return bundle;
                    }

                    BundleManager.AddBundleLoader(getBundleFunc, assetBundlePath, out string warning);

                    if (!string.IsNullOrEmpty(warning))
                        Logger.Log(LogLevel.Warning, $"{warning}");
                }
            }
        }

        protected bool RedirectHook(string assetBundleName, string assetName, Type type, string manifestAssetBundleName, out AssetBundleLoadAssetOperation result)
        {
            string zipPath = $"{manifestAssetBundleName ?? "abdata"}/{assetBundleName.Replace(".unity3d", "")}/{assetName}";

            if (type == typeof(Texture2D))
            {
                zipPath = $"{zipPath}.png";

                //Only search the archives for a .png that can actually be found
                if (PngList.TryGetValue(zipPath, out ZipFile archive))
                {
                    var entry = archive.GetEntry(zipPath);

                    if (entry != null)
                    {
                        var stream = archive.GetInputStream(entry);

                        var tex = ResourceRedirector.AssetLoader.LoadTexture(stream, (int)entry.Size);

                        if (zipPath.Contains("clamp"))
                            tex.wrapMode = TextureWrapMode.Clamp;
                        else if (zipPath.Contains("repeat"))
                            tex.wrapMode = TextureWrapMode.Repeat;

                        result = new AssetBundleLoadAssetOperationSimulation(tex);
                        return true;
                    }
                }
            }

            if (BundleManager.TryGetObjectFromName(assetName, assetBundleName, type, out UnityEngine.Object obj))
            {
                result = new AssetBundleLoadAssetOperationSimulation(obj);
                return true;
            }

            result = null;
            return false;
        }
    }
}
