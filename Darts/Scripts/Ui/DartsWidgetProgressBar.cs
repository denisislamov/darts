using System;
using UnityEngine;
using DG.Tweening;
using MoreMountains.NiceVibrations;
using Dip.Ui.Rewards;
using Dip.Ui;
using System.Collections.Generic;

namespace Dip.Features.Darts.Ui
{
    public class DartsWidgetProgressBar : MonoBehaviour
    {
        [SerializeField] private Transform visualParent;
        [SerializeField] private Vector3 initialPosition;
        [SerializeField] private Vector3 finalPosition;
        [SerializeField] AnimationCurve appearCurve;
        [SerializeField] private float appearDuration;
        [SerializeField] AnimationCurve hideCurve;
        [SerializeField] private float hideDuration;
        [SerializeField] private GameObject prizeContainer;
        [SerializeField] private PaddingProgressBar progress;
        [SerializeField] private RewardViewConfig rewardViewConfig;

        [SerializeField] private AnimationConfig animationConfig;

        [SerializeField] private ParticleSystem prizeFx;

        [Space(10)]
        [Header("Progress bar show/hide")]
        [SerializeField] private float startShowAnimationDelay;
        [SerializeField] private float endShowAnimationDelay;
        [SerializeField] private float startHideAnimationDelay;
        [SerializeField] private float endHideAnimationDelay;

        [Header("Prize show/hide")]
        [SerializeField] private float startPrizeShowDelay;
        [SerializeField] private float endPrizeShowDelay;
        [SerializeField] private float startPrizeHideDelay;
        [SerializeField] private float endPrizeHideDelay;

        private RewardItemControlFactory rewardItemControlFactory;
        private RewardItemControl currentRewardItemControl;

        private float rewardItemPadding;

        private int currentProgressValue;
        private int targetProgressValue;

        private Coroutine doProgressCoroutine;

        private Sequence internalAnimation;
        private Sequence sliderAnimation;

        private IAudioSource audioSource = null;


        private void OnDestroy()
        {
            DestroyCurrentRewardItem();
        }

        public RewardItemControl CreateRewardItem(string type, string subtype)
        {
            DestroyCurrentRewardItem();
            UpdateRewardFactory();
            var control = rewardItemControlFactory.CreateRewardItem(type, subtype, prizeContainer.transform);
            control.transform.SetAsFirstSibling();

            currentRewardItemControl = control;
            return control;
        }

        public RewardItemControl CreateSpecialRewardItem(string specialView)
        {
            DestroyCurrentRewardItem();
            UpdateRewardFactory();
            var control = rewardItemControlFactory.CreateSpecialRewardItem(specialView, prizeContainer.transform);
            control.transform.SetAsFirstSibling();

            currentRewardItemControl = control;
            return control;
        }

        public void DestroyCurrentRewardItem()
        {
            if (currentRewardItemControl)
            {
                rewardItemPadding = 0;
                UpdatePadding();

                Destroy(currentRewardItemControl.gameObject);
                currentRewardItemControl = null;
            }
        }

        public void SetCurrentProgress(int current)
        {
            currentProgressValue = current;

            SetProgress(currentProgressValue, targetProgressValue);
        }

        public void SetProgress(int currentProgress, int targetProgress)
        {
            currentProgressValue = currentProgress;
            targetProgressValue = targetProgress;

            UpdateProgressSlider();
        }

        public void AddProgress(int addValue, Action callback)
        {
            if (audioSource != null)
            {
                UiManager.Instance.SoundManager.Stop(audioSource);
            }
            audioSource = UiManager.Instance.SoundManager.Play("orders_progress");

            currentProgressValue += addValue;

            UpdateProgressSlider(false, callback);
        }

        public void CompleteTask(Action onComplete)
        {
            internalAnimation?.Kill(true);
            internalAnimation = DOTween.Sequence();

            internalAnimation.Append(DOTween.To(() => 0,
                time => prizeContainer.transform.localScale = new Vector3(
                    animationConfig.prizeCompleteScaleCurve.Evaluate(time),
                    animationConfig.prizeCompleteScaleCurve.Evaluate(time), prizeContainer.transform.localScale.z),
                1f, animationConfig.prizeCompleteAnimationDuration));
            internalAnimation.OnComplete(() =>
            {
                onComplete?.Invoke();
            });
        }

        public void PlayPrizeParticles()
        {
            if (prizeFx != null)
            {
                prizeFx.Play(true);
            }

            MMVibrationManager.Haptic(HapticTypes.Success);
        }

        private void UpdateRewardFactory()
        {
            if (rewardItemControlFactory == null)
            {
                rewardItemControlFactory = new RewardItemControlFactory(rewardViewConfig);
            }
        }

        private void UpdateProgressSlider(bool instantly = true, Action callback = null)
        {
            StopProgressCoroutine();

            var collected = Mathf.Clamp(currentProgressValue, 0, targetProgressValue);
            var factor = (float)collected / targetProgressValue;
            if (instantly)
            {
                progress.Value = factor;
            }
            else
            {
                DoProgressValue(factor, callback);
            }
        }

        private void DoProgressValue(float targetValue, Action callback)
        {
            sliderAnimation?.Kill();
            sliderAnimation = DOTween.Sequence();

            sliderAnimation.OnComplete(() =>
            {
                progress.Value = targetValue;
            });
            var startValue = progress.Value;
            var factor = 1.0f;
            if (Mathf.Abs(startValue - targetValue) < 0.1f)
            {
                factor = 0.5f;
            }

            sliderAnimation.Insert(animationConfig.sliderAnimationDelay * factor, DOTween.To(value =>
                    { progress.Value = value; }, startValue, targetValue, animationConfig.sliderFillingDuration).SetDelay(animationConfig.sliderAnimationSoundDelay))
                .SetEase(animationConfig.sliderAnimationCurve);
            sliderAnimation.InsertCallback(animationConfig.sliderAnimationDelay * factor + animationConfig.sliderFillingDuration * factor - animationConfig.callbackEnding + animationConfig.sliderAnimationSoundDelay, () =>
            {
                if (audioSource != null)
                {
                    UiManager.Instance.SoundManager.Stop(audioSource);
                }

                callback?.Invoke();
            });
        }

        private void StopProgressCoroutine()
        {
            if (doProgressCoroutine != null)
            {
                StopCoroutine(doProgressCoroutine);
                doProgressCoroutine = null;
            }
        }

        private void UpdatePadding()
        {
            progress.RightPaddingValue = rewardItemPadding;
        }

        public Sequence PlayShowAnimation()
        {
            visualParent.gameObject.SetActive(true);
            visualParent.localPosition = initialPosition;
            var animation = DOTween.Sequence();
            animation.AppendInterval(startShowAnimationDelay);
            animation.Append(visualParent.DOLocalMove(finalPosition, appearDuration).SetEase(appearCurve));
            animation.AppendInterval(endShowAnimationDelay);
            return animation;
        }

        public Sequence PlayHideAnimation()
        {
            var animation = DOTween.Sequence();
            animation.AppendInterval(startHideAnimationDelay);
            animation.Append(visualParent.DOLocalMove(initialPosition, hideDuration).SetEase(hideCurve)).AppendCallback(() =>
            {
                visualParent.gameObject.SetActive(false);

                if (prizeFx != null)
                {
                    prizeFx.Stop(true);
                }

            });
            animation.AppendInterval(endHideAnimationDelay);

            return animation;
        }

        public void PlayPrizeShow(Action callback)
        {
            internalAnimation?.Kill(true);
            internalAnimation = DOTween.Sequence();

            currentRewardItemControl.transform.localScale = Vector3.zero;

            internalAnimation.AppendInterval(startPrizeShowDelay);
            internalAnimation.Append(DOTween.To(() => 0,
                time => currentRewardItemControl.transform.localScale = new Vector3(
                    animationConfig.itemShowScaleCurve.Evaluate(time),
                    animationConfig.itemShowScaleCurve.Evaluate(time), currentRewardItemControl.transform.localScale.z),
                1f, animationConfig.itemAnimationDurationStart));
            internalAnimation.AppendInterval(endPrizeShowDelay);
            internalAnimation.AppendCallback(() => callback?.Invoke());
        }

        public void PlayPrizeHide(Action onTopOfHideCallback, Action callback)
        {
            internalAnimation?.Kill(true);
            internalAnimation = DOTween.Sequence();
            internalAnimation.AppendInterval(startPrizeHideDelay);
            internalAnimation.AppendCallback(() =>
            {
                UiManager.Instance.SoundManager.Play("ui_meta_cascade_reward_appear");
            });
            internalAnimation.Append(DOTween.To(() => 0,
                time => currentRewardItemControl.transform.localScale = new Vector3(
                    animationConfig.itemHideScaleCurve.Evaluate(time),
                    animationConfig.itemHideScaleCurve.Evaluate(time), currentRewardItemControl.transform.localScale.z),
                1f, animationConfig.itemAnimationDurationEnd));

            internalAnimation.AppendInterval(endPrizeHideDelay);
            internalAnimation.AppendCallback(() => onTopOfHideCallback?.Invoke());
            internalAnimation.AppendCallback(() =>
            {
                callback?.Invoke();
            });
        }

        [Serializable]
        public class AnimationConfig
        {
            [Header("Progress Bar Animation Settings")]
            public float sliderAnimationDelay = 0f;
            public float sliderFillingDuration = 0.2f;
            public float callbackEnding = 0.05f;
            public AnimationCurve sliderAnimationCurve;

            [Header("Reward Item Animation Settings")]
            public float itemAnimationDurationStart = 0.2f;
            public float itemAnimationDurationEnd = 0.2f;
            public AnimationCurve itemShowScaleCurve;
            public AnimationCurve itemHideScaleCurve;

            [Header("Prize Container New Task Animation Settings")]
            public float prizeNewTaskAnimationDuration = 0.6f;
            public AnimationCurve prizeShowScaleCurve;

            [Header("Prize Container Complete Task Animation Settings")]
            public float prizeCompleteAnimationDuration = 0.1f;
            public AnimationCurve prizeCompleteScaleCurve;

            public float sliderAnimationSoundDelay = 0.1f;
        }
    }
}
