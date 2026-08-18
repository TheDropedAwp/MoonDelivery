using UnityEngine;

namespace MoonDelivery
{
    public static class MoonGameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartGame()
        {
            if (Object.FindFirstObjectByType<MoonCanvasUI>() != null)
                return;
            Debug.LogError(
                "Moon Delivery Canvas отсутствует в сцене. Добавьте сценовый Canvas через Tools/Moon Delivery/Rebuild Scene Canvas."
            );
        }
    }
}
