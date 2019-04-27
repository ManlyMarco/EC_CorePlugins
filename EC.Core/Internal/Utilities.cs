using BepInEx.Logging;

namespace EC.Core.Internal
{
    public static class Utilities
    {
        static Utilities()
        {
            LogSource = Logger.CreateLogSource("EC.Core");
        }

        public static ManualLogSource LogSource { get; }
    }
}