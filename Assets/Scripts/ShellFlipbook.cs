using System.Collections;
using UnityEngine;
using UnityEngine.Video;

public class ShellFlipbook : MonoBehaviour
{

    public VideoPlayer videoPlayerBubble;
    public VideoPlayer videoPlayerShell;

    public VideoClip[] bubbleVideos;
    public VideoClip[] shellVideos;


    public SpriteRenderer spriteShell;
    public SpriteRenderer spriteBubble;
    public Sprite[] shellFrames;
     public Sprite[] bubbleFrames;

    [SerializeField] private Sprite[] frames_1;
    [SerializeField] private Sprite[] frames_2;
    [SerializeField] private Sprite[] frames_3;
    [SerializeField] private Sprite[] frames_4;
    [SerializeField] private Sprite[] frames_5;
    [SerializeField] private Sprite[] frames_6;
    [SerializeField] private Sprite[] frames_7;
    [SerializeField] private Sprite[] frames_8;
    [SerializeField] private Sprite[] frames_9;
    [SerializeField] private Sprite[] frames_10;
    [SerializeField] private Sprite[] frames_11;
    [SerializeField] private Sprite[] frames_12;


    [SerializeField] private Sprite[] frames_bubble_1;
    [SerializeField] private Sprite[] frames_bubble_2;
    [SerializeField] private Sprite[] frames_bubble_3;
    [SerializeField] private Sprite[] frames_bubble_4;
    [SerializeField] private Sprite[] frames_bubble_5;
    [SerializeField] private Sprite[] frames_bubble_6;
    [SerializeField] private Sprite[] frames_bubble_7;
    [SerializeField] private Sprite[] frames_bubble_8;
    [SerializeField] private Sprite[] frames_bubble_9;
    [SerializeField] private Sprite[] frames_bubble_10;
    [SerializeField] private Sprite[] frames_bubble_11;
    [SerializeField] private Sprite[] frames_bubble_12;
    [SerializeField] private Sprite[] frames_bubble_13;
    [SerializeField] private Sprite[] frames_bubble_14;
    [SerializeField] private Sprite[] frames_bubble_15;
    [SerializeField] private Sprite[] frames_bubble_16;
    [SerializeField] private Sprite[] frames_bubble_17;
    [SerializeField] private Sprite[] frames_bubble_18;

    public float fps = 30f;

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
        int randomBubble = Random.Range(0, 18);
        int randomShell = Random.Range(0, 12);

        videoPlayerBubble.clip = bubbleVideos[randomBubble];
        videoPlayerShell.clip = shellVideos[randomShell];

        videoPlayerBubble.Play();
        videoPlayerShell.Play();

        videoPlayerShell.loopPointReached += OnVideoEnded;
    }

    private void OnVideoEnded(VideoPlayer vp)
    {
        //transform.position = new Vector3(100f, 0f, 0f);
        ShellFlipbookPool.Instance.ReturnToPool(this.gameObject);
    }

    public void PlayOnceOld()
    {
        int randomShell = Random.Range(0, 12);
        if (randomShell == 0) { shellFrames = frames_1; }
        else if (randomShell == 1) { shellFrames = frames_2; }
        else if (randomShell == 2) { shellFrames = frames_3; }
        else if (randomShell == 3) { shellFrames = frames_4; }
        else if (randomShell == 4) { shellFrames = frames_5; }
        else if (randomShell == 5) { shellFrames = frames_6; }
        else if (randomShell == 6) { shellFrames = frames_7; }
        else if (randomShell == 7) { shellFrames = frames_8; }
        else if (randomShell == 8) { shellFrames = frames_9; }
        else if (randomShell == 9) { shellFrames = frames_10; }
        else if (randomShell == 10) { shellFrames = frames_11; }
        else { shellFrames = frames_12; }

        int randomBubble = Random.Range(0, 18);
        if (randomBubble == 0) { bubbleFrames = frames_bubble_1; }
        else if (randomBubble == 1) { bubbleFrames = frames_bubble_2; }
        else if (randomBubble == 2) { bubbleFrames = frames_bubble_3; }
        else if (randomBubble == 3) { bubbleFrames = frames_bubble_4; }
        else if (randomBubble == 4) { bubbleFrames = frames_bubble_5; }
        else if (randomBubble == 5) { bubbleFrames = frames_bubble_6; }
        else if (randomBubble == 6) { bubbleFrames = frames_bubble_7; }
        else if (randomBubble == 7) { bubbleFrames = frames_bubble_8; }
        else if (randomBubble == 8) { bubbleFrames = frames_bubble_9; }
        else if (randomBubble == 9) { bubbleFrames = frames_bubble_10; }
        else if (randomBubble == 10) { bubbleFrames = frames_bubble_11; }
        else if (randomBubble == 11) { bubbleFrames = frames_bubble_12; }
        else if (randomBubble == 12) { bubbleFrames = frames_bubble_13; }
        else if (randomBubble == 13) { bubbleFrames = frames_bubble_14; }
        else if (randomBubble == 14) { bubbleFrames = frames_bubble_15; }
        else if (randomBubble == 15) { bubbleFrames = frames_bubble_16; }
        else if (randomBubble == 16) { bubbleFrames = frames_bubble_17; }
        else { bubbleFrames = frames_bubble_18; }

        StartCoroutine(PlayFlipbook(spriteShell, shellFrames, true));
        StartCoroutine(PlayFlipbook(spriteBubble, bubbleFrames, false));
    }

    private IEnumerator PlayFlipbook(SpriteRenderer renderSprite, Sprite[] tSprites, bool isReturn)
    {
        float delay = 1f / fps;
        foreach (var frame in tSprites)
        {
            renderSprite.sprite = frame;
            yield return new WaitForSeconds(delay);
        }
        if (isReturn)
        {
            ShellFlipbookPool.Instance.ReturnToPool(this.gameObject);
        }
    }
}
