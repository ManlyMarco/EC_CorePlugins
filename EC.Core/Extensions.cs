using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

namespace EC.Core
{
    public static class Extensions
    {
        public static T GetOrAddComponent<T>(this GameObject gameObject) where T : Component
        {
            if (gameObject == null) return null;
            return gameObject.GetComponent<T>() ?? gameObject.AddComponent<T>();
        }

        public static T DeepCopy<T>(this T self)
        {
            if (self == null)
                return default(T);

            using (var memoryStream = new MemoryStream())
            {
                var binaryFormatter = new BinaryFormatter();
                binaryFormatter.Serialize(memoryStream, self);
                memoryStream.Position = 0L;
                return (T)binaryFormatter.Deserialize(memoryStream);
            }
        }
    }
}
