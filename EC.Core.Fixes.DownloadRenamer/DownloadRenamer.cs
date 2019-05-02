using BepInEx;
using EC.Core.Internal;

namespace EC.Core.DownloadRenamer
{
    [BepInPlugin(GUID, PluginName, Version)]
    public class DownloadRenamer : BaseUnityPlugin
    {
        public const string PluginName = "Download Renamer";
        public const string GUID = "EC.Core.Fixes.DownloadRenamer";
        public const string Version = Metadata.PluginsVersion;

        private void Start() => Hooks.InstallHooks();
    }
}
