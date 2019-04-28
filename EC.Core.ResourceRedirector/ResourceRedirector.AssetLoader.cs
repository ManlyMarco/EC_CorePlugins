using BepInEx;
using System.IO;
using UnityEngine;

namespace EC.Core.ResourceRedirector
{
    public static class AssetLoader
    {
        public static AudioClip LoadAudioClip(string path, AudioType type)
        {
            using (WWW loadGachi = new WWW(Utility.ConvertToWWWFormat(path)))
            {
                AudioClip clip = loadGachi.GetAudioClipCompressed(false, type);

                //force single threaded loading instead of using a coroutine
                while (!clip.isReadyToPlay) { }

                return clip;
            }
        }

        public static Texture2D LoadTexture(string path) => LoadTexture(File.ReadAllBytes(path));

        public static Texture2D LoadTexture(Stream stream) => LoadTexture(stream, (int)stream.Length);

        public static Texture2D LoadTexture(Stream stream, int length)
        {
            byte[] buffer = new byte[length];

            stream.Read(buffer, 0, length);

            return LoadTexture(buffer);
        }

        public static Texture2D LoadTexture(byte[] data)
        {
            Texture2D tex = new Texture2D(2, 2);
            ImageConversion.LoadImage(tex, data);

            return tex;
        }
    }
}
