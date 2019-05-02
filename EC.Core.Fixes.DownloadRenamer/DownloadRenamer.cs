using BepInEx;
using BepInEx.Configuration;
using EC.Core.Internal;

namespace EC.Core.DownloadRenamer
{
    [BepInPlugin(GUID, PluginName, Version)]
    public class DownloadRenamer : BaseUnityPlugin
    {
        public const string PluginName = "Download Renamer";
        public const string GUID = "EC.Core.Fixes.DownloadRenamer";
        public const string Version = Metadata.PluginsVersion;
        public static ConfigWrapper<bool> EnambleRenaming { get; private set; }

        private void Start()
        {
            Hooks.InstallHooks();
            EnambleRenaming = Config.Wrap("Config", "Enamble Renaming", "When enabled, maps, scenes, poses, and characters downloaded in game will have their file names changed to match the ones on the Illusion website.", true);
        }
    }
}
