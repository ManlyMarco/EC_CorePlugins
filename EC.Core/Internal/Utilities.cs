using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace EC.Core.Internal
{
    /// <summary>
    /// Only for internal use in EC.Core
    /// </summary>
    public static class Utilities
    {
        static Utilities()
        {
            LogSource = Logger.CreateLogSource("EC.Core");
            FixesConfig = new ConfigFile(Utility.CombinePaths(Paths.ConfigPath, "EC.Core.Fixes.cfg"), false);
        }

        public static ManualLogSource LogSource { get; }

        public static ConfigFile FixesConfig { get; }
        public const string ConfigSectionFixes = "Bug Fixes";
        public const string ConfigSectionTweaks = "Tweaks";
    }
}