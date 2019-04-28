using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using EC.Core.Internal;

namespace EC.Core.ExtensibleSaveFormat
{
    [BepInPlugin(GUID, "Extended Save", Version)]
    public partial class ExtendedSave : BaseUnityPlugin
    {
        public const string GUID = "EC.Core.ExtensibleSaveFormat";
        public const string Version = Metadata.PluginsVersion;

        private static ManualLogSource _logger;

        /// <summary>
        /// Whether extended data load events should be triggered. Temporarily disable it when extended data will never be used,
        /// for example loading lists of cards.
        /// </summary>
        public static bool LoadEventsEnabled = true;

        internal static WeakKeyDictionary<ChaFile, Dictionary<string, PluginData>> internalCharaDictionary = new WeakKeyDictionary<ChaFile, Dictionary<string, PluginData>>();
        internal static WeakKeyDictionary<ChaFileCoordinate, Dictionary<string, PluginData>> internalCoordinateDictionary = new WeakKeyDictionary<ChaFileCoordinate, Dictionary<string, PluginData>>();

        #region Events

        public delegate void CardEventHandler(ChaFile file);

        public static event CardEventHandler CardBeingSaved;
        public static event CardEventHandler CardBeingLoaded;

        public delegate void CoordinateEventHandler(ChaFileCoordinate file);

        public static event CoordinateEventHandler CoordinateBeingSaved;
        public static event CoordinateEventHandler CoordinateBeingLoaded;

        private void Awake()
        {
            _logger = Logger;
            Hooks.InstallHooks();
        }

        internal static void cardWriteEvent(ChaFile file)
        {
            if (CardBeingSaved == null)
                return;

            foreach (var entry in CardBeingSaved.GetInvocationList())
            {
                var handler = (CardEventHandler) entry;
                try
                {
                    handler.Invoke(file);
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Error, $"Subscriber crash in {nameof(ExtendedSave)}.{nameof(CardBeingSaved)} - {ex}");
                }
            }
        }

        internal static void cardReadEvent(ChaFile file)
        {
            if (!LoadEventsEnabled || CardBeingLoaded == null)
                return;

            foreach (var entry in CardBeingLoaded.GetInvocationList())
            {
                var handler = (CardEventHandler) entry;
                try
                {
                    handler.Invoke(file);
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Error, $"Subscriber crash in {nameof(ExtendedSave)}.{nameof(CardBeingLoaded)} - {ex}");
                }
            }
        }

        internal static void coordinateWriteEvent(ChaFileCoordinate file)
        {
            if (CoordinateBeingSaved == null)
                return;

            foreach (var entry in CoordinateBeingSaved.GetInvocationList())
            {
                var handler = (CoordinateEventHandler) entry;
                try
                {
                    handler.Invoke(file);
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Error, $"Subscriber crash in {nameof(ExtendedSave)}.{nameof(CoordinateBeingSaved)} - {ex}");
                }
            }
        }

        internal static void coordinateReadEvent(ChaFileCoordinate file)
        {
            if (!LoadEventsEnabled || CoordinateBeingLoaded == null)
                return;

            foreach (var entry in CoordinateBeingLoaded.GetInvocationList())
            {
                var handler = (CoordinateEventHandler) entry;
                try
                {
                    handler.Invoke(file);
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Error, $"Subscriber crash in {nameof(ExtendedSave)}.{nameof(CoordinateBeingLoaded)} - {ex}");
                }
            }
        }

        #endregion

        public static Dictionary<string, PluginData> GetAllExtendedData(ChaFile file)
        {
            return internalCharaDictionary.Get(file);
        }

        public static Dictionary<string, PluginData> GetAllExtendedData(ChaFileCoordinate file)
        {
            return internalCoordinateDictionary.Get(file);
        }

        public static PluginData GetExtendedDataById(ChaFile file, string id)
        {
            if (file == null || id == null)
                return null;

            var dict = internalCharaDictionary.Get(file);

            if (dict != null && dict.TryGetValue(id, out var extendedSection))
                return extendedSection;

            return null;
        }

        public static void SetExtendedDataById(ChaFile file, string id, PluginData extendedFormatData)
        {
            var chaDictionary = internalCharaDictionary.Get(file);

            if (chaDictionary == null)
            {
                chaDictionary = new Dictionary<string, PluginData>();
                internalCharaDictionary.Set(file, chaDictionary);
            }

            chaDictionary[id] = extendedFormatData;
        }

        public static PluginData GetExtendedDataById(ChaFileCoordinate file, string id)
        {
            if (file == null || id == null)
                return null;

            var dict = internalCoordinateDictionary.Get(file);

            if (dict != null && dict.TryGetValue(id, out var extendedSection))
                return extendedSection;

            return null;
        }

        public static void SetExtendedDataById(ChaFileCoordinate file, string id, PluginData extendedFormatData)
        {
            var chaDictionary = internalCoordinateDictionary.Get(file);

            if (chaDictionary == null)
            {
                chaDictionary = new Dictionary<string, PluginData>();
                internalCoordinateDictionary.Set(file, chaDictionary);
            }

            chaDictionary[id] = extendedFormatData;
        }
    }
}
