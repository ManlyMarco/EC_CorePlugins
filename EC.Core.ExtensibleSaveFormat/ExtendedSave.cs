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

        private static readonly WeakKeyDictionary<ChaFile, Dictionary<string, PluginData>> _internalCharaDictionary = 
            new WeakKeyDictionary<ChaFile, Dictionary<string, PluginData>>();

        private static readonly WeakKeyDictionary<ChaFileCoordinate, Dictionary<string, PluginData>> _internalCoordinateDictionary = 
            new WeakKeyDictionary<ChaFileCoordinate, Dictionary<string, PluginData>>();

        private static readonly WeakKeyDictionary<KoikatsuCharaFile.ChaFile, Dictionary<string, PluginData>> _internalCharaImportDictionary = 
            new WeakKeyDictionary<KoikatsuCharaFile.ChaFile, Dictionary<string, PluginData>>();

        private static readonly WeakKeyDictionary<KoikatsuCharaFile.ChaFileCoordinate, Dictionary<string, PluginData>> _internalCoordinateImportDictionary = 
            new WeakKeyDictionary<KoikatsuCharaFile.ChaFileCoordinate, Dictionary<string, PluginData>>();

        #region Events

        public delegate void CardEventHandler(ChaFile file);
        public delegate void CoordinateEventHandler(ChaFileCoordinate file);
        public delegate void ImportEventHandler(Dictionary<string, PluginData> importedExtendedData);

        public static event CardEventHandler CardBeingSaved;
        public static event CardEventHandler CardBeingLoaded;
        /// <summary>
        /// Contains all extended data read from the KK card. Key is data GUID.
        /// Convert your data and write it back to the dictionary to get it saved.
        /// </summary>
        public static event ImportEventHandler CardBeingImported;

        public static event CoordinateEventHandler CoordinateBeingSaved;
        public static event CoordinateEventHandler CoordinateBeingLoaded;
        /// <summary>
        /// Contains all extended data read from the KK card. Key is data GUID.
        /// Convert your data and write it back to the dictionary to get it saved.
        /// </summary>
        public static event ImportEventHandler CoordinateBeingImported;

        private void Awake()
        {
            _logger = Logger;
            Hooks.InstallHooks();
        }

        private static void CardWriteEvent(ChaFile file)
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

        private static void CardReadEvent(ChaFile file)
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

        private static void CardImportEvent(Dictionary<string, PluginData> data)
        {
            if (CardBeingImported != null)
            {
                foreach (var entry in CardBeingImported.GetInvocationList())
                {
                    var handler = (ImportEventHandler) entry;
                    try
                    {
                        handler.Invoke(data);
                    }
                    catch (Exception ex)
                    {
                        _logger.Log(LogLevel.Error, $"Subscriber crash in {nameof(ExtendedSave)}.{nameof(CardBeingLoaded)} - {ex}");
                    }
                }
            }
        }

        private static void CoordinateWriteEvent(ChaFileCoordinate file)
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

        private static void CoordinateReadEvent(ChaFileCoordinate file)
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

        private static void CoordinateImportEvent(Dictionary<string, PluginData> data)
        {
            if (CoordinateBeingImported != null)
            {
                foreach (var entry in CoordinateBeingImported.GetInvocationList())
                {
                    var handler = (ImportEventHandler)entry;
                    try
                    {
                        handler.Invoke(data);
                    }
                    catch (Exception ex)
                    {
                        _logger.Log(LogLevel.Error, $"Subscriber crash in {nameof(ExtendedSave)}.{nameof(CardBeingLoaded)} - {ex}");
                    }
                }
            }
        }

        #endregion

        public static Dictionary<string, PluginData> GetAllExtendedData(ChaFile file)
        {
            return _internalCharaDictionary.Get(file);
        }

        public static Dictionary<string, PluginData> GetAllExtendedData(ChaFileCoordinate file)
        {
            return _internalCoordinateDictionary.Get(file);
        }

        public static PluginData GetExtendedDataById(ChaFile file, string id)
        {
            if (file == null || id == null)
                return null;

            var dict = _internalCharaDictionary.Get(file);

            if (dict != null && dict.TryGetValue(id, out var extendedSection))
                return extendedSection;

            return null;
        }

        public static void SetExtendedDataById(ChaFile file, string id, PluginData extendedFormatData)
        {
            var chaDictionary = _internalCharaDictionary.Get(file);

            if (chaDictionary == null)
            {
                chaDictionary = new Dictionary<string, PluginData>();
                _internalCharaDictionary.Set(file, chaDictionary);
            }

            chaDictionary[id] = extendedFormatData;
        }

        public static PluginData GetExtendedDataById(ChaFileCoordinate file, string id)
        {
            if (file == null || id == null)
                return null;

            var dict = _internalCoordinateDictionary.Get(file);

            if (dict != null && dict.TryGetValue(id, out var extendedSection))
                return extendedSection;

            return null;
        }

        public static void SetExtendedDataById(ChaFileCoordinate file, string id, PluginData extendedFormatData)
        {
            var chaDictionary = _internalCoordinateDictionary.Get(file);

            if (chaDictionary == null)
            {
                chaDictionary = new Dictionary<string, PluginData>();
                _internalCoordinateDictionary.Set(file, chaDictionary);
            }

            chaDictionary[id] = extendedFormatData;
        }
    }
}
