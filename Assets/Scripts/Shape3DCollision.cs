using System;
using UnityEngine;
using DG.Tweening;

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

        // крутити навколо випадкової осі (можеш замінити на transform.up/forward/right)
        Vector3 axis = rigidbody.angularVelocity.normalized;
        // імпульс крутного моменту
        rigidbody.AddTorque(axis * 2f, ForceMode.Impulse);

    }

    public void OnSphereStart()
    {

        sphere.SetParent(null);

        Vector3 targetPosition = sphere.position + Vector3.up * 3f;
        
        sphere.DOMove(targetPosition, 3f)
        .SetEase(Ease.InOutQuad)
        .OnComplete(() =>
        {
            // sphere.DOScale(Vector3.zero, 0.5f)
            // .SetEase(Ease.InQuad)
            // .OnComplete(() =>
            // {
            //           //OnComplete_local?.Invoke();
            // });
        });

        Vector3 start = sphere.localScale;
        Vector3 peak  = start * 1.0f;   // значення на плато (можеш змінити)
        Vector3 end   = start * 0.0f;   // приклад: йдемо до нуля

        Sequence seq = DOTween.Sequence();
        seq.Append(sphere.DOScale(peak, 0.5f).SetEase(Ease.InSine))
           .AppendInterval(0.83f)
           .Append(sphere.DOScale(end, 0.66f).SetEase(Ease.OutSine));

        OnComplete_local?.Invoke();
    }

    public void OnAnimationCompleted()
    {

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
