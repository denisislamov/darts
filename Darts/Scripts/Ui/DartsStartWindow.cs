using System;
using DG.Tweening;
using Dip.Ui;
using TMPro;
using Ui.Widgets;
using UnityEngine;
using UnityEngine.UI;

namespace Dip.Features.Darts.Ui
{
    public class DartsStartWindow : BaseFrame
    {
        [SerializeField] private Button closeButton;
        [SerializeField] private Button fadeButton;
        [SerializeField] private Button playButton;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private TextMeshProUGUI startButtonText;
        
        [SerializeField] private TextMeshProUGUI lockedText;

        [SerializeField] private Animator timerAnimator;
        [SerializeField] private GameObject timer;
        
        [Header("Season Collections")]
        [SerializeField] private GameObject seasonCollectionsLabel;

        private TimeStringBuilder timeStringBuilder;

        public Action OnPlayPressed { get; set; }
        public Action OnClosePressed { get; set; }

        public override Action OnCloseRequestedByBackButton => CloseWindow;

        
        private void OnEnable()
        {
            closeButton.onClick.AddListener(CloseWindow);
            fadeButton.onClick.AddListener(CloseWindow);
            playButton.onClick.AddListener(PlayButton_OnClick);
        }


        private void OnDisable()
        {
            closeButton.onClick.RemoveListener(CloseWindow);
            fadeButton.onClick.RemoveListener(CloseWindow);
            playButton.onClick.RemoveListener(PlayButton_OnClick);
        }


        protected override void OnInitialize()
        {
            base.OnInitialize();
            timeStringBuilder ??= MainManager.Instance.TimeStringBuilder;
        }
        
        protected override void OnShow()
        {
            base.OnShow();
            this.PlayAppearAnimation();
            
            timerText.text = string.Empty;
            fadeButton.gameObject.SetActiveChecked(true);
        }

        public void Fill(TimeSpan timeLeft, int currentStageNumber, bool isFirstRunInEvent)
        {
            PrepareUIControls(false);
            SetTimer(timeLeft);
            
            startButtonText.text = LocalizationManager.GetLocalizedText("common.start");
        }
        
        public void FillLocked(int unlockLevel)
        {
            PrepareUIControls(true);
            // TODO string template = LocalizationManager.GetLocalizedText(BalloonRaceLocalizationKeys.AnnounceTemplate);
            lockedText.text = string.Format("TODO", unlockLevel.ToString());
            timerText.text = string.Empty;
            timerAnimator.enabled = false;
            
            startButtonText.text = LocalizationManager.GetLocalizedText("common.play");
        }
        
        public void SetTimer(TimeSpan timeLeft)
        {
            timer.SetActiveChecked(timeLeft > TimeSpan.Zero);

            if (!timeStringBuilder.TryGetTimeStringFromTimeSpan(timeLeft, out var timeString))
            {
                return;
            }
            
            timerAnimator.enabled = true;
            timerText.SetText(timeString);
        }
        
        public void SetSeasonCollectionLabel(bool show) => seasonCollectionsLabel?.SetActive(show);

        private void PrepareUIControls(bool isLocked)
        {
            lockedText.gameObject.SetActiveChecked(isLocked);
            timer.SetActiveChecked(!isLocked);
        }

        private void CloseWindow()
        {
            uiManager.HideActiveFrame(this);
            OnClosePressed?.Invoke();
            OnClosePressed = null;
        }


        private void PlayButton_OnClick()
        {
            uiManager.HideActiveFrame(this);
            OnPlayPressed?.Invoke();
            OnPlayPressed = null;
        }
    }
}