using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MoonDelivery
{
    public sealed partial class MoonCanvasUI : MonoBehaviour
    {
        private enum Tab
        {
            Map,
            Garage,
            Journal,
        }

        private MoonGame game;
        private MoonVisualCatalog visuals;
        private MoonAudioController audioController;
        private Canvas canvas;

        [Header("Scene layout")]
        [SerializeField]
        private RectTransform mapScreen;

        [SerializeField]
        private RectTransform garageScreen;

        [SerializeField]
        private RectTransform journalScreen;

        [SerializeField]
        private RectTransform mapViewport;

        [SerializeField]
        private RectTransform ordersContent;

        [SerializeField]
        private RectTransform roversContent;

        [SerializeField]
        private RectTransform ownedContent;

        [SerializeField]
        private RectTransform shopContent;

        [SerializeField]
        private RectTransform journalContent;

        [SerializeField]
        private RectTransform garageDetails;

        [SerializeField]
        private RectTransform planner;
        private MoonCanvasMap map;

        [SerializeField]
        private TMP_Text clockText;

        [SerializeField]
        private TMP_Text moneyText;

        [SerializeField]
        private TMP_Text reputationText;

        [SerializeField]
        private TMP_Text routeText;

        [SerializeField]
        private TMP_Text forecastText;

        [SerializeField]
        private TMP_Text validationText;

        [Header("Scene controls")]
        [SerializeField]
        private Button pauseButton;

        [SerializeField]
        private Button speed1Button;

        [SerializeField]
        private Button speed2Button;

        [SerializeField]
        private Button speed8Button;

        [SerializeField]
        private Button mapTabButton;

        [SerializeField]
        private Button garageTabButton;

        [SerializeField]
        private Button journalTabButton;

        [SerializeField]
        private Button startRouteButton;

        [SerializeField]
        private Button clearRouteButton;

        [SerializeField]
        private Button chargeButton;

        [SerializeField]
        private Button declineButton;
        private GameObject gameOverOverlay;
        private GameObject settingsRoot;
        private Slider musicVolumeSlider;
        private Slider sfxVolumeSlider;
        private Button continueButton;
        private readonly List<RouteStop> plan = new List<RouteStop>();
        private string selectedOrderId,
            selectedRoverId,
            garageRoverId;
        private bool paused;
        private bool settingsOpen;
        private bool pausedBeforeSettings;
        private float speed = 1,
            refreshTimer,
            saveTimer;
        private string ordersSignature,
            roversSignature,
            garageSignature;
        private int journalCount = -1;
        private Tab tab;
        private readonly Dictionary<Texture2D, Sprite> sprites =
            new Dictionary<Texture2D, Sprite>();
        internal MoonGame Game => game;
        internal IReadOnlyList<RouteStop> Plan => plan;

        private void Awake()
        {
            game = new MoonGame();
            visuals = Resources.Load<MoonVisualCatalog>("MoonVisualCatalog");
            audioController =
                gameObject.GetComponent<MoonAudioController>()
                ?? gameObject.AddComponent<MoonAudioController>();
            audioController.Initialize(visuals);
            game.OnCue += audioController.PlayCue;
            canvas = GetComponent<Canvas>();
            if (!ValidateSceneLayout())
            {
                Debug.LogError(
                    "Moon Delivery: статический Canvas UI не настроен в сцене. Выполните Tools/Moon Delivery/Rebuild Scene Canvas.",
                    this
                );
                enabled = false;
                return;
            }
            map =
                mapViewport.GetComponent<MoonCanvasMap>()
                ?? mapViewport.gameObject.AddComponent<MoonCanvasMap>();
            map.Initialize(this, visuals);
            BindSettings();
            WireSceneControls();
            SetTab(Tab.Map);
        }

        private bool ValidateSceneLayout() =>
            canvas != null
            && mapScreen != null
            && garageScreen != null
            && journalScreen != null
            && mapViewport != null
            && ordersContent != null
            && roversContent != null
            && ownedContent != null
            && shopContent != null
            && journalContent != null
            && garageDetails != null
            && planner != null
            && clockText != null
            && moneyText != null
            && reputationText != null
            && routeText != null
            && forecastText != null
            && validationText != null
            && pauseButton != null
            && speed1Button != null
            && speed2Button != null
            && speed8Button != null
            && mapTabButton != null
            && garageTabButton != null
            && journalTabButton != null
            && startRouteButton != null
            && clearRouteButton != null
            && chargeButton != null
            && declineButton != null;

        private void WireSceneControls()
        {
            pauseButton.onClick.AddListener(() =>
            {
                audioController.PlayClick();
                paused = !paused;
                PauseLabel().text = paused ? "Старт" : "Пауза";
            });
            speed1Button.onClick.AddListener(() => SetSpeed(1));
            speed2Button.onClick.AddListener(() => SetSpeed(2));
            speed8Button.onClick.AddListener(() => SetSpeed(8));
            mapTabButton.onClick.AddListener(() => SetTabWithSound(Tab.Map));
            garageTabButton.onClick.AddListener(() => SetTabWithSound(Tab.Garage));
            journalTabButton.onClick.AddListener(() => SetTabWithSound(Tab.Journal));
            startRouteButton.onClick.AddListener(() =>
            {
                audioController.PlayClick();
                StartRoute();
            });
            clearRouteButton.onClick.AddListener(() =>
            {
                audioController.PlayClick();
                plan.Clear();
                RebuildOrders();
                RefreshPlanner();
                map.RebuildRoutes();
            });
            chargeButton.onClick.AddListener(() =>
            {
                audioController.PlayClick();
                Rover rover = game.Rover(selectedRoverId);
                if (rover != null)
                {
                    game.StartBaseCharge(rover);
                    RefreshAll();
                }
            });
            declineButton.onClick.AddListener(() =>
            {
                audioController.PlayClick();
                DeclineSelectedOrder();
            });
        }

        private void BindSettings()
        {
            Transform root = transform.Find("Settings");
            if (root == null)
            {
                Debug.LogWarning("Moon Delivery: панель Settings не найдена на Canvas.", this);
                return;
            }
            settingsRoot = root.gameObject;
            musicVolumeSlider = root.Find("SettingsPanel/Content/Music/Slider")
                ?.GetComponent<Slider>();
            sfxVolumeSlider = root.Find("SettingsPanel/Content/SFX/Slider")?.GetComponent<Slider>();
            continueButton = root.Find("SettingsPanel/Content/Continue")?.GetComponent<Button>();
            ConfigureVolumeSlider(
                musicVolumeSlider,
                audioController.MusicVolume,
                value => audioController.SetMusicVolume(value)
            );
            ConfigureVolumeSlider(
                sfxVolumeSlider,
                audioController.SfxVolume,
                value => audioController.SetSfxVolume(value)
            );
            if (continueButton != null)
                continueButton.onClick.AddListener(() =>
                {
                    audioController.PlayClick();
                    CloseSettings();
                });
            else
                Debug.LogWarning(
                    "Moon Delivery: кнопка Continue не найдена в панели Settings.",
                    this
                );
            settingsRoot.SetActive(false);
        }

        private static void ConfigureVolumeSlider(
            Slider slider,
            float normalizedValue,
            UnityEngine.Events.UnityAction<float> onChanged
        )
        {
            if (slider == null)
                return;
            slider.minValue = 0;
            slider.maxValue = 100;
            slider.wholeNumbers = false;
            slider.SetValueWithoutNotify(Mathf.Clamp01(normalizedValue) * 100f);
            slider.onValueChanged.AddListener(value => onChanged(value / 100f));
        }

        private void ToggleSettings()
        {
            audioController.PlayClick();
            if (settingsOpen)
                CloseSettings();
            else
                OpenSettings();
        }

        private void OpenSettings()
        {
            if (settingsRoot == null)
                return;
            pausedBeforeSettings = paused;
            paused = true;
            settingsOpen = true;
            settingsRoot.SetActive(true);
            PauseLabel().text = "Старт";
        }

        private void CloseSettings()
        {
            if (settingsRoot == null)
                return;
            settingsRoot.SetActive(false);
            settingsOpen = false;
            paused = pausedBeforeSettings;
            PauseLabel().text = paused ? "Старт" : "Пауза";
            audioController.SaveSettings();
        }

        private TMP_Text PauseLabel() => pauseButton.GetComponentInChildren<TMP_Text>();

        private void SetSpeed(float value)
        {
            audioController.PlayClick();
            paused = false;
            speed = value;
            PauseLabel().text = "Пауза";
        }

        private void SetTabWithSound(Tab value)
        {
            audioController.PlayClick();
            SetTab(value);
        }

        private void OnDestroy()
        {
            if (game != null && audioController != null)
                game.OnCue -= audioController.PlayCue;
        }

        private void OnApplicationPause(bool value)
        {
            if (value && game != null)
                game.Save();
            if (value && audioController != null)
                audioController.SaveSettings();
        }

        private void OnApplicationQuit()
        {
            if (game != null)
                game.Save();
            if (audioController != null)
                audioController.SaveSettings();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                ToggleSettings();
            if (!paused && !game.State.gameOver)
                game.Tick(Time.unscaledDeltaTime * speed);
            UpdateRoverBars();
            refreshTimer += Time.unscaledDeltaTime;
            if (refreshTimer >= .35f)
            {
                refreshTimer = 0;
                RefreshStatus();
                map.RefreshDynamic();
            }
            saveTimer += Time.unscaledDeltaTime;
            if (saveTimer >= 10)
            {
                saveTimer = 0;
                game.Save();
            }
        }

        private void SetTab(Tab value)
        {
            tab = value;
            mapScreen.gameObject.SetActive(value == Tab.Map);
            garageScreen.gameObject.SetActive(value == Tab.Garage);
            journalScreen.gameObject.SetActive(value == Tab.Journal);
            RefreshAll();
        }

        private void RefreshAll()
        {
            RebuildOrders();
            RebuildRovers();
            RebuildGarage();
            RebuildJournal();
            RefreshPlanner();
            map.RefreshAll();
            UpdateSignatures();
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            int minute = game.MinuteOfDay;
            clockText.text =
                $"День {game.Day}, {minute / 60:00}:{minute % 60:00} — {(game.IsDaylight() ? "база освещена" : "база в тени")}";
            moneyText.text = $"Кредиты: {game.State.money}";
            reputationText.text = $"Репутация: {game.State.reputation}";
            string nextOrders = OrderSignature();
            if (nextOrders != ordersSignature)
            {
                ordersSignature = nextOrders;
                RebuildOrders();
            }
            UpdateOrderTexts();
            string nextRovers = RoverSignature();
            if (nextRovers != roversSignature)
            {
                roversSignature = nextRovers;
                RebuildRovers();
            }
            if (tab == Tab.Map)
            {
                RefreshPlanner();
                UpdateRoverTexts();
            }
            if (tab == Tab.Garage)
            {
                string nextGarage = GarageSignature();
                if (nextGarage != garageSignature)
                {
                    garageSignature = nextGarage;
                    RebuildGarage();
                }
                UpdateGarageTexts();
            }
            if (tab == Tab.Journal && journalCount != game.State.events.Count)
            {
                journalCount = game.State.events.Count;
                RebuildJournal();
            }
            RefreshGameOver();
        }

        private void UpdateSignatures()
        {
            ordersSignature = OrderSignature();
            roversSignature = RoverSignature();
            garageSignature = GarageSignature();
            journalCount = game.State.events.Count;
        }

        private string OrderSignature() =>
            string.Join(
                "|",
                game.State.orders.Select(x => $"{x.id}:{x.status}:{x.deadlineMinute:0}")
            );

        private string RoverSignature() =>
            string.Join("|", game.State.rovers.Select(x => $"{x.id}:{x.status}"));

        private string GarageSignature() =>
            $"{game.State.money}:{game.State.reputation}:"
            + string.Join("|", game.State.rovers.Select(x => $"{x.id}:{x.status}:{x.level}"));
    }
}
