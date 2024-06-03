using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using DG.Tweening;
using Dip.Ui;
using Dip.Ui.CascadeEvent;
using Dip.Ui.Rewards;
using Dip.Ui.Tooltips;
using Dip.Ui.Tooltips.MultiplyBonus;

namespace Dip.Features.Darts.Ui
{
    public class DartsCascadeWindow : BaseFrame
    {
        private const float AnimationSpeed = 0.7f;

        [SerializeField] private RectTransform animatedGameObject;
        [SerializeField] private Button fadeButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button infoButton;
        
        [SerializeField] private DartsCascadeContainer taskContainer;
        [SerializeField] private MetaScreenCascadeEventContainer infoContainer;
        
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private Tooltip lockedTooltip;
        [SerializeField] private MultiplyBonusTooltip multiplyTooltip;        
        [SerializeField] private RewardListTooltip rewardListTooltip;

        private Sequence appearAnimation;

        public Action OnInfoPressed;
        public Action OnClosePressed;

        public override Action OnCloseRequestedByBackButton
        {
            get
            {
                if (closeButton.gameObject.activeInHierarchy)
                {
                    return CloseWindow;
                }

                return null;
            }
        }

        protected override void OnInitialize()
        {
            base.OnInitialize();

            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CloseWindowByButton);

            fadeButton.onClick.RemoveAllListeners();
            fadeButton.onClick.AddListener(CloseWindowByFade);

            if (infoButton)
            {
                infoButton.onClick.AddListener(() => OnInfoPressed?.Invoke());
            }

            lockedTooltip.ShowTooltip(false, true);
            multiplyTooltip.ShowTooltip(false, true);
            rewardListTooltip.ShowTooltip(false, true);
            
        }

        protected override void OnShow()
        {
            base.OnShow();
            
            PlayAnimation();
        }

        protected override void OnHide()
        {
            base.OnHide();
            if (taskContainer)
            {
                taskContainer.RemoveAllTaskViews();
            }
        }

        public void SetTargetIcon(Sprite iconSprite)
        {
            infoContainer.SetTargetIcon(iconSprite);
        }

        public void SetTimer(TimeSpan timeLeft)
        {
            if (timeLeft > TimeSpan.Zero)
            {
                infoContainer.SetTimer(timeLeft);
            }
            else 
            {
                infoContainer.SetTimerCompletedState();

                HideInfoButton();
            }
        }

        public RewardItemControl CreateRewardItem(string type, string subtype)
        {
            if (infoContainer)
            {
                return infoContainer.CreateRewardItem(type, subtype);
            }

            return null;
        }

        public RewardItemControl CreateSpecialRewardItem(string specialView)
        {
            if (infoContainer)
            {
                return infoContainer.CreateSpecialRewardItem(specialView);
            }

            return null;
        }

        public RewardItemControl CreateAdditionalRewardItem(string type, string subtype)
        {
            if (infoContainer)
            {
                return infoContainer.CreateAdditionalRewardItem(type, subtype);
            }

            return null;
        }

        public void ShowInfoChestTooltip(Action<RewardListTooltip> onFill)
        {
            infoContainer.ShowChestTooltip(onFill);
        }

        public void ShowInfoMultiplierTooltip(MultiplyBonusTooltip.MultiplyBonusTooltipContext context)
        {
            infoContainer.ShowMultiplierTooltip(context);
        }

        public void SetProgress(int current, int target)
        {
            infoContainer.SetProgress(current, target);
        }


        public DartsTaskCascade AddTaskView(bool isLastIndex = false)
        {
            var control = taskContainer.AddTaskView();

            if (isLastIndex)
            {
                control.SetActiveLines(false, false, false);
            }

            return control;
        }

        public void RemoveAllTaskViews()
        {
            taskContainer.RemoveAllTaskViews();
        }

        public void ShowListLockedTooltip(int index)
        {
            if (lockedTooltip)
            {
                Tooltip.TooltipAlign align = lockedTooltip.DetectVerticalAlign(
                    taskContainer[index].RewardPosition,
                    scrollRect.GetComponent<RectTransform>());

                lockedTooltip.ShowTooltip(true, false, taskContainer[index].RewardTransform, preferredAlign: align);
            }
        }

        public void ShowListMultiplierTooltip(int index, MultiplyBonusTooltip.MultiplyBonusTooltipContext context)
        {
            if (multiplyTooltip)
            {
                multiplyTooltip.Setup(context);

                Tooltip.TooltipAlign align = multiplyTooltip.DetectVerticalAlign(
                    taskContainer[index].RewardPosition,
                    scrollRect.GetComponent<RectTransform>());

                multiplyTooltip.ShowTooltip(true, false, taskContainer[index].RewardTransform, preferredAlign: align);
            }
        }

        public RewardListTooltip ShowRewardListTooltip(int index)
        {
            if (rewardListTooltip)
            {
                Tooltip.TooltipAlign align = rewardListTooltip.DetectVerticalAlign(
                    taskContainer[index].RewardPosition,
                    scrollRect.GetComponent<RectTransform>());

                rewardListTooltip.ShowTooltip(true, false, taskContainer[index].RewardTransform, preferredAlign: align);

                return rewardListTooltip;
            }

            return null;
        }

        public void ScrollToView(DartsTaskCascade currentView)
        {
            if (currentView != null)
            {
                Canvas.ForceUpdateCanvases();
                var scrollPos = scrollRect.GetNormalizedPositionForItem(currentView.RectTransform);
                scrollPos = Mathf.Clamp01(scrollPos);
                scrollRect.DOScrollToTargetVertical(scrollPos, 0f);
            }
        }

        public void HideInfoButton() 
        {
            infoButton.gameObject.SetActive(false);
        }

        private void CloseWindowByButton()
        {
            CloseWindow();
        }

        private void CloseWindowByFade()
        {
            CloseWindow();
            
        }

        private void CloseWindow()
        {
            uiManager.HideActiveFrame(this);
            OnClosePressed?.Invoke();
        }

        private void PlayAnimation()
        {
            animatedGameObject.localScale = Vector3.one;

            appearAnimation?.Kill();
            appearAnimation = DOTween.Sequence().SetLink(gameObject);
            appearAnimation.Append(animatedGameObject.DOScale(Vector3.one * 1.01f, 0.05f * AnimationSpeed).SetEase(Ease.InSine));
            appearAnimation.Insert(0.05f * AnimationSpeed, animatedGameObject.DOScale(Vector3.one * 0.99f, 0.15f * AnimationSpeed).SetEase(Ease.OutCirc));
            appearAnimation.Insert(0.2f * AnimationSpeed, animatedGameObject.DOScale(Vector3.one, 0.15f * AnimationSpeed).SetEase(Ease.OutQuad));
        }
    }
}