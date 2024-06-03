using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using Dip.Ui.Animations;
using DG.Tweening;
using Dip.Ui;

namespace Dip.Features.Darts.Ui
{
    public class DartsWidgetController : MonoBehaviour
    {
        [SerializeField] private ParticleSystem fxReact;

        [SerializeField] private GameObject inProgressTimer;
        [SerializeField] private GameObject openTimer;
        [SerializeField] private GameObject completedTimer;
        [SerializeField] private TextMeshProUGUI timerText;

        [SerializeField] private TextMeshProUGUI placeText;
        [SerializeField] private GameObject noPlaceIcon;

        [SerializeField] private GameObject notifyIcon;
        [SerializeField] private Sprite cupSprite;

        [SerializeField] private GameObject lockIcon;
        [SerializeField] private PressButtonAnimation pressButtonAnimation;

        [field: SerializeField]
        public Button WidgetButton { get; private set; }

        private readonly TimeStringBuilder timeStringBuilder = new();

        [SerializeField] private DartsWidgetMultiplierBar dartsWidgetMultiplierBar;
        [SerializeField] private DartsWidgetProgressBar dartsWidgetProgressBar;

        public event Action OnDestroyed;

        public Sprite CupSprite => cupSprite;

        public DartsWidgetMultiplierBar DartsWidgetMultiplierBar => dartsWidgetMultiplierBar;
        public DartsWidgetProgressBar DartsWidgetProgressBar => dartsWidgetProgressBar;

        public Action Clicked;

        protected virtual void Awake()
        {
            lockIcon.SetActive(false);
            WidgetButton.onClick.AddListener(OnWidgetClick);
        }

        private void OnEnable()
        {
            timeStringBuilder.Localize();
            notifyIcon.SetActive(false);
        }

        private void OnDestroy()
        {
            OnDestroyed?.Invoke();
        }


        public void SetNotify(bool value)
        {
            if (notifyIcon.activeSelf != value)
            {
                notifyIcon.SetActive(value);
            }
        }


        public void SetTimer(TimeSpan timeLeft, bool isReadyToOpen)
        {
            if (timeLeft > TimeSpan.Zero)
            {
                if (timeStringBuilder.TryGetTimeStringFromTimeSpan(timeLeft, out var timeString))
                {
                    timerText.SetText(timeString);
                }
            }
            inProgressTimer.SetActive(timeLeft > TimeSpan.Zero && !isReadyToOpen);
            completedTimer.SetActive(timeLeft <= TimeSpan.Zero && !isReadyToOpen);
            openTimer.SetActive(isReadyToOpen);
        }


        public void SetData(int position)
        {
            if (position <= 0)
            {
                placeText.gameObject.SetActive(false);
                noPlaceIcon.gameObject.SetActive(true);
            }
            else
            {
                placeText.gameObject.SetActive(true);
                noPlaceIcon.gameObject.SetActive(false);
                placeText.SetText(position.ToString());
            }
        }

        public void PlayMiniPunch(float duration)
        {
            var tr = transform.Find("Button");
            if (tr == null)
            {
                tr = transform;
            }

            //Debug.Log(name + " PlayMiniPunch");
            var animation = DOTween.Sequence();
            animation.Append(tr.DOScale(Vector3.one * 1.1f, duration * .3f).SetEase(Ease.InSine));
            animation.Append(tr.DOScale(Vector3.one, duration * .7f).SetEase(Ease.OutBack));
        }

        public void EnableFx(bool isEnabled)
        {
            if (isEnabled)
            {
                fxReact.Play(true);
            }
            else
            {
                fxReact.Stop(true);
            }
        }

        public void LockWidget(bool isLocked, int level)
        {
            placeText.gameObject.SetActive(false);
            noPlaceIcon.gameObject.SetActive(true);
            timerText.SetText(isLocked ?
                string.Format(LocalizationManager.GetLocalizedText("eventsannouncer.level"), level.ToString()) :
                              LocalizationManager.GetLocalizedText("eventsannouncer.unlock"));
            lockIcon.GetComponent<UnlockIcon>().Enable(isLocked);
            //pressButtonAnimation.enabled = !isLocked;
            inProgressTimer.SetActive(true);
            completedTimer.SetActive(false);
            openTimer.SetActive(false);
        }

        private void OnWidgetClick()
        {
            Clicked?.Invoke();
        }
    }
}
