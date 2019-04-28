using System.Globalization;
using BepInEx;
using EC.Core.Internal;

namespace EC.Core.Fixes.CultureFix
{
    [BepInPlugin(GUID, "Culture Fix", Version)]
    public class CultureFix : BaseUnityPlugin
    {
        public const string GUID = "EC.Core.Fixes.CultureFix";
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
