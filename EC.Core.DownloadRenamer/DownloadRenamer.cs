using BepInEx;
using EC.Core.Internal;

namespace EC.Core.DownloadRenamer
{
    [BepInPlugin(GUID, PluginName, Version)]
    public class DownloadRenamer : BaseUnityPlugin
    {
        public const string PluginName = "Download Renamer";
        public const string GUID = "2f1827a3-7e2f-4a1d-8c46-24cf81f2d8d7";
        public const string Version = Metadata.PluginsVersion;

        private void Start() => Hooks.InstallHooks();
    }
}
