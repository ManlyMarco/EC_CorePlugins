using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EC.Core.Internal;
using UnityEngine;

namespace EC.Core.MessagePopups
{
    [BepInPlugin(GUID, "Message Popups", Version)]
    public partial class PopupDisplayer : BaseUnityPlugin
    {
        public const string GUID = "EC.Core.MessagePopups";
        public const string Version = Metadata.PluginsVersion;

        private static readonly List<LogEntry> _shownLogLines = new List<LogEntry>();
        private static float _showCounter;
        private static string _shownLogText = string.Empty;
        private GUIStyle _textStyle;

        public PopupDisplayer()
        {
            Enabled = Config.Wrap("General", "Show messages in UI", "Allow plugins to show pop-up messages", true);
            BepInEx.Logging.Logger.Listeners.Add(new MessageLogListener());
        }

        public static ConfigWrapper<bool> Enabled { get; private set; }

        private static void OnEntryLogged(LogEventArgs logEventArgs)
        {
            if (!Enabled.Value) return;

            if ("BepInEx".Equals(logEventArgs.Source.SourceName, StringComparison.Ordinal)) return;

            if (_showCounter <= 0)
                _shownLogLines.Clear();

            _showCounter = Mathf.Max(_showCounter, 7f);

            var logText = logEventArgs.Data?.ToString();
            if (string.IsNullOrEmpty(logText)) return;

            var logEntry = _shownLogLines.Find(x => x.Text.Equals(logText, StringComparison.Ordinal));
            if (logEntry == null)
            {
                logEntry = new LogEntry(logText);
                _shownLogLines.Add(logEntry);

                _showCounter += 0.8f;
            }

            logEntry.Count++;

            var logLines = _shownLogLines.Select(x => x.Count > 1 ? $"{x.Count}x {x.Text}" : x.Text).ToArray();
            _shownLogText = string.Join("\n", logLines);
        }

        private void OnGUI()
        {
            if (_showCounter <= 0) return;

            var textColor = Color.white;
            var outlineColor = Color.black;

            if (_showCounter <= 1)
            {
                textColor.a = _showCounter;
                outlineColor.a = _showCounter;
            }

            if (_textStyle == null)
            {
                _textStyle = new GUIStyle
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 20
                };
            }

            const int xMargin = 40;
            const int yMargin = 20;
            var displayRect = new Rect(xMargin, yMargin, Screen.width - xMargin * 2, Screen.height - yMargin);
            ShadowAndOutline.DrawOutline(displayRect, _shownLogText, _textStyle, outlineColor, textColor, 2);
        }

        private void Update()
        {
            if (_showCounter > 0)
                _showCounter -= Time.deltaTime;
        }
    }
}
