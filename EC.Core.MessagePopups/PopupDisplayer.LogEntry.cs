namespace EC.Core.MessagePopups
{
    public partial class PopupDisplayer
    {
        private sealed class LogEntry
        {
            public LogEntry(string text)
            {
                Text = text;
            }

            public int Count { get; set; }

            public string Text { get; }
        }
    }
}
