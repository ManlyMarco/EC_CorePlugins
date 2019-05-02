using Harmony;
using System.IO;
using UploaderSystem;

namespace EC.Core.DownloadRenamer
{
    internal static class Hooks
    {
        public static void InstallHooks() => BepInEx.Harmony.HarmonyWrapper.PatchAll(typeof(Hooks));

        [HarmonyPrefix, HarmonyPatch(typeof(NetUIControl), "SaveDownloadFile")]
        public static bool SaveDownloadFilePrefix(byte[] bytes, NetworkInfo.BaseIndex info, NetUIControl __instance)
        {
            string prefix;
            string dir;
            // These names match the web downloader, which are more sensible.
            // The original names reflect the time of download, which is not only redundant
            // information, but also allows redundant copies to be downloaded.
            switch (__instance.dataType)
            {
                case 0:
                    prefix = "emocre_chara_";
                    NetworkInfo.CharaInfo charaInfo = info as NetworkInfo.CharaInfo;
                    dir = charaInfo.sex == 1 ? "chara/female" : "chara/male";
                    break;
                case 1:
                    prefix = "emocre_map_";
                    dir = "map/data";
                    break;
                case 2:
                    prefix = "emocre_pose_";
                    dir = "pose/data";
                    break;
                case 3:
                    prefix = "emocre_scene_";
                    dir = "edit/scene";
                    break;
                default:
                    // Unknown type, fallback to the original function to do it.
                    return true;
            }
            var fileName = UserData.Create(dir) + prefix + info.idx.ToString("D7") + ".png";
            using (FileStream file = new FileStream(fileName, FileMode.Create, FileAccess.Write))
            {
                using (BinaryWriter writer = new BinaryWriter(file))
                {
                    writer.Write(bytes);
                }
            }
            return false;
        }
    }
}
