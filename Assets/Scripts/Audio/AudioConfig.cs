using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class MusicConfig
{
    [SerializeField] private string id;
    [SerializeField] private AudioClip clip;
    [Range(0f, 1f)] [SerializeField] private float volume = 1f;
    [SerializeField] private bool loop = true;

    public string Id => id;
    public AudioClip Clip => clip;
    public float Volume => volume;
    public bool Loop => loop;
}

[Serializable]
public sealed class SoundConfig
{
    [SerializeField] private string id;
    [SerializeField] private AudioClip clip;
    [Range(0f, 1f)] [SerializeField] private float volume = 1f;

    public string Id => id;
    public AudioClip Clip => clip;
    public float Volume => volume;
}

[CreateAssetMenu(fileName = "AudioConfig", menuName = "秘核防线/Audio Config")]
public sealed class AudioConfig : ScriptableObject
{
    [SerializeField] private List<MusicConfig> music = new List<MusicConfig>();
    [SerializeField] private List<SoundConfig> sounds = new List<SoundConfig>();

    public IReadOnlyList<MusicConfig> Music => music;
    public IReadOnlyList<SoundConfig> Sounds => sounds;
}
