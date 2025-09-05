using System;
using UnityEngine;
using DG.Tweening;

public class Shape3DCollision : MonoBehaviour
{

    public Animator animator;
    public Transform sphere;

    private Action OnComplete_local;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void OnClick(Action onComplete = null)
    {
        OnComplete_local = onComplete;

        transform.DORotate(Vector3.zero, 0.5f, RotateMode.Fast)
                 .SetEase(Ease.Linear).OnComplete(() =>
                 {
                     //onComplete?.Invoke();
                     sphere.SetParent(this.transform);
                     sphere.localPosition = new Vector3(0, .25f, 0f);
                     sphere.localScale = new Vector3(.25f, .25f, .25f);
                     animator.SetTrigger("Play ON");
                 });

    }

    public void OnAnimationCompleted()
    {
        sphere.SetParent(null);
        sphere.DOMove(new Vector3(sphere.position.x, sphere.position.y + 4f, sphere.position.z), 0.75f)
        .SetEase(Ease.InQuad)
        .OnComplete(() =>
        {
            sphere.DOScale(Vector3.zero, 0.5f)
            .SetEase(Ease.InQuad)
            .OnComplete(() =>
            {
                //OnComplete_local?.Invoke();
            });
        });
        OnComplete_local?.Invoke();
    }

    public void ResetAnimator()
    {
        animator.ResetTrigger("Play OFF");
        animator.ResetTrigger("Play ON");
        animator.Play("IDLE", 0, 0f);
    }

    public void SetOFFAnimation()
    {
        animator.SetTrigger("Play OFF");
        //animator.Play("IDLE", 0, 0f);
        Debug.Log("SetOFFAnimation");
    }

}
