using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace MoonDelivery
{
    public static class WebStorageSync
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void MoonDelivery_SyncFileSystem();
#endif

        public static void Flush()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                MoonDelivery_SyncFileSystem();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Moon Delivery: не удалось запустить синхронизацию IndexedDB: {exception.Message}"
                );
            }
#endif
        }
    }

    public sealed class JsonGameStorage : IGameStorage
    {
        private static string SavePath =>
            Path.Combine(Application.persistentDataPath, "moon_delivery_save.json");

        public void Save(GameState state)
        {
            if (state == null)
                return;
            try
            {
                File.WriteAllText(SavePath, JsonUtility.ToJson(state, true));
                WebStorageSync.Flush();
            }
            catch (Exception exception)
            {
                Debug.LogError($"Moon Delivery: не удалось сохранить игру: {exception.Message}");
            }
        }

        public GameState Load()
        {
            if (!File.Exists(SavePath))
                return null;
            try
            {
                return JsonUtility.FromJson<GameState>(File.ReadAllText(SavePath));
            }
            catch
            {
                return null;
            }
        }

        public void Delete()
        {
            try
            {
                if (!File.Exists(SavePath))
                    return;
                File.Delete(SavePath);
                WebStorageSync.Flush();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"Moon Delivery: не удалось удалить сохранение: {exception.Message}"
                );
            }
        }
    }

    public static class GameStorage
    {
        private static readonly IGameStorage DefaultStorage = new JsonGameStorage();

        public static void Save(GameState state)
        {
            DefaultStorage.Save(state);
        }

        public static GameState Load()
        {
            return DefaultStorage.Load();
        }

        public static void Delete()
        {
            DefaultStorage.Delete();
        }
    }
}
