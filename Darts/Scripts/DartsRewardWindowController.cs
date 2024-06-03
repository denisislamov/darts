using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Dip.Rewards;
using Dip.Ui;
using Dip.Ui.Rewards;
using System;
using Dip.Features.Darts.Ui.Rewards;

namespace Dip.Features.Darts
{
    public class DartsRewardWindowController : RewardsWindowControllerBase
    {
        public enum RewardTitleState
        {
            Start = 1,
            General = 2,
            Finish = 3
        }
        public DartsRewardWindowController(UiManager uiManager) : base(uiManager)
        {
        }

        public void ShowRewardsWindow(RewardTitleState titleState, Transform startPosition, DartsFeatureConfig.PackRewardInfo rewardPack, bool isCardsFinishNeeded, Action<Action, Action, Action> onCardsFinish, Action<bool> onFinish)
        {
            uiManager.Windows.Show(Constants.Windows.DartsRewardsWindow, (frame) =>
            {
                var rewardWindow = frame as DartsRewardWindow;

                rewardWindow.InitTitle(titleState);

                SpineChestRewardItemControl chestControl = null;

                uiManager.SoundManager.Play("RewardBox_Appear");
                var startPos = Vector3.zero;
                if (startPosition != null)
                {
                    startPosition.gameObject.SetActive(false);
                    startPos = startPosition.position;
                }

                rewardWindow.PlayChestRewardItem(rewardPack.PackRewardViewId, startPos, (control) =>
                    {
                        chestControl = control;
                        chestControl.transform.localScale = Vector3.one * 1.25f;
                    }, () =>
                {
                    var rewardControlsDict = new Dictionary<RewardItemControl, RewardInfo>();
                    foreach (var rewardInfo in rewardPack.RewardInfos)
                    {
                        var control = rewardWindow.CreateChestRewards(rewardInfo.Type, rewardInfo.Subtype);

                        rewardWindow.CreateRewardFx(control);

                        SetupRewardItemControl(control, rewardInfo);

                        rewardControlsDict.Add(control, rewardInfo);
                    }

                    uiManager.SoundManager.Play("RewardBox_Open");

                    rewardWindow.PlayChestRewards(null);
                    rewardWindow.PlayChestOpen(chestControl, () =>
                    {
                        uiManager.SoundManager.Play("RewardBox_Trails");

                        rewardWindow.HideTitle();
                        rewardWindow.HidePressToTakeReward();
                        if (!isCardsFinishNeeded)
                            rewardWindow.FadeOut();
                        rewardWindow.PlayHideChest(chestControl, null);

                        int completedRewards = 0;
                        var currentLevelButtonTarget = 0;
                        var levelButtonTargets = 0;

                        PlayCardsRewards(rewardWindow, isCardsFinishNeeded, rewardControlsDict, onCardsFinish, PlayRealCompleteRewards);

                        void PlayRealCompleteRewards()
                        {
                            var metaScreen = ForceRequestMetaScreen();

                            foreach (var pair in rewardControlsDict)
                            {
                                var control = pair.Key;
                                var rewardInfo = pair.Value;

                                var targetPosition = GetTargetPosition(rewardInfo.Type, rewardInfo.Subtype, rewardWindow);
                                if (rewardInfo.Type == Dip.Constants.RewardType.Currency)
                                {
                                    if (rewardInfo.Subtype == Dip.Constants.CurrencyRewardTypeSubtypes.Hard)
                                    {
                                        var finalAmount = int.Parse(metaScreen.HardCurrencyBar.MainText.text) + rewardInfo.Amount;
                                        rewardWindow.PlayHideCurrencyAnimation(control, rewardInfo.Amount, targetPosition,
                                            onCoinReachedTarget: (amount) =>
                                            {
                                                var currentCurrencyValue = int.Parse(metaScreen.HardCurrencyBar.MainText.text);
                                                metaScreen.HardCurrencyBar.AddStat((currentCurrencyValue + amount).ToString());
                                            },
                                            onCompleteCallback: () =>
                                            {
                                                var currentCurrencyValue = int.Parse(metaScreen.HardCurrencyBar.MainText.text);
                                                metaScreen.HardCurrencyBar.SetData(finalAmount.ToString());

                                                TryCompleteRewards();
                                            });
                                    }
                                }
                                else
                                {
                                    rewardWindow.PlayFlyToTargetAnimation(control, targetPosition, () =>
                                    {
                                        if (IsLevelButtonTarget(rewardInfo.Type))
                                        {
                                            currentLevelButtonTarget++;
                                            var isFinal = currentLevelButtonTarget == levelButtonTargets;
                                            metaScreen.LevelButton.InEffect(isFinal);
                                            metaScreen.EndContentRoundButton.InEffect(isFinal);
                                            metaScreen.EndContentLeaderboardButton.InEffect(isFinal);
                                            if (rewardInfo.Type == Dip.Constants.RewardType.Multiplier)
                                            {
                                                var multiplierManager = MainManager.Instance.MultiplierManager;
                                                metaScreen.SetLevelX2Data(multiplierManager.IsMultiplierActive, multiplierManager.TimeLeft);
                                            }
                                        }

                                        TryCompleteRewards();
                                    }, useIndexDelay: true);
                                }
                            }

                            void TryCompleteRewards()
                            {
                                completedRewards++;
                                if (completedRewards == rewardControlsDict.Count)
                                {
                                    uiManager.HideActiveFrame(rewardWindow);
                                    onFinish?.Invoke(true);
                                    onFinish = null;
                                }
                            }
                        }
                    });
                },
                    onChestFlyCompleted: () =>
                {
                    uiManager.HideActiveFrame(uiManager.GetFrame(Dip.Ui.Constants.Windows.KingsCupLeaderBoardWindow));
                });
            });
        }

        public void ShowRewardsWindow(RewardTitleState titleState, RewardInfo[] rewards, bool isCardsFinishNeeded, Action<Action, Action, Action> onCardsFinish, Action<bool> onFinish)
        {
            uiManager.Windows.Show(Constants.Windows.DartsRewardsWindow, (frame) =>
            {
                var rewardWindow = frame as DartsRewardWindow;

                rewardWindow.InitTitle(titleState);

                var rewardControlsDict = new Dictionary<RewardItemControl, RewardInfo>();
                foreach (var rewardInfo in rewards)
                {
                    var control = rewardWindow.CreateChestRewards(rewardInfo.Type, rewardInfo.Subtype);

                    rewardWindow.CreateRewardFx(control);

                    SetupRewardItemControl(control, rewardInfo);

                    rewardControlsDict.Add(control, rewardInfo);
                }
                uiManager.SoundManager.Play("RewardBox_Trails");

                rewardWindow.PlayChestRewards(null);

                int completedRewards = 0;
                var currentLevelButtonTarget = 0;
                var levelButtonTargets = 0;

                rewardWindow.PlayShowRewardAnimation(() =>
                {
                    rewardWindow.HideTitle();
                    rewardWindow.HidePressToTakeReward();
                    if (!isCardsFinishNeeded)
                    {
                        rewardWindow.FadeOut();
                    }

                    PlayCardsRewards(rewardWindow, isCardsFinishNeeded, rewardControlsDict, onCardsFinish, PlayRealCompleteRewards);
                });

                void PlayRealCompleteRewards()
                {
                    var metaScreen = uiManager.Tabs.GetActiveTab() as MetaScreen;
                    if (metaScreen == null)
                    {
                        uiManager.Tabs.ShowTab(Dip.Ui.Constants.Windows.MetaScreen, isNeedForcedShow: true);
                        metaScreen = uiManager.Tabs.GetActiveTab() as MetaScreen;
                    }

                    foreach (var pair in rewardControlsDict)
                    {
                        var control = pair.Key;
                        var rewardInfo = pair.Value;

                        var targetPosition = GetTargetPosition(rewardInfo.Type, rewardInfo.Subtype, rewardWindow);
                        if (rewardInfo.Type == Dip.Constants.RewardType.Currency)
                        {
                            if (rewardInfo.Subtype == Dip.Constants.CurrencyRewardTypeSubtypes.Hard)
                            {
                                var finalAmount = int.Parse(metaScreen.HardCurrencyBar.MainText.text) + rewardInfo.Amount;
                                rewardWindow.PlayHideCurrencyAnimation(control, rewardInfo.Amount, targetPosition,
                                    onCoinReachedTarget: (amount) =>
                                    {
                                        var currentCurrencyValue = int.Parse(metaScreen.HardCurrencyBar.MainText.text);
                                        metaScreen.HardCurrencyBar.AddStat((currentCurrencyValue + amount).ToString());
                                    },
                                    onCompleteCallback: () =>
                                    {
                                        var currentCurrencyValue = int.Parse(metaScreen.HardCurrencyBar.MainText.text);
                                        metaScreen.HardCurrencyBar.SetData(finalAmount.ToString());

                                        TryCompleteRewards();
                                    });
                            }
                        }
                        else
                        {
                            rewardWindow.PlayFlyToTargetAnimation(control, targetPosition, () =>
                            {
                                if (IsLevelButtonTarget(rewardInfo.Type))
                                {
                                    currentLevelButtonTarget++;
                                    var isFinal = currentLevelButtonTarget == levelButtonTargets;
                                    metaScreen.LevelButton.InEffect(isFinal);
                                    metaScreen.EndContentRoundButton.InEffect(isFinal);
                                    metaScreen.EndContentLeaderboardButton.InEffect(isFinal);

                                    if (rewardInfo.Type == Dip.Constants.RewardType.Multiplier)
                                    {
                                        var multiplierManager = MainManager.Instance.MultiplierManager;
                                        metaScreen.SetLevelX2Data(multiplierManager.IsMultiplierActive, multiplierManager.TimeLeft);
                                    }
                                }

                                TryCompleteRewards();
                            }, useIndexDelay: true);
                        }
                    }

                    void TryCompleteRewards()
                    {
                        completedRewards++;
                        if (completedRewards == rewardControlsDict.Count)
                        {
                            uiManager.HideActiveFrame(rewardWindow);
                            onFinish?.Invoke(true);
                            onFinish = null;
                        }
                    }
                }
            });
        }
    }
}
