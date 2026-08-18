using UnityEngine;

namespace MoonDelivery
{
    public class MoonAudioController : MonoBehaviour
    {
        private const string MusicVolumeKey = "MoonDelivery.MusicVolume";
        private const string SfxVolumeKey = "MoonDelivery.SfxVolume";
        private AudioSource musicSource;
        private AudioSource sfxSource;
        private MoonVisualCatalog catalog;
        private float lastClickTime;

        public float MusicVolume { get; private set; } = 1f;
        public float SfxVolume { get; private set; } = 1f;

        public void Initialize(MoonVisualCatalog visualCatalog)
        {
            catalog = visualCatalog;
            musicSource = gameObject.AddComponent<AudioSource>();
            sfxSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            sfxSource.playOnAwake = false;
            MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, 1f));
            SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, 1f));
            musicSource.volume = MusicVolume;
            sfxSource.volume = SfxVolume;
            if (catalog != null && catalog.ambientMusic != null)
            {
                musicSource.clip = catalog.ambientMusic;
                musicSource.Play();
            }
        }

        public void SetMusicVolume(float value)
        {
            MusicVolume = Mathf.Clamp01(value);
            if (musicSource != null)
                musicSource.volume = MusicVolume;
            PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume);
        }

        public void SetSfxVolume(float value)
        {
            SfxVolume = Mathf.Clamp01(value);
            if (sfxSource != null)
                sfxSource.volume = SfxVolume;
            PlayerPrefs.SetFloat(SfxVolumeKey, SfxVolume);
        }

        public void SaveSettings()
        {
            PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume);
            PlayerPrefs.SetFloat(SfxVolumeKey, SfxVolume);
            PlayerPrefs.Save();
            WebStorageSync.Flush();
        }

        public void PlayClick()
        {
            if (Time.unscaledTime - lastClickTime < .045f)
                return;
            lastClickTime = Time.unscaledTime;
            Play(catalog != null ? catalog.clickSfx : null, .48f);
        }

        public void PlayCue(GameCue cue)
        {
            if (catalog == null)
                return;
            switch (cue)
            {
                case GameCue.Click:
                    PlayClick();
                    break;
                case GameCue.Confirm:
                    Play(catalog.confirmSfx, .65f);
                    break;
                case GameCue.Notification:
                    Play(catalog.notificationSfx, .55f);
                    break;
                case GameCue.Success:
                    Play(catalog.successSfx, .72f);
                    break;
                case GameCue.Breakdown:
                    Play(catalog.breakdownSfx, .8f);
                    break;
                case GameCue.Rescue:
                    Play(catalog.rescueSfx, .7f);
                    break;
                case GameCue.Upgrade:
                    Play(catalog.confirmSfx, .6f, 1.12f);
                    break;
                default:
                    Play(catalog.errorSfx, .7f);
                    break;
            }
        }

        private void Play(AudioClip clip, float volume, float pitch = 1f)
        {
            if (clip == null || sfxSource == null)
                return;
            sfxSource.pitch = pitch;
            sfxSource.PlayOneShot(clip, volume);
        }
    }
}
