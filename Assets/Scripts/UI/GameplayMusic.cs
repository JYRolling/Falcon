using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class GameplayMusic : MonoBehaviour
{
    [SerializeField] AudioClip musicClip;
    [SerializeField] [Range(0f,1f)] float volume = 1f;
    [SerializeField] bool loop = true;
    [SerializeField] bool playOnStart = true;

    AudioSource source;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.volume = volume;
        if (musicClip != null)
            source.clip = musicClip;
    }

    void Start()
    {
        if (playOnStart && source.clip != null)
            source.Play();
    }

    public void StopMusic()
    {
        if (source.isPlaying) source.Stop();
    }
}
