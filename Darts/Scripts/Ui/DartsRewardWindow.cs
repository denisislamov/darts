using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using Dip.Ui;
using Dip.Ui.Rewards;
using UnityEngine;
using System;

namespace Dip.Features.Darts.Ui.Rewards
{
    public class DartsRewardWindow : RewardWindowBase
    {
        [SerializeField] private List<GameObject> max9EndPositions;

        [SerializeField] private GameObject rewardsFx;
        [SerializeField] private float rewardsFxScale;

        [SerializeField] protected GameObject startRewardTitle;
        [SerializeField] protected GameObject generalRewardTitle;
        [SerializeField] protected GameObject finishRewardTitle;

        private Action onNewContinueClick;

        private bool isUpdateSkipAvailable = false;

        protected override List<GameObject> CalculateBoostersTargetPositions(int currentRewardsCount)
        {
            if (currentRewardsCount > 13)
            {
                CustomDebug.LogError($"No available slots for {currentRewardsCount} rewards");
                return null;
            }
            return currentRewardsCount switch
            {
                //13 => max13EndPositions,
                //12 => max12EndPositions,
                //11 => max11EndPositions,
                //10 => max10EndPositions,
                9 => max9EndPositions,
                8 => max8EndPositions,
                7 => max7EndPositions,
                6 => max6EndPositions,
                5 => max5EndPositions,
                4 => max4EndPositions,
                3 => max3EndPositions,
                2 => max2EndPositions,
                1 => max1EndPositions,
                _ => max1EndPositions
            };
        }

        protected override void Awake()
        {
            isUpdateSkipAvailable = false;

            base.Awake();
        }

        private void Update()
        {
            if (isUpdateSkipAvailable &&
                Input.GetMouseButtonDown(0) &&
                continueButton.interactable)
            {
                continueButton.interactable = false;
                onNewContinueClick?.Invoke();
                onNewContinueClick = null;
            }
        }

        protected override void OnShow()
        {
            base.OnShow();

            countText.GetComponent<Canvas>().sortingLayerName = "UI_Windows";
        }

        public void InitTitle(DartsRewardWindowController.RewardTitleState titleState)
        {
            rewardTitle = titleState == DartsRewardWindowController.RewardTitleState.Start ? startRewardTitle : (titleState == DartsRewardWindowController.RewardTitleState.Finish ? finishRewardTitle : generalRewardTitle);
        }

        protected override Tween ShowTitle(GameObject title)
        {
            var sequence = DOTween.Sequence();

            if (title == null)
            {
                return sequence;
            }

            title.SetActive(true);
            sequence.Insert(0.1f, title.GetComponent<ScreenTitleLocalize>().PlayAppearAnimation());

            return sequence;
        }

        public void PlayShowRewardAnimation(Action onClickCallback)
        {
            isUpdateSkipAvailable = true;

            onNewContinueClick = onClickCallback;

            var sequence = DOTween.Sequence();

            sequence.Insert(0.5f, ShowTitle(rewardTitle));
            sequence.InsertCallback(0.3f, () => { UiManager.Instance.SoundManager.Play("CascadeEventMultipleRewardShow"); });
            sequence.Insert(0.0f, FadeIn());
            sequence.Insert(0.7f, ShowPressToTakeReward());

            sequence.OnComplete(() =>
            {
                ReadyToClick(onClickCallback);
            });
        }

        public void CreateRewardFx(RewardItemControl control)
        {
            GameObject fx = Instantiate(rewardsFx, control.transform);
            fx.transform.SetAsFirstSibling();
            fx.transform.localScale = Vector3.one * rewardsFxScale;
        }
    }
}
