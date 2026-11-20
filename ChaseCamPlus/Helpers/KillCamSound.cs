using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace ArcadeCam.Helpers;

// A "whoosh" when the kill cam starts, Fallout style. Plays any .wav / .ogg / .mp3 in
// BepInEx/plugins/ArcadeCam/sounds/killcam (a random one each time); nothing is bundled, so
// the folder starts empty and the player supplies the sounds they like.
internal static class KillCamSound
{
    private static readonly List<AudioClip> Clips = new();
    private static readonly System.Random Dice = new();
    private static AudioSource _source;
    private static bool _loaded;

    internal static string Folder =>
        Path.Combine(Path.GetDirectoryName(typeof(KillCamSound).Assembly.Location) ?? "", "sounds", "killcam");

    private static void Load()
    {
        _loaded = true;
        try
        {
            Directory.CreateDirectory(Folder);
            foreach (string file in Directory.GetFiles(Folder))
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                AudioType type = ext == ".wav" ? AudioType.WAV : ext == ".ogg" ? AudioType.OGGVORBIS : ext == ".mp3" ? AudioType.MPEG : AudioType.UNKNOWN;
                if (type == AudioType.UNKNOWN) continue;
                using UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip("file:///" + file.Replace('\\', '/'), type);
                request.SendWebRequest();
                while (!request.isDone) { }   // small local files, read once
                if (request.result != UnityWebRequest.Result.Success) { Plugin.Logger.LogWarning($"Kill cam sound '{Path.GetFileName(file)}': {request.error}"); continue; }
                AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                if (clip == null) continue;
                clip.name = Path.GetFileNameWithoutExtension(file);
                Clips.Add(clip);
            }
            Plugin.Logger.LogInfo(Clips.Count > 0
                ? $"Kill cam: {Clips.Count} sound(s) loaded from {Folder}"
                : $"Kill cam: no sounds in {Folder} (add .wav, .ogg or .mp3 files for a kill cam whoosh)");
        }
        catch (Exception e) { Plugin.Logger.LogWarning("Kill cam sounds unavailable: " + e.Message); }
    }

    internal static void Play()
    {
        if (!Plugin.KillCamSound.Value) return;
        if (!_loaded) Load();
        if (Clips.Count == 0) return;
        if (_source == null)
        {
            var host = new GameObject("ArcadeCam KillCamSound");
            UnityEngine.Object.DontDestroyOnLoad(host);
            _source = host.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;        // a UI sound, not in the world
            _source.ignoreListenerPause = true;
        }
        _source.volume = Mathf.Clamp01(Plugin.KillCamSoundVolume.Value);
        _source.PlayOneShot(Clips[Dice.Next(Clips.Count)]);
    }
}
