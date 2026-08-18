using UnityEngine;

namespace MoonDelivery
{
    internal static class LunarCycle
    {
        public static float SunCenter(float absoluteMinute)
        {
            return Mathf.Repeat(absoluteMinute / 1440f + .25f, 1f);
        }

        public static bool IsSunlit(float normalizedX, float absoluteMinute)
        {
            float distance = Mathf.Abs(
                Mathf.Repeat(normalizedX - SunCenter(absoluteMinute) + .5f, 1f) - .5f
            );

            return distance <= .32f;
        }
    }
}
