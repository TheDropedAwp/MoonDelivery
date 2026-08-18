using UnityEngine;

namespace MoonDelivery
{
    [CreateAssetMenu(menuName = "Moon Delivery/Visual Catalog")]
    public class MoonVisualCatalog : ScriptableObject
    {
        [Header("Interface")]
        public Texture2D mainPanel;
        public Texture2D sidePanel;
        public Texture2D compactPanel;
        public Texture2D selectPanel;
        public Texture2D selectPanelActive;
        public Texture2D grid;
        public Texture2D wideButton;
        public Texture2D squareButton;
        public Texture2D hexButton;
        public Texture2D smallButton;
        public Texture2D titleWide;
        public Texture2D titleSmall;
        public Texture2D sliderTrack;
        public Texture2D sliderHandle;
        public Texture2D homeStationIcon;
        public Texture2D destinationStationIcon;
        public Texture2D chargingStationIcon;

        [Header("Moon")]
        public Texture2D moonBackground;
        public Texture2D[] craters;
        public Texture2D[] mountains;

        [Header("Rover previews")]
        public Texture2D previewStandard;
        public Texture2D previewFast;
        public Texture2D previewHeavy;
        public Texture2D previewOffroad;
        public Texture2D previewSolar;
        public Texture2D previewRescue;

        [Header("Rover map icons")]
        public Texture2D mapStandard;
        public Texture2D mapFast;
        public Texture2D mapHeavy;
        public Texture2D mapOffroad;
        public Texture2D mapSolar;
        public Texture2D mapRescue;

        [Header("Audio")]
        public AudioClip ambientMusic;
        public AudioClip clickSfx;
        public AudioClip confirmSfx;
        public AudioClip notificationSfx;
        public AudioClip successSfx;
        public AudioClip errorSfx;
        public AudioClip breakdownSfx;
        public AudioClip rescueSfx;

        public Texture2D Preview(RoverType type)
        {
            switch (type)
            {
                case RoverType.Fast:
                    return previewFast;
                case RoverType.Heavy:
                    return previewHeavy;
                case RoverType.Offroad:
                    return previewOffroad;
                case RoverType.Solar:
                    return previewSolar;
                default:
                    return previewStandard;
            }
        }

        public Texture2D MapIcon(RoverType type)
        {
            switch (type)
            {
                case RoverType.Fast:
                    return mapFast;
                case RoverType.Heavy:
                    return mapHeavy;
                case RoverType.Offroad:
                    return mapOffroad;
                case RoverType.Solar:
                    return mapSolar;
                default:
                    return mapStandard;
            }
        }
    }
}
