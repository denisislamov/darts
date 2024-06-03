using UnityEngine;
using System;
using DG.Tweening;
using Dip.Features.Abstractions;
using Dip.Rewards;
using Dip.Ui.Rewards;
using Dip.Ui;
using System.Linq;

namespace Dip.Features.Darts.Ui
{
    public class DartsWidgetProgressBarController
    {
        private DartsFeatureConfig config;
        private DartsFeatureStorage saveData;
        private DartsWidgetController dartsWidgetController;

        public void Init(DartsFeatureConfig config, DartsFeatureStorage saveData)
        {
            this.config = config;
            this.saveData = saveData;
        }

        public void InitWidget(DartsWidgetController dartsWidgetController)
        {
            this.dartsWidgetController = dartsWidgetController;

            if (saveData.LevelsRewardsReceived < config.DartsLevels.Length)
            {
                int points = 0;
                int currentPointsRewardProgress = 0;
                int targetPointsRewardProgress = 0;
                for (int i = 0; i < config.DartsLevels.Length; i++)
                {
                    points += config.DartsLevels[i].Points;
                    if (saveData.PointsProgress < points)
                    {
                        points -= config.DartsLevels[i].Points;
                        currentPointsRewardProgress = saveData.PointsProgress - points;
                        targetPointsRewardProgress = config.DartsLevels[i].Points;
                        break;
                    }
                }
                dartsWidgetController.DartsWidgetProgressBar.SetProgress(currentPointsRewardProgress, targetPointsRewardProgress);
                var nextLevel = config.DartsLevels[saveData.LevelsRewardsReceived];
                if (nextLevel.PackRewardInfo.PackRewardViewId.IsNullOrEmpty())
                {
                    RewardInfo specialRewards = new RewardInfo();
                    MainManager.Instance.FeaturesManager.ForeachActiveFeatures<ISpecialRewardable>(feature =>
                    {
                        string eventId = ISpecialRewardable.FeaturesIds.Darts + (saveData.LevelsRewardsReceived + 1).ToString();
                        specialRewards = feature.GetRewards(eventId, true);
                    });
                    RewardInfo rewardInfo = specialRewards.Type.IsNullOrEmpty() ? nextLevel.PackRewardInfo.RewardInfos[0] : specialRewards;
                    var control = dartsWidgetController.DartsWidgetProgressBar.CreateRewardItem(rewardInfo.Type, rewardInfo.Subtype);
                    var countControl = control as RewardItemCountControl;
                    if (countControl != null)
                    {
                        countControl.SetCount(rewardInfo.Amount);
                    }
                }
                else
                {
                    dartsWidgetController.DartsWidgetProgressBar.CreateSpecialRewardItem(nextLevel.PackRewardInfo.PackRewardViewId);
                }
            }
        }

        public void PlayAnimation(Action callback)
        {
            int points = 0;
            int currentRewardsLevel = 0;
            for (int i = 0; i < config.DartsLevels.Length; i++)
            {
                points += config.DartsLevels[i].Points;
                if (saveData.PointsProgress >= points)
                {
                    currentRewardsLevel++;
                }
                else
                {
                    break;
                }
            }
            int lastRewardsLevel = saveData.LevelsRewardsReceived;
            if (lastRewardsLevel >= config.DartsLevels.Length ||
                saveData.LastScore == 0)
            {
                callback?.Invoke();
            }
            else if (currentRewardsLevel == lastRewardsLevel &&
                     saveData.LastScore > 0)
            {
                var showAnimation = DOTween.Sequence();
                showAnimation = dartsWidgetController.DartsWidgetProgressBar.PlayShowAnimation();
                showAnimation.AppendCallback(() =>
                {
                    dartsWidgetController.DartsWidgetProgressBar.AddProgress(saveData.LastScore, () =>
                    {
                        var hideAnimation = DOTween.Sequence();
                        hideAnimation = dartsWidgetController.DartsWidgetProgressBar.PlayHideAnimation();
                        hideAnimation.AppendCallback(() => callback?.Invoke());
                        hideAnimation.Play();
                    });
                });
                showAnimation.Play();
            }
            else if (currentRewardsLevel > lastRewardsLevel)
            {
                var showAnimation = DOTween.Sequence();
                showAnimation = dartsWidgetController.DartsWidgetProgressBar.PlayShowAnimation();
                showAnimation.AppendCallback(() =>
                {
                    int targetPoints = 0;
                    for (int i = 0; i <= lastRewardsLevel; i++)
                    {
                        targetPoints += config.DartsLevels[i].Points;
                    }

                    dartsWidgetController.DartsWidgetProgressBar.AddProgress(Mathf.Clamp(targetPoints - saveData.LastPointsProgress, 0, config.DartsLevels[lastRewardsLevel].Points), () =>
                    {
                        RecursivePlay();
                    });
                });
                showAnimation.Play();
            }
            else
            {
                callback?.Invoke();
            }

            void RecursivePlay()
            {
                int targetPoints = 0;
                lastRewardsLevel++;

                if (lastRewardsLevel <= currentRewardsLevel &&
                    lastRewardsLevel < config.DartsLevels.Length)
                {
                    var nextLevel = config.DartsLevels[lastRewardsLevel];

                    int lastPoints = 0;
                    int nextPoints = 0;
                    for (int i = 0; i < lastRewardsLevel; i++)
                    {
                        lastPoints += config.DartsLevels[i].Points;
                    }
                    nextPoints = lastPoints + nextLevel.Points;
                    targetPoints = Mathf.Clamp(saveData.PointsProgress - lastPoints, 0, nextLevel.Points);

                    PlaySwitchPrizeAnimation(() =>
                    {
                        if (nextLevel.PackRewardInfo.PackRewardViewId.IsNullOrEmpty())
                        {
                            RewardInfo specialRewards = new RewardInfo();
                            MainManager.Instance.FeaturesManager.ForeachActiveFeatures<ISpecialRewardable>(feature =>
                            {
                                string eventId = ISpecialRewardable.FeaturesIds.Darts + (lastRewardsLevel + 1).ToString();
                                specialRewards = feature.GetRewards(eventId, true);
                            });
                            RewardInfo rewardInfo = specialRewards.Type.IsNullOrEmpty() ? nextLevel.PackRewardInfo.RewardInfos[0] : specialRewards;

                            var control = dartsWidgetController.DartsWidgetProgressBar.CreateRewardItem(rewardInfo.Type, rewardInfo.Subtype);
                            var countControl = control as RewardItemCountControl;
                            if (countControl != null)
                            {
                                countControl.SetCount(rewardInfo.Amount);
                            }
                        }
                        else
                        {
                            dartsWidgetController.DartsWidgetProgressBar.CreateSpecialRewardItem(nextLevel.PackRewardInfo.PackRewardViewId);
                        }
                    },
                    () =>
                    {
                        dartsWidgetController.DartsWidgetProgressBar.SetProgress(0, nextLevel.Points);

                        dartsWidgetController.DartsWidgetProgressBar.AddProgress(targetPoints, () =>
                                            {
                                                RecursivePlay();
                                            });
                    },
                    () =>
                    {
                        // dartsWidgetController.DartsWidgetProgressBar.AddProgress(targetPoints, () =>
                        //                     {
                        //                         RecursivePlay();
                        //                     });
                    });
                }
                else
                {
                    var animation = dartsWidgetController.DartsWidgetProgressBar.PlayHideAnimation();
                    animation.AppendCallback(() => callback?.Invoke());
                    animation.Play();
                }
            }
        }

        public void PlaySwitchPrizeAnimation(Action createNextItemCallback, Action onTopOfHideCallback, Action onShowCallback)
        {
            dartsWidgetController.DartsWidgetProgressBar.PlayPrizeParticles();
            dartsWidgetController.DartsWidgetProgressBar.PlayPrizeHide(onTopOfHideCallback, () =>
            {
                createNextItemCallback?.Invoke();
                dartsWidgetController.DartsWidgetProgressBar.PlayPrizeShow(onShowCallback);
            });
        }
    }
}
