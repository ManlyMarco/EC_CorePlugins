using System.Globalization;
using BepInEx;
using EC.Core.Internal;

namespace EC.Core.Fixes
{
    [BepInPlugin(GUID, "Core Fixes", Version)]
    public class CoreFixes : BaseUnityPlugin
    {
        public const string GUID = "EC.Core.Fixes";
        public const string Version = Metadata.PluginsVersion;

        private void Awake()
        {
            var culture = CultureInfo.GetCultureInfo("ja-JP");
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }
    }
}
