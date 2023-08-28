using EFT;
using BepInEx;
using UnityEngine;
using Comfort.Common;
using BepInEx.Logging;
using BepInEx.Configuration;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace LHA
{
    [BepInPlugin("com.kobrakon.LHA", "LHA", "1.0.0")]
    public class LHA : BaseUnityPlugin
    {
        public static GameObject Hook;
        public static AudioSource playerAudioSource;
        private static GameWorld gameWorld;
        private static ManualLogSource logger;
        private static ConfigEntry<float> volume;
        private static ConfigEntry<int> trigger;

        private void Awake()
        {
            logger = Logger;
            Logger.LogInfo($"LHA: Loading");
            volume = Config.Bind("LHA Settings", "Set Alert Tone Volume", 1f, new ConfigDescription("What volume the alert tone should play at", new AcceptableValueRange<float>(0f, 1f)));
            trigger = Config.Bind("LHA Settings", "Set Trigger", 1, new ConfigDescription("The health value you want the alert tone to play at", new AcceptableValueRange<int>(0, 440)));

            Hook = new GameObject("LHA");
            Hook.AddComponent<LHAController>();
            Hook.AddComponent<AudioSource>();
            playerAudioSource = Hook.GetComponent<AudioSource>();
            LHAController.RequestAudio();

            DontDestroyOnLoad(Hook);
        }

        public class LHAController : MonoBehaviour
        {
            void Update()
            {
                gameWorld = Singleton<GameWorld>.Instance;
                if (Ready())
                {
                    playerAudioSource.volume = volume.Value;
                    if (Current() <= Mathf.Floor(trigger.Value) && !playerAudioSource.isPlaying) // round down config value cause slider
                    {
                        playerAudioSource.Play();
                    }
                    else if (Current() > trigger.Value) playerAudioSource.Stop();
                }
                else if (playerAudioSource.isPlaying) playerAudioSource.Stop();
            }

            public static async void RequestAudio()
            {
                string path = "file://" + (BepInEx.Paths.PluginPath + "\\LHA\\alert.wav").Replace("\\", "/");
                using (UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(path, AudioType.WAV))
                {
                    var op = req.SendWebRequest();
                    logger.LogInfo($"LHA Web Request : Retrieving Audio File");

                    while (!op.isDone)
                    {
                        await Task.Yield();
                    }

                    bool Successful = !req.isHttpError || !req.isNetworkError ? true : false;

                    if (!Successful) { logger.LogError("LHA Web Request failed, check file paths, should be laid out as BepInEx/plugins/LHA/alert.wav"); return; }

                    logger.LogInfo("LHA Web Request Successful");

                    playerAudioSource.clip = DownloadHandlerAudioClip.GetContent(req);
                    playerAudioSource.loop = true;
                }
            }
            
            bool Ready() => !Singleton<GameWorld>.Instantiated || gameWorld.MainPlayer == null || gameWorld.AllAlivePlayersList.Count <= 0 || gameWorld.MainPlayer is HideoutPlayer ? false : true;
            float Current() => Ready() ? gameWorld.MainPlayer.HealthController.GetBodyPartHealth(EBodyPart.Common).Current : 9999f; // bullshit floating point number go
        }
    }
}

