using System;
using UnityEngine;
using DG.Tweening;
using UnityEditor.Callbacks;

public class Shape3DCollision : MonoBehaviour
{

    public Animator animator;
    public Transform sphere;

    private Action OnComplete_local;
    private Rigidbody rigidbody;

    void Awake()
    {
        rigidbody = GetComponent<Rigidbody>();
    }

    public void OnClick(Action onComplete = null)
    {
        OnComplete_local = onComplete;

        // transform.DORotate(Vector3.zero, 0.5f, RotateMode.Fast)
        //          .SetEase(Ease.Linear).OnComplete(() =>
        //          {
                     //onComplete?.Invoke();
                     sphere.SetParent(this.transform);
                     sphere.localPosition = new Vector3(0, .25f, 0f);
                     sphere.localScale = new Vector3(.25f, .25f, .25f);
                     animator.SetTrigger("Play ON");
                //  });

    }

    public void OnSphereStart()
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

    public void OnAnimationCompleted()
    {
        // крутити навколо випадкової осі (можеш замінити на transform.up/forward/right)
        Vector3 axis = rigidbody.angularVelocity.normalized;
        // імпульс крутного моменту
        rigidbody.AddTorque(axis * 2f, ForceMode.Impulse);
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
        //Debug.Log("SetOFFAnimation");
    }

}
