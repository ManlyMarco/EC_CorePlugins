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
            if (!Utilities.FixesConfig.Wrap(Utilities.ConfigSectionFixes, "Fix process culture",
                "Set process culture to ja-JP, similarly to a locale emulator. Fixes game crashes and lockups on some system locales.", true).Value)
                return;

            var culture = CultureInfo.GetCultureInfo("ja-JP");
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }
    }
}
