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
        private static ConfigEntry<float> trigger;
        private void Awake()
        {
            logger = Logger;
            Logger.LogInfo($"LHA: Loading");
            volume = Config.Bind("LHA Settings", "Set Alert Tone Volume", 1f, new ConfigDescription("How loud or quiet you want the alert tone to be", new AcceptableValueRange<float>(0f, 1f)));
            trigger = Config.Bind("LHA Settings", "Set Trigger", 1f, new ConfigDescription("At what health value do you want the alert tone to play at", new AcceptableValueRange<float>(0f, 440f)));

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
            bool Ready() => Singleton<GameWorld>.Instantiated && gameWorld.AllPlayers != null && gameWorld.AllPlayers.Count > 0 && gameWorld.AllPlayers[0] is Player ? true : false;
            float Current() => Ready() ? gameWorld.AllPlayers[0].HealthController.GetBodyPartHealth(EBodyPart.Common).Current : 9999f; // bullshit floating point number go
        }
    }
}

