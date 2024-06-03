using DG.Tweening;
using System;
using System.Collections;
using UnityEngine;
using Dip.Features.Abstractions;
using Dip.Features.Core;
using Dip.Features.Core.Triggers;
using Dip.Utils;
using Dip.Utils.TimeCheck;
using Dip.Ui;
using Dip.MetaGame.MultiplierBonus;
using System.Linq;
using Dip.Rewards;
using Dip.Features.Common;
using SpriteAtlasLoading;
using Dip.PushNotifications;
using Dip.SavedData;
using Dip.Storable;
using Dip.GameStates;
using Dip.Configs.Features;
using Dip.MetaGame.Audio;
using Dip.MetaGame.Rewards;
using Dip.MetaGame.WidgetManager;
using Object = UnityEngine.Object;
using Dip.Features.Darts.Ui;
using Dip.Features.UserProfile;
using Dip.Features.UserProfile.Common;
using Dip.Leaderboards;
using Dip.Managers;
using Dip.Utils.Networking;
using Ui.Manager;
using System.Collections.Generic;
using Dip.Analytics;
using Dip.Match3.WindowFactory;
using Dip.Ui.Rewards;
using Dip.Features.SeasonCollections;
using Dip.Ui.Tooltips.MultiplyBonus;
using Dip.SoundManagerSystem;
using Dip.MetaGame.Match3;
using MoreMountains.NiceVibrations;

namespace Dip.Features.Darts
{
    public class DartsFeatureController : ControllerBase<DartsFeatureConfig, int>,
        IEnterMetaHandler, IUpdatable, IMatch3LevelCompleteHandler, IEndLevelAnimationPlayer,
        IMultiplierBonusIconProvider, IEnterMetaShowChainHandler, IPushNotificationRegister,
        IPreEnterMatch3Handler, IWidgetable, ILoseProgressProvider, IStartupChainHandler
    {
        private const string SoundsListConfigId = "SoundsList";
        private const string itemToShieldWidgetKey = "ItemToShieldWidget";
        private const string DartsLoseRewardKey = "DartsLoseReward";
        private static readonly object WidgetScoreTweenId = new();

        private LeaderboardsManager leaderboardsManager;

        private ITimeCheckManager timeCheckManager;
        private readonly IMultiplierTooltipContextProvider multiplierTooltipContextProvider;

        private MultiplierManager multiplierManager;
        private RewardEnroller rewardEnroller;
        private DartsFeatureStorage saveData;
        private UiSoundManagerWrapper soundManagerWrapper;
        private InternetManager internetManager;
        private readonly FeatureLeaderboardBalanceManager balanceManager;
        private DartsWidgetController widgetController;
        private DartsWidgetMultiplierBarController widgetMultiplierBarController;
        private DartsWidgetProgressBarController widgetProgressBarController;

        private DartsStartWindow startWindow;
        private DartsMainWindow mainWindow;
        private DartsInfoWindow instructionsWindow;
        private DartsLeaderboardWindow leaderboardWindow;
        private DartsCascadeWindow dartsCascadeWindow;

        private DateTime lastTimerUpdate;
        private TimeSpan timerUpdateInterval = TimeSpan.FromSeconds(1);

        private bool isStartWindowShown = true;
        private bool isCompletedWindowShown;

        private bool isRewardsReceivingFlow = false;
        private bool isInitWidgetInProgress = false;
        private bool isInitWidgetInProgressAvailable = true;

        private Action<bool, bool> onFinish;

        private bool needPlayUnlock;

        private bool showLeaderboardAnimation = true;

        public DartsState EventState => saveData.StateType;
        public TimeSpan TimeLeft => saveData.EndTime - timeCheckManager.TimeChecker.CheckedDateUtc;

        public override bool AlreadyPlayedThisId => saveData.StateType == DartsState.Rest &&
                                                    featuresScheduleController.IsTimeToStartFeature(this, out DateTime startTime, out _) &&
                                                    saveData.StartTime == startTime;

        public bool IsCanBeLocked => triggerChecker.IsTrue(config.UnlockTriggerData) &&
            !triggerChecker.IsTrue(config.StartEventTriggerData) &&
            featuresScheduleController.IsTimeToStartFeature(this, out _, out _, CalendarCheckScope.Regular) &&
            saveData.DartsId == 0 && saveData.StateType == DartsState.Rest && !CheatActivated;

        public override bool ShouldBeActiveOutsideSchedule => IsCanBeLocked || saveData.StateType != DartsState.Rest;

        public DartsFeatureController(DartsFeatureConfig config, DartsFeatureStorage saveData,
            TriggerChecker triggerChecker, LeaderboardsManager leaderboardsManager, ITimeCheckManager timeCheckManager,
            MultiplierManager multiplierManager, RewardEnroller rewardEnroller, SpriteAtlasLoader spriteAtlasLoader,
            SpriteAtlasManager spriteAtlasManager, InternetManager internetManager, FeatureLeaderboardBalanceManager balanceManager,
            ICoroutineRunner coroutineRunner, PushNotificationsManager pushNotificationsManager, IMultiplierTooltipContextProvider multiplierTooltipContextProvider)
            : base(config, 0, triggerChecker, coroutineRunner, spriteAtlasLoader, spriteAtlasManager)
        {
            this.leaderboardsManager = leaderboardsManager;
            this.timeCheckManager = timeCheckManager;
            this.multiplierManager = multiplierManager;
            this.rewardEnroller = rewardEnroller;
            this.saveData = saveData;
            this.internetManager = internetManager;
            this.balanceManager = balanceManager;
            this.multiplierTooltipContextProvider = multiplierTooltipContextProvider;

            soundManagerWrapper = new UiSoundManagerWrapper();

            widgetMultiplierBarController = new DartsWidgetMultiplierBarController();
            widgetMultiplierBarController.Init(config, saveData);
            widgetProgressBarController = new DartsWidgetProgressBarController();
            widgetProgressBarController.Init(config, saveData);

            DartsFeatureSaveController.SaveData = saveData;
        }

        protected override void RefreshBeforeActivate()
        {
            base.RefreshBeforeActivate();

            RestoreEventFromState();
        }

        protected override void Activate(Action callback)
        {
            CustomDebug.Log("DartsFeatureController Activate");

            AddAssetRef(DartsLoseRewardKey, config.LoseRewardPrefab.Resource);

            AddAssetRef(itemToShieldWidgetKey, config.ItemToShield.Resource);
            AddAssetRef(config.MetaWidgetConfig.Id, config.MetaWidgetConfig.Prefab.Resource);

            AddSpriteAtlasRefs(config.SpriteAtlasRefs.Select(x => x.Resource).ToArray());

            if (TryGetLoadedAsset<InterchangeableSoundConfigsHolder>(SoundsListConfigId, out var soundConfigsHolder))
            {
                MainManager.Instance.SoundManager.AddSoundList(soundConfigsHolder);
            }

            if (IsCanBeLocked && !IsAvailableByLevel && !CheatActivated)
            {
                DartsFeatureSaveController.SaveData.IsLocked = true;
                DartsFeatureSaveController.MarkForSaving();
            }

            callback?.Invoke();
        }

        protected override void Deactivate(Action callback)
        {
            var uiManager = UiManager.Instance;
            uiManager.Windows.Destroy(Constants.Windows.DartsMainWindow);
            uiManager.Windows.Destroy(Constants.Windows.DartsInstructionsWindow);
            uiManager.Windows.Destroy(Constants.Windows.DartsLeaderboardWindow);
            uiManager.Windows.Destroy(Constants.Windows.DartsRewardsProgressBarWindow);
            uiManager.Windows.Destroy(Constants.Windows.DartsRewardsWindow);
            uiManager.Windows.Destroy(Constants.Windows.DartsCascadeWindow);

            OnWidgetRemoveRequesting();

            if (TryGetLoadedAsset<InterchangeableSoundConfigsHolder>(SoundsListConfigId, out var soundConfigsHolder))
            {
                MainManager.Instance.SoundManager.RemoveSoundList(soundConfigsHolder);
            }

            callback?.Invoke();
        }

        public void ForceStart()
        {
            StartEvent();
            ((IEnterMetaHandler)this).EnterMeta(GameState.Design);
        }

        public void ShowStartWindow(bool isOutOfQueue, ShowingQueueType queueType = ShowingQueueType.ReplaceServed)
        {
            var uiManager = UiManager.Instance;
            uiManager.Windows.Show(Constants.Windows.DartsStartWindow, queueType,
                frame => OnDartsStartWindowShown(frame as DartsStartWindow, isOutOfQueue));

            CustomDebug.Log("DartsFeatureController ShowStartWindow");
        }

        public void ShowMainWindow(Action callback)
        {
            ChooseAvatar.ChooseAvatarHelper.CheckAvatarAndNameSet(() =>
            {
                var uiManager = UiManager.Instance;
                uiManager.Windows.Show(Constants.Windows.DartsMainWindow,
                    frame => OnDartsMainWindowShown(frame as DartsMainWindow, callback));

                CustomDebug.Log("DartsFeatureController ShowMainWindow");
            });
        }

        public void ShowRewardsWindow()
        {
            CustomDebug.Log("DartsFeatureController ShowRewardsWindow");
        }

        public void ShowInstructionsWindow(Action callback)
        {
            var uiManager = UiManager.Instance;
            uiManager.Windows.Show(Constants.Windows.DartsInstructionsWindow,
                frame => OnDartsInstructionsWindowShown(frame as DartsInfoWindow, callback));

            CustomDebug.Log("DartsFeatureController ShowInstructionsWindow");
        }

        public void ShowLeaderboardWindow(bool isCompleted = false)
        {
            var uiManager = UiManager.Instance;
            uiManager.Windows.Show(Constants.Windows.DartsLeaderboardWindow,
                frame => OnDartsLeaderboardWindowShown(frame as DartsLeaderboardWindow, isCompleted));
        }

        public void ShowRewardsProgressBarWindow()
        {
            CustomDebug.Log("DartsFeatureController ShowRewardsProgressBarWindow");
        }

        void IEnterMetaHandler.EnterMeta(GameState fromState)
        {
            CustomDebug.Log("DartsFeatureController EnterMeta");
            if (saveData.StateType != DartsState.Rest)
            {
                return;
            }

            StartEvent();

            if (!saveData.IsLocked)
            {
                return;
            }

            saveData.IsLocked = false;
            needPlayUnlock = true;
        }

        bool IEnterMetaShowChainHandler.HasCommonStateToShow
        {
            get
            {
                if (saveData.StateType == DartsState.Announce && !isStartWindowShown)
                {
                    return true;
                }
                return false;
            }
        }

        bool IEnterMetaShowChainHandler.HasCompletionStateToShow
        {
            get
            {
                if (saveData.StateType == DartsState.Completed)
                {
                    return true;
                }

                if (saveData.StateType == DartsState.InProgress)
                {
                    int points = 0;
                    int currentRewardProgress = saveData.LevelsRewardsReceived;
                    int targetRewardProgress = 0;
                    for (int i = 0; i < config.DartsLevels.Length; i++)
                    {
                        points += config.DartsLevels[i].Points;
                        if (saveData.PointsProgress < points)
                        {
                            targetRewardProgress = i;
                            break;
                        }
                    }
                    if (currentRewardProgress < targetRewardProgress)
                    {
                        return true;
                    }
                    else
                    {
                        return saveData.LastMultipliersProgress != saveData.MultipliersProgress &&
                                (saveData.WatchedMultipliers.IsNullOrEmpty() ||
                                !saveData.WatchedMultipliers.Contains(saveData.MultipliersProgress));
                    }
                }

                return false;
            }
        }

        void IEnterMetaShowChainHandler.Start(GameState fromState, int currentIndex, int overallCount, int handledCount, Action<bool, bool> onFinish)
        {
            CustomDebug.Log("DartsFeatureController IEnterMetaShowChainHandler.Start");
            this.onFinish = onFinish;
            CustomDebug.Log($"Start {GetType()} {((IEnterMetaShowChainHandler)this).GetPriority()}");

            switch (saveData.StateType)
            {
                case DartsState.Announce:
                    UpdateWidget();
                    if (!isStartWindowShown)
                    {
                        isStartWindowShown = true;
                        ShowStartWindow(false);
                    }
                    else
                    {
                        this.onFinish?.Invoke(false, true);
                        this.onFinish = null;
                    }
                    break;
                case DartsState.PreStarted:
                    this.onFinish?.Invoke(false, true);
                    this.onFinish = null;
                    break;
                case DartsState.InProgress:
                    UpdateWidget();
                    isRewardsReceivingFlow = true;
                    PlayInProgressSequence();
                    break;
                case DartsState.Completed:
                    UpdateWidget();
                    if (!isCompletedWindowShown)
                    {
                        isCompletedWindowShown = true;
                        ShowLeaderboardWindow(true);
                    }
                    else
                    {
                        this.onFinish?.Invoke(false, true);
                        this.onFinish = null;
                    }
                    break;
                case DartsState.Rest:
                default:
                    this.onFinish?.Invoke(false, true);
                    this.onFinish = null;
                    break;
            }

            void PlayInProgressSequence()
            {
                if (saveData.LevelsRewardsReceived < config.DartsLevels.Length)
                {
                    int points = 0;
                    int currentRewardProgress = saveData.LevelsRewardsReceived;
                    int targetRewardProgress = config.DartsLevels.Length;
                    for (int i = 0; i < config.DartsLevels.Length; i++)
                    {
                        points += config.DartsLevels[i].Points;
                        if (saveData.PointsProgress < points)
                        {
                            targetRewardProgress = i;
                            break;
                        }
                    }
                    if (currentRewardProgress < targetRewardProgress)
                    {
                        RewardInfo specialRewards = new RewardInfo();
                        List<RewardInfo> rewards = new List<RewardInfo>();
                        List<RewardInfo> specialRewardsInfos = new List<RewardInfo>();
                        for (int i = currentRewardProgress; i < targetRewardProgress; i++)
                        {
                            MainManager.Instance.FeaturesManager.ForeachActiveFeatures<ISpecialRewardable>(feature =>
                            {
                                string eventId = ISpecialRewardable.FeaturesIds.Darts + (i + 1).ToString();
                                specialRewards = feature.GetRewards(eventId, true);
                                List<RewardInfo> featureRewards = feature.GenerateRewards(eventId);
                                if (!featureRewards.IsNullOrEmpty())
                                {
                                    feature.ApplyRewards(featureRewards);
                                    specialRewardsInfos.AddRange(featureRewards);
                                }
                            });

                            if (config.DartsLevels[i].PackRewardInfo.PackRewardViewId.IsNullOrEmpty())
                            {
                                if (specialRewards.Type.IsNullOrEmpty())
                                {
                                    rewards.AddRange(config.DartsLevels[i].PackRewardInfo.RewardInfos);
                                }
                                else
                                {
                                    rewards.Add(specialRewards);
                                }
                            }
                            else
                            {
                                rewards.AddRange(config.DartsLevels[i].PackRewardInfo.RewardInfos);
                                rewards.Add(specialRewards);
                            }
                        }

                        List<RewardInfo> countedRewards = new List<RewardInfo>();
                        for (int i = 0; i < rewards.Count; i++)
                        {
                            int index = countedRewards.FindIndex(x => x.Type == rewards[i].Type && x.Subtype == rewards[i].Subtype);
                            if (index != -1)
                            {
                                RewardInfo rewardInfo = countedRewards[index];
                                rewardInfo.Amount += rewards[i].Amount;
                            }
                            else
                            {
                                countedRewards.Add(rewards[i]);
                            }
                        }

                        bool areSpecialRewardsAvailable = specialRewardsInfos.Count > 0;
                        int jokersCount = specialRewardsInfos.FindAll(x => x.Type == SeasonCollectionsConstants.Cards.JokerCardId).Count;
                        bool areCardsAvailable = areSpecialRewardsAvailable && specialRewardsInfos.Count != jokersCount;
                        bool isJokerAvailable = areSpecialRewardsAvailable && jokersCount > 0;

                        DartsRewardWindowController.RewardTitleState titleState = saveData.LevelsRewardsReceived == 0 ? DartsRewardWindowController.RewardTitleState.Start : DartsRewardWindowController.RewardTitleState.General;
                        DartsRewardWindowController rewardWindowController = new DartsRewardWindowController(UiManager.Instance);
                        if (areSpecialRewardsAvailable)
                        {
                            if (areCardsAvailable)
                            {
                                rewardWindowController.ShowRewardsWindow(titleState, rewards.ToArray(), true, (callback1, callback2, callback3) =>
                                {
                                    MainManager.Instance.FeaturesManager.ForeachActiveFeatures<ISpecialRewardable>(feature =>
                                    {
                                        feature.ShowRewards(Vector3.zero, ISpecialRewardable.FeaturesIds.Darts + (currentRewardProgress + 1).ToString(), specialRewardsInfos, () => callback3?.Invoke(), false, preCallback1: () => callback1?.Invoke(), preCallback2: () => callback2?.Invoke());
                                    });
                                }, (result) =>
                                {
                                    MainManager.Instance.FeaturesManager.ForeachActiveFeatures<ISpecialRewardable>(feature =>
                                    {
                                        feature.ShowRewards(Vector3.zero, ISpecialRewardable.FeaturesIds.Darts + (currentRewardProgress + 1).ToString(), specialRewardsInfos, () =>
                                        {
                                            rewardEnroller.ApplyRewards(rewards.ToArray(), "darts_stage_completed", AnalyticsCategoryKeys.DartsEvent, false);

                                            UiManager.Instance.HideAllWindows();

                                            saveData.LastPointsProgress = saveData.PointsProgress;
                                            saveData.LevelsRewardsReceived = targetRewardProgress;

                                            if (saveData.LastMultipliersProgress != saveData.MultipliersProgress &&
                                                (saveData.WatchedMultipliers.IsNullOrEmpty() ||
                                                !saveData.WatchedMultipliers.Contains(saveData.MultipliersProgress)))
                                            {
                                                ShowMainWindow(null);
                                            }
                                            else
                                            {
                                                this.onFinish?.Invoke(false, true);
                                                this.onFinish = null;
                                            }
                                        }, true, isFadeInNeeded: true);
                                    });
                                });
                            }
                            else
                            {
                                rewardWindowController.ShowRewardsWindow(titleState, rewards.ToArray(), true, (callback1, callback2, callback3) =>
                                {
                                    MainManager.Instance.FeaturesManager.ForeachActiveFeatures<ISpecialRewardable>(feature =>
                                    {
                                        feature.ShowRewards(Vector3.zero, ISpecialRewardable.FeaturesIds.Darts + (currentRewardProgress + 1).ToString(), specialRewardsInfos, () => callback3?.Invoke(), true, preCallback1: () => callback1?.Invoke(), preCallback2: () => callback2?.Invoke());
                                    });
                                }, (result) =>
                                {
                                    rewardEnroller.ApplyRewards(rewards.ToArray(), "darts_stage_completed", AnalyticsCategoryKeys.DartsEvent, false);

                                    UiManager.Instance.HideAllWindows();

                                    saveData.LastPointsProgress = saveData.PointsProgress;
                                    saveData.LevelsRewardsReceived = targetRewardProgress;

                                    if (saveData.LastMultipliersProgress != saveData.MultipliersProgress &&
                                        (saveData.WatchedMultipliers.IsNullOrEmpty() ||
                                        !saveData.WatchedMultipliers.Contains(saveData.MultipliersProgress)))
                                    {
                                        ShowMainWindow(null);
                                    }
                                    else
                                    {
                                        this.onFinish?.Invoke(false, true);
                                        this.onFinish = null;
                                    }
                                });
                            }
                        }
                        else
                        {
                            rewardWindowController.ShowRewardsWindow(titleState, rewards.ToArray(), false, null, (result) =>
                            {
                                rewardEnroller.ApplyRewards(rewards.ToArray(), "darts_stage_completed", AnalyticsCategoryKeys.DartsEvent, false);

                                UiManager.Instance.HideAllWindows();

                                saveData.LastPointsProgress = saveData.PointsProgress;
                                saveData.LevelsRewardsReceived = targetRewardProgress;

                                if (saveData.LastMultipliersProgress != saveData.MultipliersProgress &&
                                    (saveData.WatchedMultipliers.IsNullOrEmpty() ||
                                    !saveData.WatchedMultipliers.Contains(saveData.MultipliersProgress)))
                                {
                                    ShowMainWindow(null);
                                }
                                else
                                {
                                    this.onFinish?.Invoke(false, true);
                                    this.onFinish = null;
                                }
                            });
                        }
                    }
                    else
                    {
                        saveData.LastPointsProgress = saveData.PointsProgress;
                        saveData.LevelsRewardsReceived = targetRewardProgress;

                        if (saveData.LastMultipliersProgress != saveData.MultipliersProgress &&
                            (saveData.WatchedMultipliers.IsNullOrEmpty() ||
                            !saveData.WatchedMultipliers.Contains(saveData.MultipliersProgress)))
                        {
                            ShowMainWindow(null);
                        }
                        else
                        {
                            this.onFinish?.Invoke(false, true);
                            this.onFinish = null;
                        }
                    }
                }
                else
                {
                    this.onFinish?.Invoke(false, true);
                    this.onFinish = null;
                }
            }
        }

        void IPreEnterMatch3Handler.OnPreEnterMatch3(int levelNumber)
        {
            DOTween.Kill(WidgetScoreTweenId, true);
            soundManagerWrapper.StopAllPlayingSounds();
        }

        void IUpdatable.Update()
        {
            if (saveData.StateType == DartsState.Completed)
            {
                return;
            }

            var currentTime = timeCheckManager.TimeChecker.CheckedDateUtc;
            if (currentTime - lastTimerUpdate < timerUpdateInterval)
            {
                return;
            }

            lastTimerUpdate = currentTime;
            UpdateVisuals();

            if (TimeLeft > TimeSpan.Zero)
            {
                return;
            }

            switch (saveData.StateType)
            {
                case DartsState.Rest:
                    OnWidgetRemoveRequesting();
                    break;
                case DartsState.InProgress:
                    CompleteCurrentEvent();
                    break;
                case DartsState.Announce:
                case DartsState.PreStarted:
                    break;
                case DartsState.Completed:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void OnDartsStartWindowShown(DartsStartWindow frame, bool isOutOfQueue)
        {
            UiManager uiManager = UiManager.Instance;

            startWindow = frame;

            saveData.IsStartWindowsShown = true;
            startWindow.SetTimer(TimeLeft);

            startWindow.OnPlayPressed = () =>
            {
                uiManager.HideAllWindows();
                if (!internetManager.IsInternetAvailable)
                {
                    ApplicationContext.Instance.LocalUiManager.ShowNoInternetText();
                    onFinish?.Invoke(true, true);
                    return;
                }

                saveData.StateType = DartsState.PreStarted;

                SendStartAnalyticsEvent();

                //ShowInstructionsWindow();

                ApplicationContext.Instance.LocalUiManager.TryStartLevel((needContinue) =>
                    {
                        onFinish?.Invoke(true, needContinue);
                        onFinish = null;
                    });
            };
        }

        private void OnDartsMainWindowShown(DartsMainWindow frame, Action callback)
        {
            mainWindow = frame;
            mainWindow.ShowLeaderboardAnimation = showLeaderboardAnimation;
            mainWindow.SetTimer(TimeLeft);

            if (saveData.LevelsRewardsReceived < config.DartsLevels.Length)
            {
                int nextPoints = 0;
                int previousPoints = 0;
                for (int i = 0; i < config.DartsLevels.Length; i++)
                {
                    nextPoints += config.DartsLevels[i].Points;
                    if (saveData.PointsProgress < nextPoints)
                    {
                        previousPoints = nextPoints - config.DartsLevels[i].Points;
                        break;
                    }
                }

                mainWindow.SetProgress(saveData.PointsProgress - previousPoints,
                    nextPoints - previousPoints);
            }
            else
            {
                mainWindow.SetProgress(config.DartsLevels[saveData.LevelsRewardsReceived - 1].Points,
                    config.DartsLevels[saveData.LevelsRewardsReceived - 1].Points);
            }

            int index = Mathf.Clamp(saveData.LevelsRewardsReceived, 0, config.DartsLevels.Length - 1);
            RewardInfo specialRewards = new RewardInfo();
            MainManager.Instance.FeaturesManager.ForeachActiveFeatures<ISpecialRewardable>(feature =>
            {
                string eventId = ISpecialRewardable.FeaturesIds.Darts + "final_" + (index + 1).ToString();
                specialRewards = feature.GetRewards(eventId, true);
            });
            DartsFeatureConfig.PackRewardInfo packRewardInfo = config.DartsLevels[index].PackRewardInfo;
            if (packRewardInfo.PackRewardViewId.IsNullOrEmpty())
            {
                RewardInfo rewardInfo = specialRewards.Type.IsNullOrEmpty() ? packRewardInfo.RewardInfos[0] : specialRewards;
                RewardItemControl rewardItemControl = mainWindow.CreateRewardItem(rewardInfo.Type, rewardInfo.Subtype);
                rewardItemControl.gameObject.transform.localScale = new Vector3(1.1f, 1.1f, 1.1f);

                var countControl = rewardItemControl as RewardItemCountControl;
                if (countControl != null)
                {
                    countControl.SetCount(rewardInfo.Amount);
                }
                
                RewardsUtils.SetupRewardItemControlWithMultiplier(rewardItemControl, rewardInfo, null);
            }
            else
            {
                RewardItemControl rewardItemControl = mainWindow.CreateSpecialRewardItem(packRewardInfo.PackRewardViewId);
                //setup item
            }

            var userId = Storage.Get<CommonStorage>().UserId;

            mainWindow.InitProgressArrows(config.DartsMultipliers, saveData.MultipliersProgress);
            if (saveData.LastMultipliersProgress != saveData.MultipliersProgress &&
                isRewardsReceivingFlow &&
                (saveData.WatchedMultipliers.IsNullOrEmpty() ||
                !saveData.WatchedMultipliers.Contains(saveData.MultipliersProgress)))
            {
                if (saveData.WatchedMultipliers == null)
                {
                    saveData.WatchedMultipliers = new List<int>();
                }
                saveData.WatchedMultipliers.AddExclusive(saveData.MultipliersProgress);
                DartsFeatureSaveController.MarkForSaving();

                mainWindow.UpdateProgressArrows(saveData.MultipliersProgress);
                mainWindow.AnimateProgressArrows(saveData.LastMultipliersProgress, saveData.MultipliersProgress);
            }
            else
            {
                mainWindow.UpdateProgressArrows(saveData.MultipliersProgress);
            }
            isRewardsReceivingFlow = false;

            var leaderboard = leaderboardsManager.GetLeaderboard(config.LeaderboardId);

            DartsFeatureConfig.PackRewardInfo[] packRewardsInfos = new DartsFeatureConfig.PackRewardInfo[config.CompleteEventRewardInfos.Length];
            for (int i = 0; i < config.CompleteEventRewardInfos.Length; i++)
            {
                packRewardsInfos[i] = new DartsFeatureConfig.PackRewardInfo("", null);
                packRewardsInfos[i].PackRewardViewId = config.CompleteEventRewardInfos[i].PackRewardViewId;
                MainManager.Instance.FeaturesManager.ForeachActiveFeatures<ISpecialRewardable>(feature =>
                {
                    string eventId = ISpecialRewardable.FeaturesIds.Darts + "final_" + (i + 1).ToString();
                    specialRewards = feature.GetRewards(eventId, true);
                });
                if (packRewardsInfos[i].PackRewardViewId.IsNullOrEmpty())
                {
                    if (!specialRewards.Type.IsNullOrEmpty())
                    {
                        packRewardsInfos[i].RewardInfos = new RewardInfo[1];
                        packRewardsInfos[i].RewardInfos[0] = specialRewards;
                    }
                    else
                    {
                        packRewardsInfos[i].RewardInfos = new RewardInfo[config.CompleteEventRewardInfos[i].RewardInfos.Length];
                        for (int j = 0; j < packRewardsInfos[i].RewardInfos.Length; j++)
                        {
                            packRewardsInfos[i].RewardInfos[j] = config.CompleteEventRewardInfos[i].RewardInfos[j];
                        }
                    }
                }
                else
                {
                    if (!specialRewards.Type.IsNullOrEmpty())
                    {
                        packRewardsInfos[i].RewardInfos = new RewardInfo[config.CompleteEventRewardInfos[i].RewardInfos.Length + 1];
                        for (int j = 0; j < packRewardsInfos[i].RewardInfos.Length; j++)
                        {
                            packRewardsInfos[i].RewardInfos[j] = config.CompleteEventRewardInfos[i].RewardInfos[j];
                        }
                        packRewardsInfos[i].RewardInfos[config.CompleteEventRewardInfos[i].RewardInfos.Length] = specialRewards;
                    }
                    else
                    {
                        packRewardsInfos[i].RewardInfos = new RewardInfo[config.CompleteEventRewardInfos[i].RewardInfos.Length];
                        for (int j = 0; j < packRewardsInfos[i].RewardInfos.Length; j++)
                        {
                            packRewardsInfos[i].RewardInfos[j] = config.CompleteEventRewardInfos[i].RewardInfos[j];
                        }
                    }
                }
            }
            //RewardInfo finalRewardInfo = (resultPosition > packRewardsInfos.Length) ? new RewardInfo() : (specialRewards.Type.IsNullOrEmpty() ? packRewardsInfos[resultPosition - 1].RewardInfos[0] : specialRewards);
            RewardInfo finalRewardInfo = new RewardInfo();
            mainWindow.SetData(leaderboard.Select(DartsPlayerDataSelector), packRewardsInfos, finalRewardInfo, false, () =>
            {
                if (!showLeaderboardAnimation)
                {
                    return;
                }
            });

            mainWindow.OnInfoPressed = () => ShowInstructionsWindow(null);
            mainWindow.OnClosePressed = () =>
            {
                UiManager.Instance.SoundManager.Play("ui_meta_click_common");
                UiManager.Instance.HideActiveFrame(mainWindow);

                onFinish?.Invoke(true, true);
                onFinish = null;

                callback?.Invoke();
                callback = null;
            };

            mainWindow.OnProgressButtonPressed = () =>
            {
                var uiManager = UiManager.Instance;
                
                MMVibrationManager.Haptic(HapticTypes.LightImpact);
                
                uiManager.Popups.Show(Constants.Windows.DartsCascadeWindow,
                    frame => OnDartsCascadeWindowShown(frame as DartsCascadeWindow));
            };

            if (showLeaderboardAnimation)
            {
                var resultPosition = leaderboard.FindIndex(x => x.userId == userId) + 1;

                mainWindow.PlayLeaderboardAnimation(resultPosition, saveData.PreviousPosition,
                    config.DartsLevels.Length, userId);
                saveData.PreviousPosition = resultPosition;
            }

            return;

            DartsLeaderboardWindow.DartsPlayerData DartsPlayerDataSelector(LeaderboardPlayerResultInfo x) => new()
            {
                name = x.userName,
                avatarId = x.avatarId,
                avatarFrame = x.userId.Equals(userId) ? Storage.Get<CommonStorage>().ProfileFrame : x.userFrameColor,
                playerNameColor = x.userId.Equals(userId) ? Storage.Get<CommonStorage>().ProfileNameColor : x.userNameColor,
                cupPoints = x.score,
                place = x.position,
                isPlayer = x.userId.Equals(userId),
                isShowingChest = x.position <= config.CompleteEventRewardInfos.Length && !config.CompleteEventRewardInfos[x.position - 1].PackRewardViewId.IsNullOrEmpty(),
                isShowingJoker = false,
                rewardsCardsCount = 0,

                OnChestPressed = () =>
                {
                    if (x.position > config.CompleteEventRewardInfos.Length)
                    {
                        return;
                    }

                    var rewardPack = config.CompleteEventRewardInfos[x.position - 1];
                    var tooltip = mainWindow.RewardListTooltip;

                    RewardInfo specialRewards = new RewardInfo();
                    MainManager.Instance.FeaturesManager.ForeachActiveFeatures<ISpecialRewardable>(feature =>
                    {
                        string eventId = ISpecialRewardable.FeaturesIds.Darts + "final_" + (index + 1).ToString();
                        specialRewards = feature.GetRewards(eventId, true);
                    });

                    tooltip.ClearRewards();
                    int additionalCapacity = (!specialRewards.Type.IsNullOrEmpty()) ? 1 : 0;
                    tooltip.SetItemsCapacity(rewardPack.RewardInfos.Length + additionalCapacity);

                    foreach (var reward in rewardPack.RewardInfos)
                    {
                        var tooltipRewardItem = tooltip.CreateRewardItem(reward.Type, reward.Subtype);
                        RewardsUtils.SetupRewardItemControl(tooltipRewardItem, reward);
                    }

                    if (!specialRewards.Type.IsNullOrEmpty())
                    {
                        var tooltipRewardItem = tooltip.CreateRewardItem(specialRewards.Type, specialRewards.Subtype);
                        RewardsUtils.SetupRewardItemControl(tooltipRewardItem, specialRewards);
                    }

                    mainWindow.ShowRewardListTooltip(mainWindow.GetPlayerWidgetChestTransform(x.position));
                },
                OnPlayerPressed = () =>
                {
                    if (MainManager.Instance.FeaturesManager.TryGetActiveFeature<UserProfileFeatureController>(out var controller))
                    {
                        controller.ShowWindow(x, config.UnlockTriggerData.Match3LevelIndex);
                    }
                }
            };
        }

        private void OnDartsInstructionsWindowShown(DartsInfoWindow window, Action callback)
        {
            instructionsWindow = window;
            instructionsWindow.SetInfo(config);
            
            UiManager.Instance.SoundManager.Play("ui_meta_lucky_info");
            
            instructionsWindow.OnCloseCallback = () =>
            {
                UiManager.Instance.HideAllWindows();
                ShowMainWindow(callback);
            };
        }

        private void OnDartsLeaderboardWindowShown(DartsLeaderboardWindow window, bool isCompleted)
        {
            leaderboardWindow = window;

            var userId = Storage.Get<CommonStorage>().UserId;
            var leaderboard = leaderboardsManager.GetLeaderboard(config.LeaderboardId);
            var player = leaderboardsManager.GetLeaderboardAroundPlayer(config.LeaderboardId, Storage.Get<CommonStorage>().UserId, 1);
            if (player == null || player.Count < 1)
            {
                Debug.LogError($"Invalid leaderboard {config.LeaderboardId}");
                return;
            }
            var resultPosition = player[0].position;

            if (resultPosition > config.CompleteEventRewardInfos.Length)
            {
                saveData.IsFinalRewardReceived = true;
                DartsFeatureSaveController.MarkForSaving();

                UpdateWidget();
            }

            RewardInfo specialRewards = new RewardInfo();
            int index = Mathf.Clamp(saveData.LevelsRewardsReceived, 0, config.DartsLevels.Length - 1);
            MainManager.Instance.FeaturesManager.ForeachActiveFeatures<ISpecialRewardable>(feature =>
            {
                string eventId = ISpecialRewardable.FeaturesIds.Darts + "final_" + (index + 1).ToString();
                specialRewards = feature.GetRewards(eventId, true);
            });

            DartsFeatureConfig.PackRewardInfo[] packRewardsInfos = new DartsFeatureConfig.PackRewardInfo[config.CompleteEventRewardInfos.Length];
            for (int i = 0; i < config.CompleteEventRewardInfos.Length; i++)
            {
                packRewardsInfos[i] = new DartsFeatureConfig.PackRewardInfo("", null);
                packRewardsInfos[i].PackRewardViewId = config.CompleteEventRewardInfos[i].PackRewardViewId;
                MainManager.Instance.FeaturesManager.ForeachActiveFeatures<ISpecialRewardable>(feature =>
                {
                    string eventId = ISpecialRewardable.FeaturesIds.Darts + "final_" + (i + 1).ToString();
                    specialRewards = feature.GetRewards(eventId, true);
                });
                if (packRewardsInfos[i].PackRewardViewId.IsNullOrEmpty())
                {
                    if (!specialRewards.Type.IsNullOrEmpty())
                    {
                        packRewardsInfos[i].RewardInfos = new RewardInfo[1];
                        packRewardsInfos[i].RewardInfos[0] = specialRewards;
                    }
                    else
                    {
                        packRewardsInfos[i].RewardInfos = new RewardInfo[config.CompleteEventRewardInfos[i].RewardInfos.Length];
                        for (int j = 0; j < packRewardsInfos[i].RewardInfos.Length; j++)
                        {
                            packRewardsInfos[i].RewardInfos[j] = config.CompleteEventRewardInfos[i].RewardInfos[j];
                        }
                    }
                }
                else
                {
                    if (!specialRewards.Type.IsNullOrEmpty())
                    {
                        packRewardsInfos[i].RewardInfos = new RewardInfo[config.CompleteEventRewardInfos[i].RewardInfos.Length + 1];
                        for (int j = 0; j < packRewardsInfos[i].RewardInfos.Length; j++)
                        {
                            packRewardsInfos[i].RewardInfos[j] = config.CompleteEventRewardInfos[i].RewardInfos[j];
                        }
                        packRewardsInfos[i].RewardInfos[config.CompleteEventRewardInfos[i].RewardInfos.Length] = specialRewards;
                    }
                    else
                    {
                        packRewardsInfos[i].RewardInfos = new RewardInfo[config.CompleteEventRewardInfos[i].RewardInfos.Length];
                        for (int j = 0; j < packRewardsInfos[i].RewardInfos.Length; j++)
                        {
                            packRewardsInfos[i].RewardInfos[j] = config.CompleteEventRewardInfos[i].RewardInfos[j];
                        }
                    }
                }
            }
            RewardInfo finalRewardInfo = (resultPosition > packRewardsInfos.Length) ? new RewardInfo() : (specialRewards.Type.IsNullOrEmpty() ? packRewardsInfos[resultPosition - 1].RewardInfos[0] : specialRewards);
            leaderboardWindow.SetData(leaderboard.Select(DartsPlayerDataSelector), packRewardsInfos, finalRewardInfo, isCompleted, saveData.IsFinalRewardReceived);
            leaderboardWindow.OnInfoPressed = () => ShowInstructionsWindow(null);
            leaderboardWindow.OnClosePressed = () =>
            {
                if (resultPosition > config.CompleteEventRewardInfos.Length)
                {
                    UiManager.Instance.HideAllWindows();

                    // FinishCurrentEvent();
                }

                onFinish?.Invoke(true, true);
                onFinish = null;
            };
            leaderboardWindow.OnClaimButtonPressed = () =>
            {
                if (!internetManager.IsInternetAvailable)
                {
                    UiManager.Instance.HideAllWindows();
                    ApplicationContext.Instance.LocalUiManager.ShowNoInternetText();
                    onFinish?.Invoke(true, true);
                    onFinish = null;
                    return;
                }

                string eventId = ISpecialRewardable.FeaturesIds.Darts + "final_" + (resultPosition + 1).ToString();
                RewardInfo specialRewards = new RewardInfo();
                List<RewardInfo> specialRewardsInfos = new List<RewardInfo>();
                MainManager.Instance.FeaturesManager.ForeachActiveFeatures<ISpecialRewardable>(feature =>
                {
                    List<RewardInfo> rewards = feature.GenerateRewards(eventId);
                    if (!rewards.IsNullOrEmpty())
                    {
                        specialRewardsInfos = rewards;
                        feature.ApplyRewards(rewards);
                    }
                    specialRewards = feature.GetRewards(eventId, false);
                });
                bool areSpecialRewardsAvailable = !specialRewards.Type.IsNullOrEmpty();

                DartsFeatureConfig.PackRewardInfo reward = new DartsFeatureConfig.PackRewardInfo(config.CompleteEventRewardInfos[resultPosition - 1].PackRewardViewId, new RewardInfo[config.CompleteEventRewardInfos[resultPosition - 1].RewardInfos.Length + ((areSpecialRewardsAvailable && !config.CompleteEventRewardInfos[resultPosition - 1].PackRewardViewId.IsNullOrEmpty()) ? 1 : 0)]);
                for (int i = 0; i < config.CompleteEventRewardInfos[resultPosition - 1].RewardInfos.Length; i++)
                {
                    reward.RewardInfos[i] = new RewardInfo(config.CompleteEventRewardInfos[resultPosition - 1].RewardInfos[i]);
                }
                if (areSpecialRewardsAvailable)
                {
                    if (!config.CompleteEventRewardInfos[resultPosition - 1].PackRewardViewId.IsNullOrEmpty())
                    {
                        reward.RewardInfos[config.CompleteEventRewardInfos[resultPosition - 1].RewardInfos.Length] = new RewardInfo(specialRewards);
                    }
                    else
                    {
                        reward.RewardInfos[0] = new RewardInfo(specialRewards);
                    }
                }
                bool areCardsAvailable = areSpecialRewardsAvailable && specialRewardsInfos.Count > 1;
                bool isJokerAvailable = areSpecialRewardsAvailable && specialRewardsInfos.FindIndex(x => x.Type == SeasonCollectionsConstants.Cards.JokerCardId) != -1;

                if (resultPosition <= config.CompleteEventRewardInfos.Length)
                {
                    DartsRewardWindowController rewardWindowController = new DartsRewardWindowController(UiManager.Instance);
                    if (areSpecialRewardsAvailable)
                    {
                        if (areCardsAvailable)
                        {
                            if (reward.PackRewardViewId.IsNullOrEmpty())
                            {
                                rewardWindowController.ShowRewardsWindow(DartsRewardWindowController.RewardTitleState.Finish, reward.RewardInfos, true, (callback1, callback2, callback3) =>
                                {
                                    MainManager.Instance.FeaturesManager.ForeachActiveFeatures<ISpecialRewardable>(feature =>
                                    {
                                        feature.ShowRewards(Vector3.zero, eventId, specialRewardsInfos, () => callback3?.Invoke(), false, preCallback1: () => callback1?.Invoke(), preCallback2: () => callback2?.Invoke());
                                    });
                                }, (result) =>
                                {
                                    MainManager.Instance.FeaturesManager.ForeachActiveFeatures<ISpecialRewardable>(feature =>
                                    {
                                        feature.ShowRewards(Vector3.zero, eventId, specialRewardsInfos, () =>
                                        {
                                            rewardEnroller.ApplyRewards(reward.RewardInfos, "darts_completed", AnalyticsCategoryKeys.DartsEvent, false);

                                            saveData.IsFinalRewardReceived = true;
                                            DartsFeatureSaveController.MarkForSaving();

                                            UpdateWidget();

                                            UiManager.Instance.HideAllWindows();

                                            onFinish?.Invoke(true, true);
                                            onFinish = null;
                                        }, true, isFadeInNeeded: true);
                                    });
                                });
                            }
                            else
                            {
                                rewardWindowController.ShowRewardsWindow(DartsRewardWindowController.RewardTitleState.Finish, window.GetMainChestTransform(), reward, true, (callback1, callback2, callback3) =>
                                {
                                    MainManager.Instance.FeaturesManager.ForeachActiveFeatures<ISpecialRewardable>(feature =>
                                    {
                                        feature.ShowRewards(Vector3.zero, eventId, specialRewardsInfos, () => callback3?.Invoke(), false, preCallback1: () => callback1?.Invoke(), preCallback2: () => callback2?.Invoke());
                                    });
                                }, (result) =>
                                {
                                    MainManager.Instance.FeaturesManager.ForeachActiveFeatures<ISpecialRewardable>(feature =>
                                    {
                                        feature.ShowRewards(Vector3.zero, eventId, specialRewardsInfos, () =>
                                        {
                                            rewardEnroller.ApplyRewards(reward.RewardInfos, "darts_completed", AnalyticsCategoryKeys.DartsEvent, false);

                                            saveData.IsFinalRewardReceived = true;
                                            DartsFeatureSaveController.MarkForSaving();

                                            UpdateWidget();

                                            UiManager.Instance.HideAllWindows();

                                            onFinish?.Invoke(true, true);
                                            onFinish = null;
                                        }, true, isFadeInNeeded: true);
                                    });
                                });
                            }
                        }
                        else
                        {
                            if (reward.PackRewardViewId.IsNullOrEmpty())
                            {
                                rewardWindowController.ShowRewardsWindow(DartsRewardWindowController.RewardTitleState.Finish, reward.RewardInfos, true, (callback1, callback2, callback3) =>
                                {
                                    MainManager.Instance.FeaturesManager.ForeachActiveFeatures<ISpecialRewardable>(feature =>
                                    {
                                        feature.ShowRewards(Vector3.zero, eventId, specialRewardsInfos, () => callback3?.Invoke(), true, preCallback1: () => callback1?.Invoke(), preCallback2: () => callback2?.Invoke());
                                    });
                                }, (result) =>
                                {
                                    rewardEnroller.ApplyRewards(reward.RewardInfos, "darts_completed", AnalyticsCategoryKeys.DartsEvent, false);

                                    saveData.IsFinalRewardReceived = true;
                                    DartsFeatureSaveController.MarkForSaving();

                                    UpdateWidget();

                                    UiManager.Instance.HideAllWindows();

                                    onFinish?.Invoke(true, true);
                                    onFinish = null;
                                });
                            }
                            else
                            {
                                rewardWindowController.ShowRewardsWindow(DartsRewardWindowController.RewardTitleState.Finish, window.GetMainChestTransform(), reward, true, (callback1, callback2, callback3) =>
                                {
                                    MainManager.Instance.FeaturesManager.ForeachActiveFeatures<ISpecialRewardable>(feature =>
                                    {
                                        feature.ShowRewards(Vector3.zero, eventId, specialRewardsInfos, () => callback3?.Invoke(), true, preCallback1: () => callback1?.Invoke(), preCallback2: () => callback2?.Invoke());
                                    });
                                }, (result) =>
                                {
                                    rewardEnroller.ApplyRewards(reward.RewardInfos, "darts_completed", AnalyticsCategoryKeys.DartsEvent, false);

                                    saveData.IsFinalRewardReceived = true;
                                    DartsFeatureSaveController.MarkForSaving();

                                    UpdateWidget();

                                    UiManager.Instance.HideAllWindows();

                                    onFinish?.Invoke(true, true);
                                    onFinish = null;
                                });
                            }
                        }
                    }
                    else
                    {
                        if (reward.PackRewardViewId.IsNullOrEmpty())
                        {
                            rewardWindowController.ShowRewardsWindow(DartsRewardWindowController.RewardTitleState.Finish, reward.RewardInfos, false, null, (result) =>
                            {
                                rewardEnroller.ApplyRewards(reward.RewardInfos, "darts_completed", AnalyticsCategoryKeys.DartsEvent, false);

                                saveData.IsFinalRewardReceived = true;
                                DartsFeatureSaveController.MarkForSaving();

                                UpdateWidget();

                                UiManager.Instance.HideAllWindows();

                                UpdateVisuals();

                                onFinish?.Invoke(true, true);
                                onFinish = null;
                            });
                        }
                        else
                        {
                            rewardWindowController.ShowRewardsWindow(DartsRewardWindowController.RewardTitleState.Finish, window.GetMainChestTransform(), reward, false, null, (result) =>
                            {
                                rewardEnroller.ApplyRewards(reward.RewardInfos, "darts_completed", AnalyticsCategoryKeys.DartsEvent, false);

                                saveData.IsFinalRewardReceived = true;
                                DartsFeatureSaveController.MarkForSaving();

                                UiManager.Instance.HideAllWindows();

                                UpdateWidget();

                                onFinish?.Invoke(true, true);
                                onFinish = null;
                            });
                        }
                    }
                }
                else
                {
                    UiManager.Instance.HideAllWindows();

                    onFinish?.Invoke(true, true);
                    onFinish = null;
                }

                // FinishCurrentEvent();
            };
            return;

            DartsLeaderboardWindow.DartsPlayerData DartsPlayerDataSelector(LeaderboardPlayerResultInfo x) => new()
            {
                name = x.userName,
                avatarId = x.avatarId,
                avatarFrame = x.userId.Equals(userId) ? Storage.Get<CommonStorage>().ProfileFrame : x.userFrameColor,
                playerNameColor = x.userId.Equals(userId) ? Storage.Get<CommonStorage>().ProfileNameColor : x.userNameColor,
                cupPoints = x.score,
                place = x.position,
                isPlayer = x.userId.Equals(userId),
                isShowingChest = x.position <= config.CompleteEventRewardInfos.Length && !config.CompleteEventRewardInfos[x.position - 1].PackRewardViewId.IsNullOrEmpty(),
                isShowingJoker = false,
                rewardsCardsCount = 0,

                OnChestPressed = () =>
                {
                    if (x.position > config.CompleteEventRewardInfos.Length)
                    {
                        return;
                    }

                    var rewardPack = config.CompleteEventRewardInfos[x.position - 1];
                    var tooltip = leaderboardWindow.RewardListTooltip;

                    RewardInfo specialRewards = new RewardInfo();
                    MainManager.Instance.FeaturesManager.ForeachActiveFeatures<ISpecialRewardable>(feature =>
                    {
                        string eventId = ISpecialRewardable.FeaturesIds.Darts + (x.position + 1).ToString();
                        specialRewards = feature.GetRewards(eventId, true);
                    });

                    tooltip.ClearRewards();
                    int additionalCapacity = (!specialRewards.Type.IsNullOrEmpty()) ? 1 : 0;
                    tooltip.SetItemsCapacity(rewardPack.RewardInfos.Length + additionalCapacity);

                    foreach (var reward in rewardPack.RewardInfos)
                    {
                        var tooltipRewardItem = tooltip.CreateRewardItem(reward.Type, reward.Subtype);
                        RewardsUtils.SetupRewardItemControl(tooltipRewardItem, reward);
                    }

                    if (!specialRewards.Type.IsNullOrEmpty())
                    {
                        var tooltipRewardItem = tooltip.CreateRewardItem(specialRewards.Type, specialRewards.Subtype);
                        RewardsUtils.SetupRewardItemControl(tooltipRewardItem, specialRewards);
                    }

                    leaderboardWindow.ShowRewardListTooltip(leaderboardWindow.GetPlayerWidgetChestTransform(x.position));
                },
                OnPlayerPressed = () =>
                {
                    if (MainManager.Instance.FeaturesManager.TryGetActiveFeature<UserProfileFeatureController>(out var controller))
                    {
                        controller.ShowWindow(x, config.UnlockTriggerData.Match3LevelIndex);
                    }
                }
            };
        }

        private void OnDartsCascadeWindowShown(DartsCascadeWindow frame)
        {
            dartsCascadeWindow = frame;

            var animatedSteps = 0;

            DartsTaskCascade currentStepView = null;

            int currentStep = Mathf.Clamp(saveData.LevelsRewardsReceived, 0, config.DartsLevels.Length);
            // RewardItemControl rewardItemControl = mainWindow.CreateRewardItem(config.DartsLevels[currentStep].RewardInfos[0].Type, config.DartsLevels[currentStep].RewardInfos[0].Subtype);

            var currProgress = 1.0f;

            for (var index = 0; index < config.DartsLevels.Length; index++)
            {
                var control = dartsCascadeWindow.AddTaskView(index == config.DartsLevels.Length - 1);

                // TODO
                control.SetActiveLines(index != config.DartsLevels.Length - 1, index != 0 && index != config.DartsLevels.Length - 1, false);
                control.SetSlotNumber(index + 1);

                RewardInfo specialRewards = new RewardInfo();
                MainManager.Instance.FeaturesManager.ForeachActiveFeatures<ISpecialRewardable>(feature =>
                {
                    string eventId = ISpecialRewardable.FeaturesIds.Darts + (index + 1).ToString();
                    specialRewards = feature.GetRewards(eventId, true);
                });

                var rewardPackInfo = config.DartsLevels[index].PackRewardInfo;
                RewardInfo[] rewardInfos;

                if (rewardPackInfo.PackRewardViewId.IsNullOrEmpty())
                {
                    rewardInfos = specialRewards.Type.IsNullOrEmpty() ? rewardPackInfo.RewardInfos : new RewardInfo[] { specialRewards };
                }
                else
                {
                    rewardInfos = new RewardInfo[rewardPackInfo.RewardInfos.Length + (specialRewards.Type.IsNullOrEmpty() ? 1 : 0)];
                    for (int j = 0; j < rewardPackInfo.RewardInfos.Length; j++)
                    {
                        rewardInfos[j] = rewardPackInfo.RewardInfos[j];
                    }
                    if (!specialRewards.Type.IsNullOrEmpty())
                    {
                        rewardInfos[rewardPackInfo.RewardInfos.Length] = specialRewards;
                    }
                }

                if (index < currentStep)
                {
                    var needAnimate = currentStep - 1 < index;
                    control.SetCompleted(animatedSteps, needAnimate);
                    control.SetInteractableButton(false);

                    if (needAnimate)
                    {
                        animatedSteps++;
                    }
                }
                else if (index == currentStep)
                {
                    currentStepView = control;
                    control.SetActive(index, currProgress);

                    if (!string.IsNullOrEmpty(rewardPackInfo.PackRewardViewId))
                    {
                        var rewardItemControl = dartsCascadeWindow.CreateSpecialRewardItem(rewardPackInfo.PackRewardViewId);
                        //setup item

                        if (rewardInfos.Length > 1)
                        {
                            rewardItemControl.Interactable = false;
                            rewardItemControl.BlocksRaycasts = false;

                            control.SetInteractableButton(true);

                            var tmpIndex = index;
                            control.Clicked.AddListener(() =>
                            {
                                RunTooltipCoroutine(ShowRewardsRoutine(tmpIndex, rewardInfos));
                            });
                        }
                        else if (rewardInfos.Length == 1)
                        {
                            var reward = rewardInfos[0];
                            SetupRewardControl(rewardItemControl, reward, null);

                            rewardItemControl.Interactable = false;
                            rewardItemControl.BlocksRaycasts = false;

                            control.SetInteractableButton(false);
                        }
                    }
                    else
                    {
                        if (rewardInfos.Length == 1)
                        {
                            var reward = rewardInfos[0];
                            RewardItemControl rewardItemControl = control.CreateRewardItem(reward.Type, reward.Subtype, index);

                            SetupRewardControl(rewardItemControl, reward, null);

                            rewardItemControl.BlocksRaycasts = false;
                            rewardItemControl.Interactable = false;

                            if (reward.Type == Dip.Constants.RewardType.Multiplier)
                            {
                                control.SetInteractableButton(true);

                                var tmpIndex = index;
                                control.Clicked.AddListener(() =>
                                {
                                    var context = multiplierTooltipContextProvider.GetTooltipContextByReward(reward.Subtype);
                                    RunTooltipCoroutine(ShowMultiplierRoutine(tmpIndex, context));
                                });
                            }
                            else
                            {
                                control.SetInteractableButton(false);
                            }
                        }
                    }

                    // if (stepInfo.additionalReward.HasValue)
                    // {
                    //     var additionalReward = stepInfo.additionalReward.Value;
                    //     var rewardItemControl = control.CreateAdditionalRewardItem(additionalReward.Type, additionalReward.Subtype);
                    //     SetupRewardControl(rewardItemControl, additionalReward, null);
                    // }
                }
                else
                {
                    control.SetLocked();
                    control.SetInteractableButton(true);

                    if (!string.IsNullOrEmpty(rewardPackInfo.PackRewardViewId))
                    {
                        var rewardItemControl = dartsCascadeWindow.CreateSpecialRewardItem(rewardPackInfo.PackRewardViewId);
                        //setup item

                        if (rewardInfos.Length > 1)
                        {
                            rewardItemControl.Interactable = false;
                            rewardItemControl.BlocksRaycasts = false;

                            control.SetInteractableButton(true);

                            var tmpIndex = index;
                            control.Clicked.AddListener(() =>
                            {
                                RunTooltipCoroutine(ShowRewardsRoutine(tmpIndex, rewardInfos));
                            });
                        }
                        else if (rewardInfos.Length == 1)
                        {
                            var reward = rewardInfos[0];
                            SetupRewardControl(rewardItemControl, reward, null);

                            rewardItemControl.Interactable = false;
                            rewardItemControl.BlocksRaycasts = false;

                            var tmpIndex = index;
                            control.Clicked.AddListener(() =>
                            {
                                MainManager.Instance.SoundManager.Play("ui_meta_seasonpass_tap_locked");
                                dartsCascadeWindow.ShowListLockedTooltip(tmpIndex);
                            });
                        }
                    }
                    else
                    {
                        if (rewardInfos.Length == 1)
                        {
                            var reward = rewardInfos[0];

                            RewardItemControl rewardItemControl = control.CreateRewardItem(reward.Type, reward.Subtype, index);

                            SetupRewardControl(rewardItemControl, reward, null);

                            rewardItemControl.BlocksRaycasts = false;
                            rewardItemControl.Interactable = false;

                            if (reward.Type == Dip.Constants.RewardType.Multiplier)
                            {
                                control.SetInteractableButton(true);

                                var tmpIndex = index;
                                control.Clicked.AddListener(() =>
                                {
                                    var context = multiplierTooltipContextProvider.GetTooltipContextByReward(reward.Subtype);
                                    RunTooltipCoroutine(ShowMultiplierRoutine(tmpIndex, context));
                                });
                            }
                            else
                            {
                                var tmpIndex = index;
                                control.Clicked.AddListener(() => dartsCascadeWindow.ShowListLockedTooltip(tmpIndex));
                            }
                        }
                    }

                    // if (stepInfo.additionalReward.HasValue)
                    // {
                    //     var additionalReward = stepInfo.additionalReward.Value;
                    //     var rewardItemControl = control.CreateAdditionalRewardItem(additionalReward.Type, additionalReward.Subtype);
                    //     SetupRewardControl(rewardItemControl, additionalReward, null);
                    // }
                }
            }

            dartsCascadeWindow.ScrollToView(currentStepView);

            void ShowMultiplierTooltip(RewardInfo rewardInfo)
            {
                if (dartsCascadeWindow != null)
                {
                    var context = multiplierTooltipContextProvider.GetTooltipContextByReward(rewardInfo.Subtype);
                    dartsCascadeWindow.ShowInfoMultiplierTooltip(context);
                }
            }
        }

        private Coroutine tooltipCoroutine;
        private void RunTooltipCoroutine(IEnumerator coroutine)
        {
            if (tooltipCoroutine != null)
            {
                coroutineRunner.StopCoroutine(tooltipCoroutine);
            }

            tooltipCoroutine = coroutineRunner.StartCoroutine(coroutine);
        }

        private void SetupRewardControl(RewardItemControl control, RewardInfo rewardInfo, Action<RewardInfo> showMultiplierTooltip)
        {
            var countControl = control as RewardItemCountControl;
            if (countControl != null)
            {
                countControl.drawOneCount = true;
            }

            RewardsUtils.SetupRewardItemControlWithMultiplier(control, rewardInfo, showMultiplierTooltip);
        }

        private IEnumerator ShowRewardsRoutine(int tmpIndex, RewardInfo[] rewardInfos)
        {
            var tooltip = dartsCascadeWindow.ShowRewardListTooltip(tmpIndex);
            tooltip.ClearRewards();

            yield return new WaitForEndOfFrame();

            tooltip.SetItemsCapacity(rewardInfos.Length);

            foreach (var reward in rewardInfos)
            {
                var tooltipRewardItem = tooltip.CreateRewardItem(reward.Type, reward.Subtype);

                SetupRewardControl(tooltipRewardItem, reward, null);
            }

            dartsCascadeWindow.ShowRewardListTooltip(tmpIndex);
        }

        private IEnumerator ShowMultiplierRoutine(int index, MultiplyBonusTooltip.MultiplyBonusTooltipContext context)
        {
            dartsCascadeWindow.ShowListMultiplierTooltip(index, context);

            yield return new WaitForEndOfFrame();

            dartsCascadeWindow.ShowListMultiplierTooltip(index, context);
        }

        void IMatch3LevelCompleteHandler.CompleteMatch3(bool isWin, int levelDifficulty)
        {
            if (saveData.StateType == DartsState.Completed)
            {
                if (saveData.IsFinalRewardReceived)
                {
                    FinishCurrentEvent();
                }
                // else
                // {
                //     ShowLeaderboardWindow();
                // }
            }

            if (saveData.StateType == DartsState.PreStarted &&
                isWin)
            {
                saveData.StateType = DartsState.InProgress;

                isRewardsReceivingFlow = true;

                isInitWidgetInProgressAvailable = false;
            }

            if (saveData.StateType != DartsState.InProgress)
            {
                return;
            }

            UpdateVisuals();

            if (isWin)
            {
                isRewardsReceivingFlow = true;

                saveData.LastScore = levelDifficulty * multiplierManager.MultiplierFactor * config.DartsMultipliers[saveData.MultipliersProgress];
                saveData.LastMultipliersProgress = saveData.MultipliersProgress;
                saveData.MultipliersProgress = Mathf.Clamp(saveData.MultipliersProgress + 1, 0, config.DartsMultipliers.Length - 1);

                leaderboardsManager.SubmitScoreToLeaderboard(config.LeaderboardId, Storage.Get<CommonStorage>().UserId, saveData.LastScore);
            }
            else
            {
                saveData.LastScore = 0;
                saveData.LastMultipliersProgress = saveData.MultipliersProgress;
                saveData.MultipliersProgress = 0;

                UpdateVisuals();
            }

            saveData.PointsProgress += saveData.LastScore;

            leaderboardsManager.RefreshLeaderboard(config.LeaderboardId, new LeaderboardRefreshPayload() { IsWin = isWin, MultiplyScoreFactor = levelDifficulty * multiplierManager.MultiplierFactor * config.DartsMultipliers[saveData.MultipliersProgress] });

            DartsFeatureSaveController.MarkForSaving();

            if (saveData.MultipliersProgress !=
                saveData.LastMultipliersProgress)
            {
                SendMultiplierAnalyticsEvent();
            }
        }

        int IEndLevelAnimationPlayer.GetPriority()
        {
            return config.EndLevelAnimationOrder;
        }

        bool IEndLevelAnimationPlayer.IsInQueue()
        {
            return false;
        }

        float IEndLevelAnimationPlayer.AnimationDelay()
        {
            return IEndLevelAnimationPlayer.DefaultDelay;
        }

        int IPrioritized.GetPriority()
        {
            return saveData.StateType == DartsState.Completed ? config.ApplyRewardPriority : config.ShowPriority;
        }

        bool IWidgetable.IsNeedCreateWidget => saveData.StateType != DartsState.Rest || saveData.IsLocked;

        GameObject IWidgetable.WidgetPrefab => (GameObject)GetLoadedAsset(config.MetaWidgetConfig.Id);

        MetaWidgetAlignment IWidgetable.Alignment => config.MetaWidgetConfig.Alignment;

        int IWidgetable.SlotIndex => config.MetaWidgetConfig.SlotIndex;

        int IWidgetable.Priority => config.MetaWidgetConfig.Priority;

        public event Action<IWidgetable> WidgetRemoveRequesting;

        void IWidgetable.OnWidgetCreated(GameObject widget)
        {
            widgetController = widget.GetComponent<DartsWidgetController>();
            if ((IsAvailableByLevel && !needPlayUnlock) || CheatActivated)
            {
                isInitWidgetInProgress = true;
                UpdateWidget();
                isInitWidgetInProgress = false;
                isInitWidgetInProgressAvailable = false;
            }
            else
            {
                widgetController.LockWidget(true, config.StartEventTriggerData.Match3LevelIndex);
                widgetController.Clicked = null;
            }

            widgetController.OnDestroyed += WidgetControllerOnDestroyed;

            widgetController.Clicked = OnWidgetClicked;

            widgetMultiplierBarController.InitWidget(widgetController);
            widgetProgressBarController.InitWidget(widgetController);
        }

        private void OnWidgetClicked()
        {
            switch (saveData.StateType)
            {
                case DartsState.Rest:
                    if (IsCanBeLocked)
                    {
                        StartEvent();
                    }
                    break;
                case DartsState.Announce:
                case DartsState.PreStarted:
                    ShowStartWindow(false);
                    break;
                case DartsState.InProgress:
                    ShowMainWindow(null);
                    break;
                case DartsState.Completed:
                    ShowLeaderboardWindow(true);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void WidgetControllerOnDestroyed()
        {
            if (widgetController != null)
            {
                widgetController.Clicked = null;
                widgetController.OnDestroyed -= WidgetControllerOnDestroyed;

                widgetController = null;
            }
        }

        private void OnWidgetRemoveRequesting()
        {
            if (widgetController == null || WidgetRemoveRequesting == null)
            {
                return;
            }

            widgetController = null;
            WidgetRemoveRequesting?.Invoke(this);
        }

        private Sequence PlayUnlockAnimation()
        {
            if (!featuresScheduleController.IsTimeToStartFeature(this, out _, out _))
            {
                return null;
            }

            var sequence = DOTween.Sequence();
            sequence.AppendCallback(() =>
            {
                if (widgetController == null)
                {
                    return;
                }

                widgetController.LockWidget(false, config.UnlockTriggerData.Match3LevelIndex);
                widgetController.Clicked = null;
            });

            sequence.AppendInterval(UnlockInterval);
            sequence.AppendCallback(() => { });

            return sequence;
        }

        Sequence IEndLevelAnimationPlayer.PlayEndLevelAnimation(bool isWin, Action callback)
        {
            var sequence = DOTween.Sequence();

            if (saveData == null)
            {
                Debug.LogError("Storage is NULL!!!");
                callback?.Invoke();
                return sequence;
            }

            if (widgetController == null)
            {
                callback?.Invoke();
                return sequence;
            }

            if (saveData.StateType == DartsState.InProgress)
            {
                if (!isWin)
                {
                    sequence.InsertCallback(0.0f, () =>
                        {
                            widgetMultiplierBarController.PlayAnimation(() =>
                            {
                                callback?.Invoke();
                            });
                        });
                    return sequence;
                }

                isRewardsReceivingFlow = true;

                if (TryGetLoadedAsset(itemToShieldWidgetKey, out ItemToShieldWidget chipPrefab))
                {
                    var chipGo = Object.Instantiate(chipPrefab, widgetController.transform, false);
                    var startPositionPointer = config.MetaWidgetConfig.Alignment == MetaWidgetAlignment.Left ?
                        chipGo.RightToLeftStartPosition :
                        chipGo.LeftToRightStartPosition;

                    //chipGo.SetCount(5, true);

                    chipGo.SetCount(saveData.LastScore, multiplierManager.WasActiveInSession);

                    sequence.Join(chipGo.DisplayFlow(startPositionPointer,
                        multiplierManager.WasActiveInSession && saveData.LastScore != 1,
                        () =>
                        {
                            var list = leaderboardsManager.GetLeaderboardAroundPlayer(config.LeaderboardId, Storage.Get<CommonStorage>().UserId, 1);
                            if (list is { Count: > 0 })
                            {
                                widgetController.SetData(list[0].position);
                            }
                            widgetController.EnableFx(true);

                            PlayUpdateSequence();
                        }, value =>
                        {
                            widgetController.PlayMiniPunch(value);
                            soundManagerWrapper.Play("ui_meta_goldencup_hud_fly_in");
                        }));

                    sequence.SetId(WidgetScoreTweenId);
                }
                else
                {
                    callback?.Invoke();
                }
            }
            else
            {
                callback?.Invoke();
            }

            return sequence;

            void PlayUpdateSequence()
            {
                widgetProgressBarController.PlayAnimation(() =>
                {
                    widgetMultiplierBarController.PlayAnimation(() =>
                    {
                        widgetController.EnableFx(false);

                        callback?.Invoke();
                    });
                });
            }
        }

        Sprite IMultiplierBonusIconProvider.CollectionItemIcon
        {
            get
            {
                if (saveData.StateType == DartsState.InProgress &&
                    TryGetLoadedAsset(itemToShieldWidgetKey, out ItemToShieldWidget chip))
                {
                    return chip.WidgetSprite;
                }

                return null;
            }
        }

        private void RestoreEventFromState()
        {
            switch (saveData.StateType)
            {
                case DartsState.InProgress:
                    {
                        saveData.LastScore = 0;

                        leaderboardsManager.InitializeLeaderboard(config.LeaderboardId, saveData.StartTime,
                            saveData.EndTime);
                        AvatarsCacheManager.PreloadAvatars(leaderboardsManager.GetLeaderboard(config.LeaderboardId)
                            .Select(lpri => lpri.avatarId));

                        var currentTime = timeCheckManager.TimeChecker.CheckedDateUtc;
                        if (currentTime >= saveData.EndTime && saveData.StateType == DartsState.InProgress)
                        {
                            CompleteCurrentEvent();
                        }
                        else
                        {
                            isStartWindowShown = true;
                            DartsFeatureSaveController.MarkForSaving();
                        }

                        UpdateVisuals();
                        break;
                    }
                case DartsState.Completed:
                    {
                        leaderboardsManager.InitializeLeaderboard(config.LeaderboardId, saveData.StartTime,
                            saveData.EndTime, false);
                        AvatarsCacheManager.PreloadAvatars(leaderboardsManager.GetLeaderboard(config.LeaderboardId)
                            .Select(lpri => lpri.avatarId));
                        isStartWindowShown = true;
                        DartsFeatureSaveController.MarkForSaving();
                        if (saveData.IsFinalRewardReceived)
                        {
                            FinishCurrentEvent();
                        }
                        break;
                    }
                case DartsState.Rest:
                    leaderboardsManager.DisposeLeaderboard(config.LeaderboardId);
                    break;
                case DartsState.Announce:
                case DartsState.PreStarted:
                    {
                        FinishCurrentEvent();
                        leaderboardsManager.InitializeLeaderboard(config.LeaderboardId, saveData.StartTime,
                                saveData.EndTime);
                        AvatarsCacheManager.PreloadAvatars(leaderboardsManager.GetLeaderboard(config.LeaderboardId)
                            .Select(lpri => lpri.avatarId));
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (saveData.StateType == DartsState.InProgress ||
                saveData.StateType == DartsState.Completed)
            {
                int points = 0;
                int currentRewardProgress = saveData.LevelsRewardsReceived;
                int targetRewardProgress = 0;
                for (int i = 0; i < config.DartsLevels.Length; i++)
                {
                    points += config.DartsLevels[i].Points;
                    if (saveData.PointsProgress < points)
                    {
                        targetRewardProgress = i;
                        break;
                    }
                }
                if (currentRewardProgress < targetRewardProgress)
                {
                    RewardInfo specialRewards = new RewardInfo();
                    for (int i = currentRewardProgress; i < targetRewardProgress; i++)
                    {
                        MainManager.Instance.FeaturesManager.ForeachActiveFeatures<ISpecialRewardable>(feature =>
                        {
                            string eventId = ISpecialRewardable.FeaturesIds.Darts + (i + 1).ToString();
                            specialRewards = feature.GetRewards(eventId, true);
                            List<RewardInfo> rewards = feature.GenerateRewards(eventId);
                            if (!rewards.IsNullOrEmpty())
                            {
                                feature.ApplyRewards(rewards);
                            }
                        });
                        RewardInfo[] rewardInfos;
                        if (config.DartsLevels[i].PackRewardInfo.PackRewardViewId.IsNullOrEmpty())
                        {
                            rewardInfos = specialRewards.Type.IsNullOrEmpty() ? config.DartsLevels[i].PackRewardInfo.RewardInfos : new RewardInfo[] { specialRewards };
                        }
                        else
                        {
                            rewardInfos = new RewardInfo[config.DartsLevels[i].PackRewardInfo.RewardInfos.Length + (specialRewards.Type.IsNullOrEmpty() ? 1 : 0)];
                            for (int j = 0; j < config.DartsLevels[i].PackRewardInfo.RewardInfos.Length; j++)
                            {
                                rewardInfos[j] = config.DartsLevels[i].PackRewardInfo.RewardInfos[j];
                            }
                            if (!specialRewards.Type.IsNullOrEmpty())
                            {
                                rewardInfos[config.DartsLevels[i].PackRewardInfo.RewardInfos.Length] = specialRewards;
                            }
                        }
                        rewardEnroller.ApplyRewards(rewardInfos, "darts_stage_completed", AnalyticsCategoryKeys.DartsEvent, false);
                    }
                    saveData.LevelsRewardsReceived = targetRewardProgress;
                    saveData.LastPointsProgress = saveData.PointsProgress;
                }
            }
        }

        private void StartEvent()
        {
            CustomDebug.Log("DartsFeatureController StartEvent");

            if (saveData.StateType == DartsState.Completed)
            {
                if (saveData.IsFinalRewardReceived)
                {
                    FinishCurrentEvent();
                    return;
                }
            }

            if (saveData.StateType == DartsState.InProgress)
            {
                return;
            }

            if (!featuresScheduleController.IsTimeToStartFeature(this, out var eventStartTime,
                                                                 out var eventDurationInMinutes))
            {
                Debug.LogError("Darts trying to start but there is no time in calendar schedule");
            }

            saveData.StartTime = eventStartTime;
            saveData.EndTime = eventStartTime + TimeSpan.FromMinutes(eventDurationInMinutes);

            leaderboardsManager.InitializeLeaderboard(config.LeaderboardId, eventStartTime, saveData.EndTime);

            leaderboardsManager.SetLeaderboardDifficulty(config.LeaderboardId, balanceManager.LevelDifficulty, 0);
            leaderboardsManager.SubmitScoreToLeaderboard(config.LeaderboardId, Storage.Get<CommonStorage>().UserId, saveData.LastScore);
            leaderboardsManager.RefreshLeaderboard(config.LeaderboardId, new LeaderboardRefreshPayload() { IsWin = true });

            AvatarsCacheManager.PreloadAvatars(leaderboardsManager.GetLeaderboard(config.LeaderboardId).Select(lpri => lpri.avatarId));

            saveData.MultipliersProgress = 0;
            saveData.LastMultipliersProgress = 0;
            saveData.WatchedMultipliers = null;
            saveData.PointsProgress = 0;
            saveData.LastPointsProgress = 0;
            saveData.LevelsRewardsReceived = 0;
            saveData.LastScore = 0;
            saveData.StateType = DartsState.Announce;
            saveData.IsFinalRewardReceived = false;
            saveData.PreviousPosition = config.DartsLevels.Length - 1;
            saveData.IsStartWindowsShown = false;

            if (widgetController != null)
            {
                widgetMultiplierBarController.InitWidget(widgetController);
                widgetProgressBarController.InitWidget(widgetController);
            }

            isStartWindowShown = false;
            isCompletedWindowShown = false;

            DartsFeatureSaveController.MarkForSaving();
        }

        private void CompleteCurrentEvent()
        {
            CustomDebug.Log("DartsFeatureController CompleteCurrentEvent");

            if (saveData.StateType == DartsState.Completed)
            {
                return;
            }

            if (leaderboardsManager.GetLeaderboardInstance(config.LeaderboardId) is LocalLeaderboard localLeaderboard)
            {
                localLeaderboard.StopSimulation();
            }

            saveData.StateType = DartsState.Completed;

            UpdateVisuals();
            DartsFeatureSaveController.MarkForSaving();
        }

        private void FinishCurrentEvent()
        {
            CustomDebug.Log("DartsFeatureController FinishCurrentEvent");
            if (saveData.StateType != DartsState.Completed)
            {
                return;
            }

            leaderboardsManager.DisposeLeaderboard(config.LeaderboardId);
            saveData.StateType = DartsState.Rest;

            OnWidgetRemoveRequesting();
            SendFinishAnalyticsEvent();

            isStartWindowShown = false;
            isCompletedWindowShown = false;

            saveData.LastScore = 0;
            saveData.DartsId += 1;
            saveData.MultipliersProgress = 0;
            saveData.LastMultipliersProgress = 0;
            saveData.WatchedMultipliers = null;
            saveData.PointsProgress = 0;
            saveData.LastPointsProgress = 0;
            saveData.LevelsRewardsReceived = 0;
            saveData.IsFinalRewardReceived = false;
            saveData.IsStartWindowsShown = false;

            featuresScheduleController.FinishFeature(this);

            DartsFeatureSaveController.MarkForSaving();
        }

        private void UpdateVisuals()
        {
            UpdateWidget();

            if (leaderboardWindow != null)
            {
                leaderboardWindow.SetTimer(TimeLeft);
            }

            if (startWindow != null)
            {
                startWindow.SetTimer(TimeLeft);
            }

            if (mainWindow != null)
            {
                mainWindow.SetTimer(TimeLeft);
            }
        }

        private bool IsEventActiveAndNecessaryShowLabel()
        {
            var isNeedShowLabel = false;
            MainManager.Instance.FeaturesManager.ForeachActiveFeatures<INeedShowWindowLabel>(feature =>
            {
                isNeedShowLabel = feature.IsNeedShowLabel;
            });

            return isNeedShowLabel;
        }

        private void UpdateWidget()
        {
            CustomDebug.Log("DartsFeatureController UpdateWidget");

            if (widgetController != null)
            {
                var player = leaderboardsManager.GetLeaderboardAroundPlayer(config.LeaderboardId, Storage.Get<CommonStorage>().UserId, 1);
                if (player == null || player.Count < 1)
                {
                    Debug.LogError($"Invalid leaderboard {config.LeaderboardId}");
                    return;
                }

                widgetController.SetTimer(TimeLeft, saveData.StateType == DartsState.Completed && !saveData.IsFinalRewardReceived);

                switch (saveData.StateType)
                {
                    case DartsState.Rest:
                    case DartsState.Announce:
                    case DartsState.PreStarted:
                        PrepareUiWithNotStartedState();
                        break;
                    case DartsState.InProgress:
                        PrepareUiWithInProgressState();
                        break;
                    case DartsState.Completed:
                        PrepareUiWithCompletedState();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                //widgetController.Clicked = OnWidgetClicked;
            }
        }

        private void PrepareUiWithNotStartedState()
        {
            if (widgetController == null)
            {
                return;
            }

            widgetController.SetData(-1);
            widgetController.SetTimer(TimeLeft, false);

            widgetController.SetNotify(!saveData.IsStartWindowsShown);

            widgetController.Clicked = () =>
            {
                ShowStartWindow(false);
            };
        }

        private void PrepareUiWithInProgressState()
        {
            if (widgetController != null)
            {
                widgetController.SetTimer(TimeLeft, false);

                if ((!isRewardsReceivingFlow &&
                    saveData.LastScore == 0) ||
                    (isInitWidgetInProgressAvailable &&
                     isInitWidgetInProgress))
                {
                    int position;
                    if (internetManager.IsInternetAvailable)
                    {
                        var list = leaderboardsManager.GetLeaderboardAroundPlayer(config.LeaderboardId, Storage.Get<CommonStorage>().UserId, 1);
                        if (list is { Count: > 0 })
                        {
                            position = list[0].position;
                        }
                        else
                        {
                            position = -1;
                        }
                    }
                    else
                    {
                        position = -1;
                    }
                    widgetController.SetData(position);
                }

                widgetController.Clicked = () =>
                {
                    if (!internetManager.IsInternetAvailable)
                    {
                        UiManager.Instance.HideAllWindows();
                        ApplicationContext.Instance.LocalUiManager.ShowNoInternetText();
                        return;
                    }

                    ShowMainWindow(null);
                    widgetController.SetNotify(false);
                };
            }
        }

        private void PrepareUiWithCompletedState()
        {
            UiManager uiManager = UiManager.Instance;
            if (widgetController != null)
            {
                int position;
                if (internetManager.IsInternetAvailable)
                {
                    var list = leaderboardsManager.GetLeaderboardAroundPlayer(config.LeaderboardId, Storage.Get<CommonStorage>().UserId, 1);
                    if (list is { Count: > 0 })
                    {
                        position = list[0].position;
                    }
                    else
                    {
                        position = -1;
                    }
                }
                else
                {
                    position = -1;
                }

                if (position > config.CompleteEventRewardInfos.Length)
                {
                    saveData.IsFinalRewardReceived = true;
                    DartsFeatureSaveController.MarkForSaving();
                }

                widgetController.SetData(position);

                widgetController.SetTimer(TimeSpan.Zero,
                    !saveData.IsFinalRewardReceived &&
                    internetManager.IsInternetAvailable);
                widgetController.SetNotify(!saveData.IsFinalRewardReceived);

                widgetController.Clicked = () =>
                {
                    if (!internetManager.IsInternetAvailable)
                    {
                        UiManager.Instance.HideAllWindows();
                        ApplicationContext.Instance.LocalUiManager.ShowNoInternetText();
                        onFinish?.Invoke(false, true);
                        return;
                    }

                    ShowLeaderboardWindow(true);
                    widgetController.SetNotify(false);
                };
            }
        }

        private void OnInternetStateChanged(InternetState internetState)
        {
            if (saveData.StateType == DartsState.Rest)
            {
                return;
            }

            if (leaderboardsManager.GetLeaderboardInstance(config.LeaderboardId) is LocalLeaderboard localLeaderboard)
            {
                localLeaderboard.InternetManager_InternetStatusChanged(internetState);
            }

            UpdateWidget();
        }

        public void DeepLinkClick()
        {
            if (widgetController != null)
            {
                widgetController.Clicked?.Invoke();
            }
        }

        LoseProgressPanel.LoseItemInfo ILoseProgressProvider.LoseItemInfo => new(
            (GameObject)GetLoadedAsset(DartsLoseRewardKey), (obj) =>
            {
                var rewardItem = obj.GetComponent<DartsLoseReward>();
                rewardItem.SetReward($"x{config.DartsMultipliers[saveData.MultipliersProgress]}");
            }, config.TitleLocalizationKey, useCustomColor: true,
            textColor: config.LoseRewardItemTextColor);

        int ILoseProgressProvider.Priority => config.LoseInfoPriority;
        bool ILoseProgressProvider.HasValue => saveData.StateType == DartsState.InProgress &&
            saveData.MultipliersProgress != config.DartsMultipliers[0];

        #region Analytics

        private void SendStartAnalyticsEvent()
        {
            int position;
            if (internetManager.IsInternetAvailable)
            {
                var leaderboard = leaderboardsManager.GetLeaderboard(config.LeaderboardId);
                var userId = Storage.Get<CommonStorage>().UserId;
                var resultPosition = leaderboard.FindIndex(x => x.userId == userId) + 1;
                position = resultPosition;
                
                // var list = leaderboardsManager.GetLeaderboardAroundPlayer(config.LeaderboardId, Storage.Get<CommonStorage>().UserId, 1);
                // if (list is { Count: > 0 })
                // {
                //     position = list[0].position;
                // }
                // else
                // {
                //     position = -1;
                // }
            }
            else
            {
                position = -1;
            }

            Dictionary<string, object> analyticsData = new Dictionary<string, object>();

            analyticsData["darts_id"] = saveData.DartsId;
            analyticsData["balance"] = saveData.PointsProgress;
            analyticsData["rank"] = position;
            analyticsData["multiplier"] = config.DartsMultipliers[saveData.MultipliersProgress];
            analyticsData[AnalyticsKeys.Parameters.LevelNumber] = MetaMatch3SavedData.GetActiveBranchLevelNumber();
            analyticsData[AnalyticsKeys.Parameters.LevelVersion] = MainManager.Instance.Match3LevelManager.Version;
            analyticsData[AnalyticsKeys.Parameters.LevelUid] = AnalyticsParameters.GetMatch3LevelUid();
            analyticsData[AnalyticsKeys.Parameters.QuestID] = Storage.Get<CommonStorage>().AnalyticsQuestId;

            DipAnalytics.SendEvent("darts_event_start", analyticsData);
        }

        private void SendFinishAnalyticsEvent()
        {
            int position;
            if (internetManager.IsInternetAvailable)
            {
                var leaderboard = leaderboardsManager.GetLeaderboard(config.LeaderboardId);
                var userId = Storage.Get<CommonStorage>().UserId;
                var resultPosition = leaderboard.FindIndex(x => x.userId == userId) + 1;
                position = resultPosition;
                
                // var list = leaderboardsManager.GetLeaderboardAroundPlayer(config.LeaderboardId, Storage.Get<CommonStorage>().UserId, 1);
                // if (list is { Count: > 0 })
                // {
                //     position = list[0].position;
                // }
                // else
                // {
                //     position = -1;
                // }
            }
            else
            {
                position = -1;
            }

            Dictionary<string, object> analyticsData = new Dictionary<string, object>();

            analyticsData["darts_id"] = saveData.DartsId;
            analyticsData["balance"] = saveData.PointsProgress;
            analyticsData["rank"] = position;
            analyticsData["multiplier"] = config.DartsMultipliers[saveData.MultipliersProgress];
            analyticsData[AnalyticsKeys.Parameters.LevelNumber] = MetaMatch3SavedData.GetActiveBranchLevelNumber();
            analyticsData[AnalyticsKeys.Parameters.LevelVersion] = MainManager.Instance.Match3LevelManager.Version;
            analyticsData[AnalyticsKeys.Parameters.LevelUid] = AnalyticsParameters.GetMatch3LevelUid();
            analyticsData[AnalyticsKeys.Parameters.QuestID] = Storage.Get<CommonStorage>().AnalyticsQuestId;

            DipAnalytics.SendEvent("darts_event_finish", analyticsData);
        }

        private void SendMultiplierAnalyticsEvent()
        {
            int position;
            if (internetManager.IsInternetAvailable)
            {
                var leaderboard = leaderboardsManager.GetLeaderboard(config.LeaderboardId);
                var userId = Storage.Get<CommonStorage>().UserId;
                var resultPosition = leaderboard.FindIndex(x => x.userId == userId) + 1;
                position = resultPosition;
                
                // var list = leaderboardsManager.GetLeaderboardAroundPlayer(config.LeaderboardId, Storage.Get<CommonStorage>().UserId, 1);
                // if (list is { Count: > 0 })
                // {
                //     position = list[0].position;
                // }
                // else
                // {
                //     position = -1;
                // }
            }
            else
            {
                position = -1;
            }

            Dictionary<string, object> analyticsData = new Dictionary<string, object>();

            analyticsData["darts_id"] = saveData.DartsId;
            analyticsData["balance"] = saveData.PointsProgress;
            analyticsData["rank"] = position;
            analyticsData["multiplier"] = config.DartsMultipliers[saveData.MultipliersProgress];
            analyticsData[AnalyticsKeys.Parameters.LevelNumber] = MetaMatch3SavedData.GetActiveBranchLevelNumber();
            analyticsData[AnalyticsKeys.Parameters.LevelVersion] = MainManager.Instance.Match3LevelManager.Version;
            analyticsData[AnalyticsKeys.Parameters.LevelUid] = AnalyticsParameters.GetMatch3LevelUid();
            analyticsData[AnalyticsKeys.Parameters.QuestID] = Storage.Get<CommonStorage>().AnalyticsQuestId;

            DipAnalytics.SendEvent("dart_event_stage", analyticsData);
        }

        #endregion


        #region PushNotifications

        void IPushNotificationRegister.RegisterPushNotifications()
        {

        }

        void IPushNotificationRegister.CancelPushNotifications()
        {

        }

        #endregion


        #region Cheats

        public void SetScoreDarts(int score)
        {
            if (EventState == DartsState.InProgress)
            {
                saveData.PointsProgress = score;
                saveData.LastScore = 0;
                var leaderboard = leaderboardsManager.GetLeaderboardInstance(config.LeaderboardId);
                leaderboard?.SetScore(Storage.Get<CommonStorage>().UserId, score);
                DartsFeatureSaveController.MarkForSaving();
                UpdateVisuals();
            }
        }

        public void CompleteDarts()
        {
            if (EventState == DartsState.InProgress)
            {
                UiManager.Instance.HideAllWindows();
                UiManager.Instance.HideAllPopups();
                CompleteCurrentEvent();
            }
        }

        public void ResetDarts()
        {
            if (EventState != DartsState.Rest)
            {
                saveData.StateType = DartsState.Rest;
                saveData.LastScore = 0;
                saveData.IsStartWindowsShown = false;

                var leaderboard = leaderboardsManager.GetLeaderboardInstance(config.LeaderboardId);
                leaderboard?.SetScore(Storage.Get<CommonStorage>().UserId, 0);
                UiManager.Instance.HideAllWindows();
                UiManager.Instance.HideAllPopups();
                StartEvent();
                UpdateVisuals();
            }
        }

        #endregion
    }
}
