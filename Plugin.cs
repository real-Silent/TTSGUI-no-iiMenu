using BepInEx;
using Oculus.Platform.Models;
using Photon.Pun;
using Photon.Voice.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace TTSGUI
{
    // Srry for this messy ass code but if it works it works
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class Manager : BaseUnityPlugin
    {
        public static Manager instance = null;

        private void Awake()
        {
            instance = this;
        }

        public static Coroutine RunCoroutine(IEnumerator enumerator)
        {
            return instance.StartCoroutine(enumerator);
        }

        public static void EndCoroutine(Coroutine enumerator)
        {
            instance.StopCoroutine(enumerator);
        }
        void OnGUI()
        {
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.alignment = TextAnchor.MiddleCenter;
            labelStyle.fontSize = 100;
            labelStyle.normal.textColor = Color.white;

            smoothAnim = barOpen ? Mathf.Lerp(smoothAnim, 0.5f, Time.deltaTime) : Mathf.Lerp(smoothAnim, 0f, Time.deltaTime);
            if (Mathf.Floor(smoothAnim * 255f) != 0f)
            {
                overlay.SetPixel(0, 0, new Color(0f, 0f, 0f, smoothAnim));
                overlay.Apply();

                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), overlay);

                GUIStyle textStyle = new GUIStyle(GUI.skin.textArea);
                textStyle.fontSize = 50;
                GUI.SetNextControlName("bartext");
                barText = GUI.TextArea(new Rect(10, smoothAnim * 115 - 50, Screen.width - 20, 50), barText, textStyle);
            }
            if (barOpen)
            {
                GorillaTagger.Instance.transform.position = startPos;
                GUI.FocusControl("bartext");

                if (barText.Contains("\n"))
                {
                    barText = barText.Replace("\n", "");
                    GUI.FocusControl(null);

                    RunCoroutine(SpeakText(barText));
                    ToggleBar();
                }
            }

            bool down = UnityInput.Current.GetKey(KeyCode.Slash) && !UnityInput.Current.GetKey(KeyCode.LeftShift);
            if (down && !oldbs)
                ToggleBar();
            
            oldbs = down;

            GUIStyle labelStyle2 = new GUIStyle(GUI.skin.label);
            labelStyle2.alignment = TextAnchor.MiddleLeft;
            labelStyle2.wordWrap = false;
            labelStyle2.fontSize = 20;
            labelStyle2.normal.textColor = Color.white;
        }

        public static float timeMenuStarted = -1f;
        public static string GetSHA256(string input)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                StringBuilder stringy = new StringBuilder();
                foreach (byte by in bytes)
                {
                    stringy.Append(by.ToString("x2"));
                }

                return stringy.ToString();
            }
        }
        public static int narratorIndex;
        public static string narratorName = "Default";
        public static System.Collections.IEnumerator SpeakText(string text)
        {
            if (Time.time < (timeMenuStarted + 5f))
                yield break;

            string fileName = GetSHA256(text) + (narratorIndex == 0 ? ".wav" : ".mp3");
            string directoryPath = "iisStupidMenu/TTS" + (narratorName == "Default" ? "" : narratorName);

            if (!Directory.Exists("iisStupidMenu"))
                Directory.CreateDirectory("iisStupidMenu");

            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            if (!File.Exists("iisStupidMenu/TTS" + (narratorName == "Default" ? "" : narratorName) + "/" + fileName))
            {
                string filePath = directoryPath + "/" + fileName;

                if (!File.Exists(filePath))
                {
                    string postData = "{\"text\": \"" + text.Replace("\n", "").Replace("\r", "").Replace("\"", "") + "\"}";

                    if (narratorIndex == 0)
                    {
                        using (UnityWebRequest request = new UnityWebRequest("https://iidk.online/tts", "POST"))
                        {
                            byte[] raw = Encoding.UTF8.GetBytes(postData);

                            request.uploadHandler = new UploadHandlerRaw(raw);
                            request.SetRequestHeader("Content-Type", "application/json");

                            request.downloadHandler = new DownloadHandlerBuffer();
                            yield return request.SendWebRequest();

                            if (request.result != UnityWebRequest.Result.Success)
                            {
                                UnityEngine.Debug.LogError("Error downloading TTS: " + request.error);
                                yield break;
                            }

                            byte[] response = request.downloadHandler.data;
                            File.WriteAllBytes(filePath, response);
                        }
                    }
                    else
                    {
                        using (UnityWebRequest request = UnityWebRequest.Get("https://api.streamelements.com/kappa/v2/speech?voice=" + narratorName + "&text=" + UnityWebRequest.EscapeURL(text)))
                        {
                            yield return request.SendWebRequest();

                            if (request.result != UnityWebRequest.Result.Success)
                                Debug.LogError("Error downloading TTS: " + request.error);
                            else
                                File.WriteAllBytes(filePath, request.downloadHandler.data);
                        }
                    }
                }
            }

            PlayAudio("TTS" + (narratorName == "Default" ? "" : narratorName) + "/" + fileName);
        }
        public static bool barOpen = false;
        private static string barText = "";
        private static float smoothAnim = 0f;
        private static bool oldbs = false;
        private static Vector3 startPos = Vector3.zero;
        Texture2D overlay = new Texture2D(1, 1);
        public static void PlayAudio(string file)
        {
            AudioClip sound = LoadSoundFromFile(file);
            GorillaTagger.Instance.myRecorder.SourceType = Recorder.InputSourceType.AudioClip;
            GorillaTagger.Instance.myRecorder.AudioClip = sound;
            GorillaTagger.Instance.myRecorder.RestartRecording(true);
            GorillaTagger.Instance.myRecorder.DebugEchoMode = true;
            if (!LoopAudio)
            {
                AudioIsPlaying = true;
                RecoverTime = Time.time + sound.length + 0.4f;
            }
        }

        public static Dictionary<string, AudioClip> audioFilePool = new Dictionary<string, AudioClip> { };
        public static AudioClip LoadSoundFromFile(string fileName) // Thanks to ShibaGT for help with loading the audio from file
        {
            AudioClip sound = null;

            if (!audioFilePool.ContainsKey(fileName))
            {
                if (!Directory.Exists("iisStupidMenu"))
                {
                    Directory.CreateDirectory("iisStupidMenu");
                }
                string filePath = System.IO.Path.Combine(System.Reflection.Assembly.GetExecutingAssembly().Location, "iisStupidMenu/" + fileName);
                filePath = filePath.Split("BepInEx\\")[0] + "iisStupidMenu/" + fileName;
                filePath = filePath.Replace("\\", "/");

                UnityWebRequest actualrequest = UnityWebRequestMultimedia.GetAudioClip("file://" + filePath, GetAudioType(GetFileExtension(fileName)));
                UnityWebRequestAsyncOperation newvar = actualrequest.SendWebRequest();
                while (!newvar.isDone) { }

                AudioClip actualclip = DownloadHandlerAudioClip.GetContent(actualrequest);
                sound = Task.FromResult(actualclip).Result;

                audioFilePool.Add(fileName, sound);
            }
            else
            {
                sound = audioFilePool[fileName];
            }

            return sound;
        }

        public static string GetFileExtension(string fileName)
        {
            return fileName.ToLower().Split(".")[fileName.Split(".").Length - 1];
        }
        public static AudioType GetAudioType(string extension)
        {
            switch (extension.ToLower())
            {
                case "mp3":
                    return AudioType.MPEG;
                case "wav":
                    return AudioType.WAV;
                case "ogg":
                    return AudioType.OGGVORBIS;
                case "aiff":
                    return AudioType.AIFF;
            }
            return AudioType.WAV;
        }

        public static bool AudioIsPlaying = false;
        public static float RecoverTime = -1f;
        public static bool LoopAudio = false;
        public static void ToggleBar()
        {
            barOpen = !barOpen;
            barText = "";
            startPos = GorillaTagger.Instance.transform.position;
            if (!barOpen)
            {
                GUI.FocusControl(null);
            }
        }
    }
}
