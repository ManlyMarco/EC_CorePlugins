using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EC.Core.ConfigExtensions;
using EC.Core.Internal;
using EC.Core.Screencap.Renderers;
using Illusion.Game;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EC.Core.Screencap
{
    [BepInPlugin(GUID, "Screenshot Manager", Version)]
    public partial class ScreenshotManager : BaseUnityPlugin
    {
        public const string GUID = "EC.Core.Screencap";
        public const string Version = Metadata.PluginsVersion;
        public string ScreenshotDir { get; } = Path.Combine(Paths.GameRootPath, "UserData\\cap\\");

        private const int ScreenshotSizeMax = 4096;
        private const int ScreenshotSizeMin = 2;

        private static AlphaShot2 _currentAlphaShot;

        #region Config properties

        public static SavedKeyboardShortcut KeyCapture { get; private set; }
        public static SavedKeyboardShortcut KeyCaptureAlpha { get; private set; }
        public static SavedKeyboardShortcut KeyCapture360 { get; private set; }
        public static SavedKeyboardShortcut KeyGui { get; private set; }

        [AcceptableValueRange(ScreenshotSizeMin, ScreenshotSizeMax, false)]
        public static ConfigWrapper<int> ResolutionX { get; private set; }

        [AcceptableValueRange(ScreenshotSizeMin, ScreenshotSizeMax, false)]
        public static ConfigWrapper<int> ResolutionY { get; private set; }

        [AcceptableValueList(new object[] { 1024, 2048, 4096, 8192 })]
        public static ConfigWrapper<int> Resolution360 { get; private set; }

        [AcceptableValueRange(1, 4, false)]
        public static ConfigWrapper<int> DownscalingRate { get; private set; }

        [AcceptableValueRange(1, 4, false)]
        public static ConfigWrapper<int> CardDownscalingRate { get; private set; }

        public static ConfigWrapper<bool> CaptureAlpha { get; private set; }

        public static ConfigWrapper<bool> ScreenshotMessage { get; private set; }

        #endregion

        private string GetUniqueFilename()
        {
            return Path.GetFullPath(Path.Combine(ScreenshotDir, $"EC-{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.png"));
        }

        protected void Awake()
        {
            KeyCapture = new SavedKeyboardShortcut(Config, "Take UI screenshot", "Capture a simple \"as you see it\" screenshot of the game. Not affected by settings for rendered screenshots.", new KeyboardShortcut(KeyCode.F9));
            KeyCaptureAlpha = new SavedKeyboardShortcut(Config, "Take rendered screenshot", null, new KeyboardShortcut(KeyCode.F11));
            KeyCapture360 = new SavedKeyboardShortcut(Config, "Take 360 screenshot", "Captures a 360 screenshot around current camera. The created image is in equirectangular format and can be viewed by most 360 image viewers (e.g. Google Cardboard).", new KeyboardShortcut(KeyCode.F11, KeyCode.LeftControl));
            KeyGui = new SavedKeyboardShortcut(Config, "Open settings window", null, new KeyboardShortcut(KeyCode.F11, KeyCode.LeftShift));

            ResolutionX = Config.Wrap("Render Output Resolution", "Horizontal", "Horizontal size (width) of rendered screenshots in pixels. Doesn't affect UI and 360 screenshots.", Screen.width);
            ResolutionY = Config.Wrap("Render Output Resolution", "Vertical", "Vertical size (height) of rendered screenshots in pixels. Doesn't affect UI and 360 screenshots.", Screen.height);
            ResolutionX.SettingChanged += (sender, args) => _resolutionXBuffer = ResolutionX.Value.ToString();
            ResolutionY.SettingChanged += (sender, args) => _resolutionYBuffer = ResolutionY.Value.ToString();

            Resolution360 = Config.Wrap("360 Screenshots", "360 screenshot resolution", "Horizontal resolution (width) of 360 degree/panorama screenshots. Decrease if you have issues. WARNING: Memory usage can get VERY high - 4096 needs around 4GB of free RAM/VRAM to create, 8192 will need much more.", 4096);

            DownscalingRate = Config.Wrap("Render Settings", "Screenshot upsampling ratio", "Capture screenshots in a higher resolution and then downscale them to desired size. Prevents aliasing, perserves small details and gives a smoother result, but takes longer to create.", 2);
            CardDownscalingRate = Config.Wrap("Render Settings", "Card image upsampling ratio", "Capture character card images in a higher resolution and then downscale them to desired size. Prevents aliasing, perserves small details and gives a smoother result, but takes longer to create.", 3);
            CaptureAlpha = Config.Wrap("Render Settings", "Transparency in rendered screenshots", "Replaces background with transparency in rendered image. Works only if there are no 3D objects covering the background (e.g. the map). Works well in character creator and studio.", true);

            ScreenshotMessage = Config.Wrap("General", "Show messages on screen", "Whether screenshot messages will be displayed on screen. Messages will still be written to the log.", true);

            SceneManager.sceneLoaded += (s, a) => InstallSceenshotHandler();
            InstallSceenshotHandler();

            if (!Directory.Exists(ScreenshotDir))
                Directory.CreateDirectory(ScreenshotDir);

            Hooks.InstallHooks();

            I360Render.Init();
        }

        private static void InstallSceenshotHandler()
        {
            if (!Camera.main || !Camera.main.gameObject) return;
            _currentAlphaShot = Camera.main.gameObject.GetOrAddComponent<AlphaShot2>();
        }

        protected void Update()
        {
            if (KeyGui.IsDown())
            {
                _uiShow = !_uiShow;
                _resolutionXBuffer = ResolutionX.Value.ToString();
                _resolutionYBuffer = ResolutionY.Value.ToString();
            }
            else if (KeyCaptureAlpha.IsDown()) StartCoroutine(TakeCharScreenshot());
            else if (KeyCapture.IsDown()) TakeScreenshot();
            else if (KeyCapture360.IsDown()) StartCoroutine(Take360Screenshot());
        }

        private void TakeScreenshot()
        {
            var filename = GetUniqueFilename();

            ScreenCapture.CaptureScreenshot(filename);
            StartCoroutine(TakeScreenshotLog(filename));
        }

        private IEnumerator TakeScreenshotLog(string filename)
        {
            yield return new WaitForEndOfFrame();

            Utils.Sound.Play(SystemSE.photo);
            Logger.Log(ScreenshotMessage.Value ? LogLevel.Message : LogLevel.Info, $"UI screenshot saved to {filename}");
        }

        private IEnumerator TakeCharScreenshot()
        {
            yield return new WaitForEndOfFrame();

            if (_currentAlphaShot != null)
            {
                var filename = GetUniqueFilename();
                File.WriteAllBytes(filename, _currentAlphaShot.Capture(ResolutionX.Value, ResolutionY.Value, DownscalingRate.Value, CaptureAlpha.Value));

                Utils.Sound.Play(SystemSE.photo);
                Logger.Log(ScreenshotMessage.Value ? LogLevel.Message : LogLevel.Info, $"Character screenshot saved to {filename}");
            }
            else
                Logger.Log(LogLevel.Message, "Can't render a screenshot here, try UI screenshot instead");
        }

        private IEnumerator Take360Screenshot()
        {
            yield return new WaitForEndOfFrame();

            try
            {
                var filename = GetUniqueFilename();
                File.WriteAllBytes(filename, I360Render.Capture(Resolution360.Value, false));

                Utils.Sound.Play(SystemSE.photo);
                Logger.Log(ScreenshotMessage.Value ? LogLevel.Message : LogLevel.Info, $"360 screenshot saved to {filename}");
            }
            catch (Exception e)
            {
                Logger.Log(LogLevel.Message | LogLevel.Error, "Failed to take a 360 screenshot - " + e.Message);
                Logger.Log(LogLevel.Error, e.StackTrace);
            }
        }

        #region UI

        private readonly int _uiWindowId = GUID.GetHashCode();
        private Rect _uiRect = new Rect(20, Screen.height / 2 - 150, 160, 223);
        private bool _uiShow;
        private string _resolutionXBuffer = "", _resolutionYBuffer = "";

        protected void OnGUI()
        {
            if (_uiShow)
                _uiRect = GUILayout.Window(_uiWindowId, _uiRect, WindowFunction, "Screenshot settings");
        }

        private void WindowFunction(int windowId)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            {
                GUILayout.Label(
                    "Output resolution (W/H)", new GUIStyle
                    {
                        alignment = TextAnchor.MiddleCenter,
                        normal = new GUIStyleState
                        {
                            textColor = Color.white
                        }
                    });

                GUILayout.BeginHorizontal();
                {
                    GUI.SetNextControlName("X");
                    _resolutionXBuffer = GUILayout.TextField(_resolutionXBuffer);

                    GUILayout.Label(
                        "x", new GUIStyle
                        {
                            alignment = TextAnchor.LowerCenter,
                            normal = new GUIStyleState
                            {
                                textColor = Color.white
                            }
                        }, GUILayout.ExpandWidth(false));

                    GUI.SetNextControlName("Y");
                    _resolutionYBuffer = GUILayout.TextField(_resolutionYBuffer);

                    var focused = GUI.GetNameOfFocusedControl();
                    if (focused != "X" && focused != "Y")
                    {
                        if (!int.TryParse(_resolutionXBuffer, out var x))
                            x = ResolutionX.Value;
                        if (!int.TryParse(_resolutionYBuffer, out var y))
                            y = ResolutionY.Value;
                        _resolutionXBuffer = (ResolutionX.Value = Mathf.Clamp(x, ScreenshotSizeMin, ScreenshotSizeMax)).ToString();
                        _resolutionYBuffer = (ResolutionY.Value = Mathf.Clamp(y, ScreenshotSizeMin, ScreenshotSizeMax)).ToString();
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.Space(2);

                    if (GUILayout.Button("Set to screen size"))
                    {
                        ResolutionX.Value = Screen.width;
                        ResolutionY.Value = Screen.height;
                    }

                    if (GUILayout.Button("Rotate 90 degrees"))
                    {
                        var curerntX = ResolutionX.Value;
                        ResolutionX.Value = ResolutionY.Value;
                        ResolutionY.Value = curerntX;
                    }
                }
                GUILayout.EndVertical();

                GUILayout.BeginVertical(GUI.skin.box);
                {
                    GUILayout.Label(
                        "Screen upsampling rate", new GUIStyle
                        {
                            alignment = TextAnchor.MiddleCenter,
                            normal = new GUIStyleState
                            {
                                textColor = Color.white
                            }
                        });

                    GUILayout.BeginHorizontal();
                    {
                        var downscale = (int) Math.Round(GUILayout.HorizontalSlider(DownscalingRate.Value, 1, 4));

                        GUILayout.Label(
                            $"{downscale}x", new GUIStyle
                            {
                                alignment = TextAnchor.UpperRight,
                                normal = new GUIStyleState
                                {
                                    textColor = Color.white
                                }
                            }, GUILayout.ExpandWidth(false));
                        DownscalingRate.Value = downscale;
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();

                GUILayout.BeginVertical(GUI.skin.box);
                {
                    GUILayout.Label(
                        "Card upsampling rate", new GUIStyle
                        {
                            alignment = TextAnchor.MiddleCenter,
                            normal = new GUIStyleState
                            {
                                textColor = Color.white
                            }
                        });

                    GUILayout.BeginHorizontal();
                    {
                        var carddownscale = (int) Math.Round(GUILayout.HorizontalSlider(CardDownscalingRate.Value, 1, 4));

                        GUILayout.Label(
                            $"{carddownscale}x", new GUIStyle
                            {
                                alignment = TextAnchor.UpperRight,
                                normal = new GUIStyleState
                                {
                                    textColor = Color.white
                                }
                            }, GUILayout.ExpandWidth(false));
                        CardDownscalingRate.Value = carddownscale;
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();

                CaptureAlpha.Value = GUILayout.Toggle(CaptureAlpha.Value, "Transparent background");

                if (GUILayout.Button("Open screenshot dir"))
                    Process.Start(ScreenshotDir);

                GUI.DragWindow();
            }

            #endregion
        }
    }
}
