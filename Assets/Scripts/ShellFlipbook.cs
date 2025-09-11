using System.Collections;
using UnityEngine;

public class ShellFlipbook : MonoBehaviour
{
    public SpriteRenderer sr;
    public Sprite[] frames;

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
        int r = Random.Range(0, 12);
        if (r == 0) { frames = frames_1; }
        else if (r == 1) { frames = frames_2; }
        else if (r == 2) { frames = frames_3; }
        else if (r == 3) { frames = frames_4; }
        else if (r == 4) { frames = frames_5; }
        else if (r == 5) { frames = frames_6; }
        else if (r == 6) { frames = frames_7; }
        else if (r == 7) { frames = frames_8; }
        else if (r == 8) { frames = frames_9; }
        else if (r == 9) { frames = frames_10; }
        else if (r == 10) { frames = frames_11; }
        else { frames = frames_12; }
        StartCoroutine(PlayFlipbook());
    }

    private IEnumerator PlayFlipbook()
    {
        float delay = 1f / fps;
        foreach (var frame in frames)
        {
            sr.sprite = frame;
            yield return new WaitForSeconds(delay);
        }
        ShellFlipbookPool.Instance.ReturnToPool(this.gameObject);
    }
}
