using UnityEngine;

public class AudioManager : MonoBehaviour
{
    [Header("Audio Source")]
    [SerializeField] AudioSource musicSource;
    [SerializeField] AudioSource SFXSource;

    [Header("Audio Clip")]
    public AudioClip background;
    public AudioClip clear1;
    public AudioClip clear2;
    public AudioClip clear3;
    public AudioClip damage;
    public AudioClip dash;
    public AudioClip explosion1;
    public AudioClip explosion2;
    public AudioClip jump1;
    public AudioClip jump2;
    public AudioClip shoot1;
    public AudioClip shoot2;
    public AudioClip shoot3;
    public AudioClip slime;
    public AudioClip ui1;
    public AudioClip ui2;
    public AudioClip ui3;
    public AudioClip walk1;
    public AudioClip walk2;
    public AudioClip walk3;

    void Start()
    {
        musicSource.clip = background;
        musicSource.Play();
    }

    public void PlaySFX(AudioClip clip)
    {
        SFXSource.PlayOneShot(clip);
    }
}
