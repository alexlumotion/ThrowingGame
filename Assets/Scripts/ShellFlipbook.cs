using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShellFlipbook : MonoBehaviour
{
    public SpriteRenderer sr;
    public Sprite[] frames;
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
