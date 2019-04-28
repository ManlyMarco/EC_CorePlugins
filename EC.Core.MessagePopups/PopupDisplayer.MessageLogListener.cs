using BepInEx.Logging;

namespace EC.Core.MessagePopups
{
    public partial class PopupDisplayer
    {
        private sealed class MessageLogListener : ILogListener
        {
            public void Dispose() { }

            public void LogEvent(object sender, LogEventArgs eventArgs)
            {
                if (eventArgs.Level.HasFlag(LogLevel.Message))
                    OnEntryLogged(eventArgs);
            }
        }
    }
}
