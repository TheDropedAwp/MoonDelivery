using UnityEngine;

namespace MoonDelivery
{
    internal sealed class UnityRandomSource : IRandomSource
    {
        public float Value => Random.value;

        public float Range(float minimum, float maximum)
        {
            return Random.Range(minimum, maximum);
        }

        public int Range(int minimum, int maximum)
        {
            return Random.Range(minimum, maximum);
        }
    }
}
