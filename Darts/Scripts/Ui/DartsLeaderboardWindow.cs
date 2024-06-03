using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Dip.MetaGame.Rewards;
using Dip.Rewards;
using Dip.Ui;
using Dip.Ui.Rewards;
using Dip.Ui.Tooltips;
using Spine;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

namespace Dip.Features.Darts.Ui
{
    public class DartsLeaderboardWindow : BaseFrame
    {
        public struct DartsPlayerData
        {
            public string name;
            public string avatarId;
            public string avatarFrame;
            public string playerNameColor;
            public int place;
            public int cupPoints;
            public bool isPlayer;
            public bool isShowingChest;
            public bool? isShowingJoker;
            public int rewardsCardsCount;
            public Action OnChestPressed;
            public Action OnPlayerPressed;
        }

        [SerializeField] private float AnimationSpeed = 0.7f;

        [Header("Main parameters")]
        [SerializeField] private RectTransform animatedGameObject;
        [SerializeField] private Button fadeButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button claimButton;
        [SerializeField] private TextMeshProUGUI claimText;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private int bottomPaddingFull = 60;
        [SerializeField] private int bottomPaddingComplete = 480;

        [Header("Top panel")]
        [SerializeField] private GameObject claimPanel;

        // [SerializeField] private GameObject ratingState;
        [SerializeField] private GameObject rewardsState;

        [SerializeField] private GameObject resultsPanel;
        [SerializeField] private GameObject normalChest;
        [SerializeField] private GameObject firstPlaceChest;
        [SerializeField] private GameObject secondPlaceChest;
        [SerializeField] private GameObject thirdPlaceChest;
        [SerializeField] private GameObject noChestPlace;
        [SerializeField] private Button chestInfoButton;
        [SerializeField] private TextMeshProUGUI resultPlace;
        // [SerializeField] private SkeletonGraphic rewardsAnimation;

        private const string spineIdleAnimation = "static";

        [Header("Widgets settings")]
        [SerializeField] private PlayerDartsView constantTopPlayerWidget;
        [SerializeField] private PlayerDartsView constantBottomPlayerWidget;
        [SerializeField] private PlayerDartsViewContainer widgetsContainer;


        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private AnimationCurve widgetsShowCurve;
        [SerializeField] private AnimationCurve widgetsHideCurve;
        [SerializeField] private float widgetShowDuration;
        [SerializeField] private float widgetHideDuration;
        [SerializeField] private float widgetShowDelay;
        [SerializeField] private int widgetsToAnimate;

        [Header("Tooltip")]
        [SerializeField] private RewardListTooltip rewardListTooltip;
        [SerializeField] private RectTransform tooltipViewport;
        [SerializeField] private Image tooltipForeground;

        [Header("Vibrations")]
        [SerializeField] private VibrationSequenceConfig openVibrationSequence;

        [SerializeField] private Transform prizeContainer;
        [SerializeField] private RewardViewConfig rewardViewConfig;

        private readonly TimeStringBuilder timeStringBuilder = new();

        private PlayerDartsView localPlayerWidgetReference;

        private bool isCompletedState;
        private Sequence appearAnimation;
        private Coroutine rewardCoroutine;

        public Action OnInfoPressed;
        public Action OnClaimButtonPressed;
        public Action OnChestInfoButtonPressed;
        public Action OnClosePressed;

        private List<Tween> showTweens = new();
        private GameObject resultBox;

        private RewardItemControlFactory rewardItemControlFactory;
        private RewardItemControl currentRewardItemControl;

        public RewardListTooltip RewardListTooltip => rewardListTooltip;

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

        public void SetTimer(TimeSpan timeLeft)
        {
            if (timeStringBuilder.TryGetTimeStringFromTimeSpan(timeLeft, out var timeString))
            {
                SetTimer(timeString);
            }
        }

        public void SetData(IEnumerable<DartsPlayerData> playerDatas, DartsFeatureConfig.PackRewardInfo[] packRewardInfos, RewardInfo rewardInfo, bool isCompleted = false, bool isRewardReceived = false)
        {
            var playerData = playerDatas.First(x => x.isPlayer);
            var currentPlace = playerData.place;

            isCompletedState = isCompleted;
            // closeButton.gameObject.SetActiveChecked(!isCompleted);

            // ratingState.SetActiveChecked(!isCompleted);
            rewardsState.SetActiveChecked(isCompleted);

            if (isCompleted)
            {
                widgetsContainer.GetComponent<VerticalLayoutGroup>().padding.bottom = bottomPaddingComplete;
                timerText.text = LocalizationManager.GetLocalizedText("common.timer.finished");

                switch (currentPlace)
                {
                    case 1:
                        normalChest.SetActiveChecked(false);
                        firstPlaceChest.SetActiveChecked(true);
                        secondPlaceChest.SetActiveChecked(false);
                        thirdPlaceChest.SetActiveChecked(false);
                        noChestPlace.SetActiveChecked(false);
                        resultBox = firstPlaceChest;
                        break;
                    case 2:
                        normalChest.SetActiveChecked(false);
                        firstPlaceChest.SetActiveChecked(false);
                        secondPlaceChest.SetActiveChecked(true);
                        thirdPlaceChest.SetActiveChecked(false);
                        noChestPlace.SetActiveChecked(false);
                        resultBox = secondPlaceChest;
                        break;
                    case 3:
                        normalChest.SetActiveChecked(false);
                        firstPlaceChest.SetActiveChecked(false);
                        secondPlaceChest.SetActiveChecked(false);
                        thirdPlaceChest.SetActiveChecked(true);
                        noChestPlace.SetActiveChecked(false);
                        resultBox = thirdPlaceChest;
                        break;
                    case 4:
                    case 5:
                    case 6:
                    case 7:
                    case 8:
                    case 9:
                    case 10:
                        RewardItemControl rewardItemControl = CreateRewardItem(rewardInfo.Type, rewardInfo.Subtype);
                        if (rewardItemControl != null)
                        {
                            SetupRewardControl(rewardItemControl, rewardInfo, null);
                        }
                        normalChest.SetActiveChecked(false);
                        firstPlaceChest.SetActiveChecked(false);
                        secondPlaceChest.SetActiveChecked(false);
                        thirdPlaceChest.SetActiveChecked(false);
                        noChestPlace.SetActiveChecked(false);
                        resultBox = normalChest;
                        break;
                    default:
                        normalChest.SetActiveChecked(false);
                        firstPlaceChest.SetActiveChecked(false);
                        secondPlaceChest.SetActiveChecked(false);
                        thirdPlaceChest.SetActiveChecked(false);
                        noChestPlace.SetActiveChecked(true);
                        resultBox = noChestPlace;
                        break;
                }

                resultPlace.text = "#" + currentPlace;

                if (currentPlace <= 10)
                {
                    claimText.SetText(LocalizationManager.GetLocalizedText("buyboosterswindow.claim"));
                }
                else
                {
                    claimText.SetText(LocalizationManager.GetLocalizedText("common.exit"));
                }

                resultsPanel.SetActiveChecked(true);
            }
            else
            {
                widgetsContainer.GetComponent<VerticalLayoutGroup>().padding.bottom = bottomPaddingFull;
                resultsPanel.SetActiveChecked(false);
            }

            claimPanel.SetActiveChecked(!isRewardReceived);

            ClearData();

            CreatePlayerWidget(playerDatas, packRewardInfos, !isCompleted);
            constantTopPlayerWidget.SetData(playerData.name,
                                            playerData.place,
                                            playerData.cupPoints,
                                            ScrollToPlayer,
                                            ScrollToPlayer,
                                            playerData.rewardsCardsCount,
                                            new RewardInfo(),
                                            true,
                                            playerData.isShowingChest);
            constantBottomPlayerWidget.SetData(playerData.name,
                                                playerData.place,
                                                playerData.cupPoints,
                                                ScrollToPlayer,
                                                ScrollToPlayer,
                                                playerData.rewardsCardsCount,
                                                new RewardInfo(),
                                                true,
                                                playerData.isShowingChest);

            constantTopPlayerWidget.SetAvatarAndNameStyle(playerData);
            constantBottomPlayerWidget.SetAvatarAndNameStyle(playerData);
        }


        public Transform GetMainChestTransform()
        {
            return resultBox.transform;
        }


        public Transform GetPlayerWidgetChestTransform(int position)
        {
            return widgetsContainer.PlayerViews[position - 1].ChestTransform;
        }


        public void ShowRewardListTooltip(Transform targetTransform)
        {
            if (rewardListTooltip)
            {
                if (rewardCoroutine != null)
                {
                    StopCoroutine(rewardCoroutine);
                }
                rewardCoroutine = StartCoroutine(ClearAndShowRewardsDelayed(targetTransform));
            }
        }


        private IEnumerator ClearAndShowRewardsDelayed(Transform relativeTarget)
        {
            yield return new WaitForEndOfFrame();

            var targetWorldPosition = relativeTarget.position;
            var align = rewardListTooltip.DetectVerticalAlign(targetWorldPosition, tooltipViewport);
            RotateForeground(align, tooltipForeground);
            rewardListTooltip.ShowTooltip(true, false, relativeTarget, preferredAlign: align);
        }

        private void RotateForeground(Tooltip.TooltipAlign align, Image foreground)
        {
            if (foreground == null)
            {
                return;
            }

            switch (align)
            {
                case Tooltip.TooltipAlign.Down:
                    foreground.transform.rotation = Quaternion.Euler(0, 0f, 0f);
                    break;
                case Tooltip.TooltipAlign.Up:
                    foreground.transform.rotation = Quaternion.Euler(0, 0f, 180f);
                    break;
                case Tooltip.TooltipAlign.Left:
                    break;
                case Tooltip.TooltipAlign.Right:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(align), align, null);
            }
        }


        public void ScrollToPlayer()
        {
            Canvas.ForceUpdateCanvases();
            var index = widgetsContainer.PlayerViews.FindIndex(x => x.Equals(localPlayerWidgetReference));
            scrollRect.DOVerticalNormalizedPos(1.0f - (float)index / widgetsContainer.PlayerViews.Count, 0.5f);
        }


        protected override void OnInitialize()
        {
            base.OnInitialize();

            claimButton.interactable = true;
            rewardListTooltip.ShowTooltip(false, true);
        }


        protected override void OnShow()
        {
            base.OnShow();

            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 1.0f;
            // SetSpineAnimation(rewardsAnimation, spineIdleAnimation, startOffset: 1.2f);
            PlayAnimation();
            claimButton.interactable = true;

            VibrationSequence vibrationSequence = new VibrationSequence(openVibrationSequence);
            vibrationSequence.StartVibrationSequence();
        }

        protected override void OnHide()
        {
            for (var i = 0; i < showTweens.Count; i++)
            {
                showTweens[i].Kill();
            }
            showTweens.Clear();

            appearAnimation?.Kill();

            base.OnHide();

        }


        private void OnEnable()
        {
            timeStringBuilder.Localize();
            I2.Loc.LocalizationManager.OnLocalizeEvent += LocalizationManager_OnLocalizeEvent;

            closeButton.onClick.AddListener(CloseWindow);
            //fadeButton.onClick.AddListener(CloseWindow);
            claimButton.onClick.AddListener(ClaimPrize);
            chestInfoButton.onClick.AddListener(ShowChestTooltip);
            scrollRect.onValueChanged.AddListener(UpdateConstantPlayerWidget);
        }

        private void OnDisable()
        {
            I2.Loc.LocalizationManager.OnLocalizeEvent -= LocalizationManager_OnLocalizeEvent;

            closeButton.onClick.RemoveListener(CloseWindow);
            //fadeButton.onClick.RemoveListener(CloseWindow);
            claimButton.onClick.RemoveListener(ClaimPrize);
            chestInfoButton.onClick.RemoveListener(ShowChestTooltip);
            scrollRect.onValueChanged.RemoveListener(UpdateConstantPlayerWidget);
        }


        private void PlayAnimation()
        {
            animatedGameObject.localScale = Vector3.one;

            appearAnimation?.Kill();
            appearAnimation = DOTween.Sequence();
            appearAnimation.Append(animatedGameObject.DOScale(Vector3.one * 1.01f, 0.05f * AnimationSpeed).SetEase(Ease.InSine));
            appearAnimation.Insert(0.05f * AnimationSpeed, animatedGameObject.DOScale(Vector3.one * 0.99f, 0.15f * AnimationSpeed).SetEase(Ease.OutCirc));
            appearAnimation.Insert(0.2f * AnimationSpeed, animatedGameObject.DOScale(Vector3.one, 0.15f * AnimationSpeed).SetEase(Ease.OutQuad));
        }

        private TrackEntry SetSpineAnimation(SkeletonGraphic skeletonAnimation, string animationName, int trackIndex = 0, bool loop = false, float startOffset = 0f)
        {
            if (skeletonAnimation == null || skeletonAnimation.AnimationState == null)
            {
                return null;
            }

            var trackEntry = skeletonAnimation.AnimationState.SetAnimation(trackIndex, animationName, loop);
            skeletonAnimation.Update(startOffset);
            skeletonAnimation.LateUpdate();

            return trackEntry;
        }


        private void ClaimPrize()
        {
            claimButton.interactable = false;
            //uiManager.HideActiveFrame(this);
            OnClaimButtonPressed?.Invoke();
        }

        private void ShowChestTooltip() => OnChestInfoButtonPressed?.Invoke();

        private void SetTimer(string time)
        {
            if (timerText)
            {
                timerText.SetText(time);
            }
        }


        private void LocalizationManager_OnLocalizeEvent()
        {
            timeStringBuilder.Localize();
        }


        private void ClearData()
        {
            widgetsContainer.ClearData();
        }

        private void CreatePlayerWidget(IEnumerable<DartsPlayerData> playerDatas, DartsFeatureConfig.PackRewardInfo[] packRewardInfos, bool isNeedAppearAnimation)
        {
            var index = 0;

            showTweens.Clear();
            showTweens = new List<Tween>();

            foreach (DartsPlayerData playerData in playerDatas)
            {
                RewardInfo rewardInfo = (packRewardInfos.Length >= playerData.place) ? packRewardInfos[playerData.place - 1].RewardInfos[0] : new RewardInfo();
                var widget = widgetsContainer.AddView(playerData, rewardInfo);

                if (index < widgetsToAnimate)
                {
                    var tween = widget.Show(isNeedAppearAnimation ? widgetShowDuration : 0.0f, widgetsShowCurve)
                        .SetDelay(isNeedAppearAnimation ? widgetShowDelay * index : 0.0f);
                    tween.Play();

                    showTweens.Add(tween);

                    index++;
                }

                if (playerData.isPlayer)
                {
                    localPlayerWidgetReference = widget;
                }
            }
        }


        private void CloseWindow()
        {
            uiManager.HideActiveFrame(this);
            OnClosePressed?.Invoke();
        }

        private void UpdateConstantPlayerWidget(Vector2 position)
        {
            if (localPlayerWidgetReference == null || constantTopPlayerWidget == null || constantBottomPlayerWidget == null) return;

            if (constantBottomPlayerWidget.transform.position.y > localPlayerWidgetReference.transform.position.y &&
                !isCompletedState)
            {
                if (!constantBottomPlayerWidget.gameObject.activeInHierarchy)
                {
                    constantBottomPlayerWidget.gameObject.SetActive(true);
                }
            }
            else
            {
                if (constantBottomPlayerWidget.gameObject.activeInHierarchy)
                {
                    constantBottomPlayerWidget.gameObject.SetActive(false);
                }
            }
            if (constantTopPlayerWidget.transform.position.y < localPlayerWidgetReference.transform.position.y &&
                !isCompletedState &&
                false)
            {
                if (!constantTopPlayerWidget.gameObject.activeInHierarchy)
                {
                    constantTopPlayerWidget.gameObject.SetActive(true);
                }
            }
            else
            {
                if (constantTopPlayerWidget.gameObject.activeInHierarchy)
                {
                    constantTopPlayerWidget.gameObject.SetActive(false);
                }
            }
        }

        public RewardItemControl CreateRewardItem(string type, string subtype)
        {
            DestroyCurrentRewardItem();
            UpdateRewardFactory();
            var control = rewardItemControlFactory.CreateRewardItem(type, subtype, prizeContainer.transform);

            currentRewardItemControl = control;
            return control;
        }

        public RewardItemControl CreateSpecialRewardItem(string specialView)
        {
            DestroyCurrentRewardItem();
            UpdateRewardFactory();
            var control = rewardItemControlFactory.CreateSpecialRewardItem(specialView, prizeContainer.transform);

            currentRewardItemControl = control;
            return control;
        }

        public void SetupRewardControl(RewardItemControl control, RewardInfo rewardInfo, Action<RewardInfo> showMultiplierTooltip)
        {
            var countControl = control as RewardItemCountControl;
            if (countControl != null)
            {
                countControl.drawOneCount = true;
            }

            RewardsUtils.SetupRewardItemControlWithMultiplier(control, rewardInfo, showMultiplierTooltip);
        }

        public void DestroyCurrentRewardItem()
        {
            if (currentRewardItemControl)
            {
                Destroy(currentRewardItemControl.gameObject);
                currentRewardItemControl = null;
            }
        }

        private void UpdateRewardFactory()
        {
            if (rewardItemControlFactory == null)
            {
                rewardItemControlFactory = new RewardItemControlFactory(rewardViewConfig);
            }
        }


        private void InfoButton_OnClick() => OnInfoPressed?.Invoke();
    }
}
