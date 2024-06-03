using UnityEngine;
using DG.Tweening;
using TMPro;
using System;

namespace Dip.Features.Darts.Ui
{
    public class DartsWidgetMultiplierBar : MonoBehaviour
    {
        [SerializeField] private Transform visualParent;
        [SerializeField] private TextMeshProUGUI multiplierText;
        [SerializeField] private Vector3 initialPosition;
        [SerializeField] private Vector3 finalPosition;
        [SerializeField] AnimationCurve appearCurve;
        [SerializeField] private float appearDuration;
        [SerializeField] private float callbackDelay;
        [SerializeField] private float duration;
        [SerializeField] AnimationCurve hideCurve;
        [SerializeField] private float hideDuration;
        [SerializeField] ParticleSystem fx;

        // [SerializeField] private float scaleFactor = 1.5f;
        // [SerializeField] private float zoomIn = 0.2f;
        // [SerializeField] private float zoomOut = 0.4f;

        [SerializeField] private Animator animator;
        [SerializeField] private string scaleAnimationName = "Scale";


        [Space(10)]
        [Header("New anim values")]
        [SerializeField] private float startDelay;
        [SerializeField] private float delayBeforeFxStartAgain;
        [SerializeField] private float endDelay;

        private void Awake()
        {
            if (fx != null)
            {
                fx.Stop(true);
            }
        }

        public void PlayAnimation(int oldValue, int newValue, Action callback)
        {
            if (oldValue == newValue)
            {
                return;
            }

            if (fx != null)
            {
                fx.Stop(true);
            }
            visualParent.gameObject.SetActive(true);
            multiplierText.text = string.Format("x{0}", oldValue);
            // var originalScale = multiplierText.transform.localScale;

            visualParent.localPosition = initialPosition;
            var animation = DOTween.Sequence();

            animation.AppendInterval(startDelay);
            animation.Append(visualParent.DOLocalMove(finalPosition, appearDuration).SetEase(appearCurve));
            animation.AppendInterval(duration);

            // if (newValue > oldValue)
            // {
            //     animation.AppendInterval(appearDuration);
            //     animation.Append(multiplierText.transform.DOScale(originalScale * scaleFactor, zoomIn));
            //     animation.AppendInterval(duration);
            //     animation.Append(multiplierText.transform.DOScale(originalScale, zoomOut));
            // }
            // else
            // {
            //     animation.AppendInterval(duration);
            // }

            animation.Append(visualParent.DOLocalMove(initialPosition, hideDuration).SetEase(hideCurve)).AppendCallback(() =>
            {
                visualParent.gameObject.SetActive(false);
                if (fx != null)
                {
                    fx.Stop(true);
                }
            });
            
            animation.InsertCallback(startDelay + appearDuration, () =>
            {
                animator.SetTrigger(scaleAnimationName);
            });
            
            animation.InsertCallback(startDelay + appearDuration + delayBeforeFxStartAgain, () =>
            {
                multiplierText.text = string.Format("x{0}", newValue);
                if (fx != null /*&& newValue > oldValue*/)
                {
                    fx.Play(true);
                }

                Dip.Ui.UiManager.Instance.SoundManager.Play("ui_multiplier_increased");
            });
            animation.InsertCallback(startDelay + appearDuration + callbackDelay, () =>
            {
                callback?.Invoke();
            });

            animation.Play();
        }
    }
}
