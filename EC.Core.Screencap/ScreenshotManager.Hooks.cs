using BepInEx.Harmony;
using ChaCustom;
using Harmony;
using UnityEngine;

namespace EC.Core.Screencap
{
    public partial class ScreenshotManager
    {
        private static class Hooks
        {
            //Chara card Render/Downsample rate.
            private static int CardRenderRate => CardDownscalingRate.Value;

            public static void InstallHooks()
            {
                HarmonyWrapper.PatchAll(typeof(Hooks));
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(GameScreenShot), "Capture")]
            public static bool CapturePreHook()
            {
                //cancel the vanilla screenshot
                return false;
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(CustomCapture), "CreatePng")]
            public static bool pre_CreatePng(ref int createW, ref int createH)
            {
                //Multiply up render resolution.
                createW *= CardRenderRate;
                createH *= CardRenderRate;
                return true;
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(CustomCapture), "CreatePng")]
            public static void post_CreatePng(ref byte[] pngData)
            {
                DownscaleEncoded(ref pngData);
            }

            private static void DownscaleEncoded(ref byte[] encoded)
            {
                if (CardRenderRate <= 1) return;

                //Texture buffer for fullres.
                var t2d = new Texture2D(2, 2);
                t2d.LoadImage(encoded);

                //New width/height after downsampling.
                var nw = t2d.width / CardRenderRate;
                var nh = t2d.height / CardRenderRate;

                //Downsample texture
                encoded = _currentAlphaShot.Lanczos(t2d, nw, nh);
            }
        }
    }
}
