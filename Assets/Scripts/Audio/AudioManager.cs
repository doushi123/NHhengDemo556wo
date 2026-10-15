using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class AudioManager : AppService
{
    [SerializeField] private AudioConfig audioConfig;
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource soundSource;

    private readonly Dictionary<string, MusicConfig> music =new Dictionary<string, MusicConfig>();
    private readonly Dictionary<string, SoundConfig> sounds =new Dictionary<string, SoundConfig>();

    private MusicConfig currentMusic;
    private float musicVolume = 1f;
    private float soundVolume = 1f;

    protected override void OnInitialize()
    {
        foreach (MusicConfig config in audioConfig.Music)
        {
            music.Add(config.Id, config);
        }

        foreach (SoundConfig config in audioConfig.Sounds)
        {
            sounds.Add(config.Id, config);
        }

        SetMusicVolume(PlayerPrefs.GetFloat("MusicVolume", 1f), false);
        SetSoundVolume(PlayerPrefs.GetFloat("SoundVolume", 1f), false);
    }

    public void PlayMusic(string id)
    {
        MusicConfig config = music[id];
        if (musicSource.clip == config.Clip && musicSource.isPlaying)
        {
            return;
        }

        currentMusic = config;
        musicSource.clip = config.Clip;
        musicSource.loop = config.Loop;
        musicSource.volume = config.Volume * musicVolume;
        musicSource.Play();
    }

    public void StopMusic()
    {
        musicSource.Stop();
        currentMusic = null;
    }

    public void PlaySound(string id)
    {
        SoundConfig config = sounds[id];
        soundSource.PlayOneShot(config.Clip, config.Volume * soundVolume);
    }

    public void PlaySoundAt(string id, Vector3 position)
    {
        SoundConfig config = sounds[id];
        AudioSource.PlayClipAtPoint(config.Clip, position, config.Volume * soundVolume);
    }

    public void SetMusicVolume(float volume, bool save = true)
    {
        musicVolume = Mathf.Clamp01(volume);
        if (currentMusic != null)
        {
            musicSource.volume = currentMusic.Volume * musicVolume;
        }

        if (save)
        {
            PlayerPrefs.SetFloat("MusicVolume", musicVolume);
        }
    }

    public void SetSoundVolume(float volume, bool save = true)
    {
        soundVolume = Mathf.Clamp01(volume);
        if (save)
        {
            PlayerPrefs.SetFloat("SoundVolume", soundVolume);
        }
    }

    protected override void OnShutdown()
    {
        musicSource.Stop();
        soundSource.Stop();
        currentMusic = null;
        music.Clear();
        sounds.Clear();
    }
}
