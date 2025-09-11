using UnityEngine;
using UnityEngine.Video;

public class BubbleFlipbook : MonoBehaviour
{

    public VideoPlayer videoPlayerBubble;
    public VideoPlayer videoPlayerWave;

    public VideoClip[] bubbleVideos;
    public VideoClip[] waveVideos;

    public bool start = false;

    void Update()
    {
        if (start)
        {
            start = false;
            PlayOnce();
        }
    }

    public void PlayOnce()
    {
        int randomBubble = Random.Range(0, 6);
        int randomWave = Random.Range(0, 12);

        videoPlayerBubble.clip = bubbleVideos[randomBubble];
        videoPlayerWave.clip = waveVideos[randomWave];

        videoPlayerBubble.Play();
        videoPlayerWave.Play();
    }
}
