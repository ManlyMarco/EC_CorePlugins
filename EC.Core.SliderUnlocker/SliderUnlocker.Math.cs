using System.Collections.Generic;
using UnityEngine;

namespace EC.Core.SliderUnlocker
{
    internal static class SliderMath
    {
        public static Vector3 CalculateScale(List<AnimationKeyInfo.AnmKeyInfo> list, float rate)
        {
            var min = list[0].scl;
            var max = list[list.Count - 1].scl;

            return min + (max - min) * rate;
        }

        public static Vector3 CalculatePosition(List<AnimationKeyInfo.AnmKeyInfo> list, float rate)
        {
            var min = list[0].pos;
            var max = list[list.Count - 1].pos;

            return min + (max - min) * rate;
        }

        public static Vector3 CalculateRotation(List<AnimationKeyInfo.AnmKeyInfo> list, float rate)
        {
            var rot1 = list[0].rot;
            var rot2 = list[1].rot;
            var rot3 = list[list.Count - 1].rot;

            var vector = rot2 - rot1;
            var vector2 = rot3 - rot1;

            var xFlag = vector.x >= 0f;
            var yFlag = vector.y >= 0f;
            var zFlag = vector.z >= 0f;

            if (vector2.x > 0f && !xFlag)
                vector2.x -= 360f;
            else if (vector2.x < 0f && xFlag)
                vector2.x += 360f;

            if (vector2.y > 0f && !yFlag)
                vector2.y -= 360f;
            else if (vector2.y < 0f && yFlag)
                vector2.y += 360f;

            if (vector2.z > 0f && !zFlag)
                vector2.z -= 360f;
            else if (vector2.z < 0f && zFlag)
                vector2.z += 360f;

            if (rate < 0f)
                return rot1 - vector2 * Mathf.Abs(rate);

            return rot3 + vector2 * Mathf.Abs(rate - 1f);
        }

        public static Vector3 SafeCalculateRotation(Vector3 original, string name, List<AnimationKeyInfo.AnmKeyInfo> list, float rate)
        {
            if (!(name.StartsWith("cf_a_bust") && name.EndsWith("_size")) && //breast fix
                !(name.Contains("thigh") && name.Contains("01"))) //thigh fix
                return CalculateRotation(list, rate);
            return original;
        }
    }
}
