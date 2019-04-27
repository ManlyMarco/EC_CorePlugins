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
    }
}
