using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using Dip.Features.ChooseAvatar;
using Dip.MetaGame.Rewards;
using Dip.Rewards;
using Dip.SoundManagerSystem;
using Dip.Ui;
using Dip.Ui.Rewards;
using Dip.Ui.Tooltips;
using MoreMountains.NiceVibrations;
using Spine;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

namespace Dip.Features.Darts.Ui
{
    public class DartsMainWindow : BaseFrame
    {
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
        }

        [SerializeField] private float AnimationSpeed = 0.7f;

        [Header("Main parameters")]
        [SerializeField] private RectTransform animatedGameObject;
        [SerializeField] private Button fadeButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button infoButton;
        [SerializeField] private Button progressBarButton;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private int bottomPaddingFull = 60;
        [SerializeField] private int bottomPaddingComplete = 480;

        [Space(5)]
        [SerializeField] private GameObject movableArrow;
        [SerializeField] private GameObject[] progressArrowsPositions;
        [SerializeField] private TextMeshProUGUI[] progressArrowsMovableTexts;
        [SerializeField] private TextMeshProUGUI[] progressArrowsNonMovableTexts;
        [SerializeField] private AnimationCurve moveArrowCurve;
        [SerializeField] private float moveArrowDuration;
        [SerializeField] private float moveArrowDelay;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private PaddingProgressBar progress;
        [SerializeField] private Button progressButton;

        [SerializeField] private Transform prizeContainer;
        [SerializeField] private RewardViewConfig rewardViewConfig;
        private Sequence sliderAnimation;
        [SerializeField] private AnimationConfig animationConfig;

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
        [SerializeField] private TextTooltip textTooltip;

        [Header("Vibrations")]
        [SerializeField] private VibrationSequenceConfig openVibrationSequence;
        [SerializeField] private VibrationSequenceConfig arrowVibrationSequence;
        [SerializeField] private VibrationSequenceConfig riseVibrationSequence;
        
        private readonly TimeStringBuilder timeStringBuilder = new();

        private PlayerDartsView localPlayerWidgetReference;

        private bool isCompletedState;
        private Sequence appearAnimation;
        private Coroutine rewardCoroutine;
        private Coroutine textCoroutine;

        public Action OnInfoPressed;
        public Action OnClaimButtonPressed;
        public Action OnChestInfoButtonPressed;
        public Action OnClosePressed;
        public Action OnProgressButtonPressed;

        private List<Tween> showTweens = new();
        private GameObject resultBox;

        private RewardItemControlFactory rewardItemControlFactory;
        private RewardItemControl currentRewardItemControl;

        private int tooltipValue = 0;

        private List<Vector3> progressArrowsInitPositions = new List<Vector3>();
        private Vector3 movableArrowInitPosition = Vector3.zero;

        public RewardListTooltip RewardListTooltip => rewardListTooltip;

        public RectTransform ProgressSecondTransform;
        public RectTransform ContentScrollTransform;
        public Mask ContentScrollMask;

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

        public bool ShowLeaderboardAnimation { get; set; }

        public void SetTimer(TimeSpan timeLeft)
        {
            if (timeStringBuilder.TryGetTimeStringFromTimeSpan(timeLeft, out var timeString))
            {
                SetTimer(timeString);
            }
        }

        public void SetData(IEnumerable<DartsLeaderboardWindow.DartsPlayerData> playerData, DartsFeatureConfig.PackRewardInfo[] packRewardInfos, RewardInfo rewardInfo, bool isCompleted = false, Action OnComplete = null)
        {
            var currentPlayerData = playerData.First(x => x.isPlayer);
            var currentPlace = currentPlayerData.place;

            isCompletedState = isCompleted;

            closeButton.gameObject.SetActiveChecked(!isCompleted);
            infoButton.gameObject.SetActiveChecked(!isCompleted);

            if (isCompleted)
            {
                widgetsContainer.GetComponent<VerticalLayoutGroup>().padding.bottom = bottomPaddingComplete;

                timerText.text = LocalizationManager.GetLocalizedText("kingscup.leaderboardwindow_completed");
            }
            else
            {
                widgetsContainer.GetComponent<VerticalLayoutGroup>().padding.bottom = bottomPaddingFull;
            }

            ClearData();
            CreatePlayerWidget(playerData, packRewardInfos, !isCompleted);

            constantTopPlayerWidget.SetData(currentPlayerData.name,
                currentPlayerData.place,
                currentPlayerData.cupPoints,
                ScrollToPlayer,
                ScrollToPlayer,
                currentPlayerData.rewardsCardsCount,
                new RewardInfo(),
                true,
                currentPlayerData.isShowingChest);

            constantBottomPlayerWidget.SetData(currentPlayerData.name,
                currentPlayerData.place,
                currentPlayerData.cupPoints,
                ScrollToPlayer,
                ScrollToPlayer,
                currentPlayerData.rewardsCardsCount,
                new RewardInfo(),
                true,
                currentPlayerData.isShowingChest);

            constantTopPlayerWidget.SetAvatarAndNameStyle(currentPlayerData);
            constantBottomPlayerWidget.SetAvatarAndNameStyle(currentPlayerData);

            // PlayerWidgetHelper.DecorateCommon(constantTopPlayerWidget, IPlayerWidget.PlacementSize.Common,
            //     currentPlayerData.name, currentPlayerData.avatarId, false);
            //
            // PlayerWidgetHelper.DecorateCommon(constantBottomPlayerWidget, IPlayerWidget.PlacementSize.Common,
            //     currentPlayerData.name, currentPlayerData.avatarId, false);

            textTooltip.ShowTooltip(false, true, preferredAlign: Tooltip.TooltipAlign.Down);
            rewardListTooltip.ShowTooltip(false, true, preferredAlign: Tooltip.TooltipAlign.Down);

            OnComplete?.Invoke();
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

        private void ShowTextTooltip()
        {
            if (textTooltip)
            {
                if (textCoroutine != null)
                {
                    StopCoroutine(textCoroutine);
                }
                textCoroutine = StartCoroutine(ShowTextTooltipRoutine());
            }
        }


        private IEnumerator ClearAndShowRewardsDelayed(Transform relativeTarget)
        {
            yield return new WaitForEndOfFrame();

            Vector3 targetWorldPosition = relativeTarget.position;
            Tooltip.TooltipAlign align = rewardListTooltip.DetectVerticalAlign(targetWorldPosition, tooltipViewport);
            RotateForeground(align, tooltipForeground);
            rewardListTooltip.ShowTooltip(true, false, relativeTarget, preferredAlign: align);
        }

        private IEnumerator ShowTextTooltipRoutine()
        {
            textTooltip.SetContent(string.Format(I2.Loc.LocalizationManager.GetTranslation("darts.main.tooltip"), tooltipValue));

            yield return new WaitForEndOfFrame();

            textTooltip.ShowTooltip(true, false, movableArrow.transform, preferredAlign: Tooltip.TooltipAlign.Up);
        }

        private void RotateForeground(Tooltip.TooltipAlign align, Image foreground)
        {
            if (foreground == null) return;
            switch (align)
            {
                case Tooltip.TooltipAlign.Down:
                    foreground.transform.rotation = Quaternion.Euler(0, 0f, 0f);
                    break;
                case Tooltip.TooltipAlign.Up:
                    foreground.transform.rotation = Quaternion.Euler(0, 0f, 180f);
                    break;
                default: break;
            }
        }


        public void ScrollToPlayer()
        {
            Canvas.ForceUpdateCanvases();
            int index = widgetsContainer.PlayerViews.FindIndex(x => x.Equals(localPlayerWidgetReference));
            scrollRect.DOVerticalNormalizedPos(1.0f - (float)index / widgetsContainer.PlayerViews.Count, 0.5f);
        }


        protected override void OnInitialize()
        {
            base.OnInitialize();

            rewardListTooltip.ShowTooltip(false, true);
        }


        protected override void OnShow()
        {
            base.OnShow();

            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 1.0f;
            PlayAnimation();

            // VibrationSequence vibrationSequence = new VibrationSequence(openVibrationSequence);
            // vibrationSequence.StartVibrationSequence();
        }

        protected override void OnHide()
        {
            for (int i = 0; i < showTweens.Count; i++)
            {
                showTweens[i].Kill();
            }
            showTweens.Clear();

            appearAnimation?.Kill();

            base.OnHide();

        }

        protected override void Awake()
        {
            closeButton?.onClick.AddListener(CloseWindow);
            fadeButton.onClick.AddListener(CloseWindow);

            if (!ShowLeaderboardAnimation)
            {
                scrollRect.onValueChanged.AddListener(UpdateConstantPlayerWidget);
            }

            progressBarButton.onClick.AddListener(ShowTextTooltip);

            if (infoButton)
            {
                infoButton.onClick.AddListener(InfoButton_OnClick);
            }

            progressButton.onClick.AddListener(ProgressButtonOnClick);
            base.Awake();
        }

        private void ProgressButtonOnClick()
        {
            OnProgressButtonPressed?.Invoke();
        }


        private void OnEnable()
        {
            timeStringBuilder.Localize();
            I2.Loc.LocalizationManager.OnLocalizeEvent += LocalizationManager_OnLocalizeEvent;
        }

        private void OnDisable()
        {
            I2.Loc.LocalizationManager.OnLocalizeEvent -= LocalizationManager_OnLocalizeEvent;
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

            TrackEntry trackEntry = skeletonAnimation.AnimationState.SetAnimation(trackIndex, animationName, loop);
            skeletonAnimation.Update(startOffset);
            skeletonAnimation.LateUpdate();

            return trackEntry;
        }


        private void ClaimPrize()
        {
            //uiManager.HideActiveFrame(this);
            OnClaimButtonPressed?.Invoke();
        }


        private void ShowChestTooltip() =>
            OnChestInfoButtonPressed?.Invoke();



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


        private void CreatePlayerWidget(IEnumerable<DartsLeaderboardWindow.DartsPlayerData> playerDatas, DartsFeatureConfig.PackRewardInfo[] packRewardInfos, bool isNeedAppearAnimation)
        {
            int index = 0;

            showTweens.Clear();
            showTweens = new List<Tween>();

            foreach (DartsLeaderboardWindow.DartsPlayerData playerData in playerDatas)
            {
                RewardInfo rewardInfo = (packRewardInfos.Length >= playerData.place) ? packRewardInfos[playerData.place - 1].RewardInfos[0] : new RewardInfo();
                var widget = widgetsContainer.AddView(playerData, rewardInfo);

                if (index < widgetsToAnimate)
                {
                    // var tween = widget.Show(isNeedAppearAnimation ? widgetShowDuration : 0.0f, widgetsShowCurve)
                    //     .SetDelay(isNeedAppearAnimation ? widgetShowDelay * index : 0.0f);
                    // tween.Play();
                    // showTweens.Add(tween);
                    // index++;
                }


                if (playerData.isPlayer)
                {
                    localPlayerWidgetReference = widget;
                }
            }
        }


        private void CloseWindow()
        {
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


        private void InfoButton_OnClick() => OnInfoPressed?.Invoke();

        private int currentProgressValue;
        private int targetProgressValue;

        private Coroutine doProgressCoroutine;

        public void SetCurrentProgress(int current)
        {
            currentProgressValue = current;

            SetProgress(currentProgressValue, targetProgressValue);
        }

        public void SetProgress(int currentProgress, int targetProgress)
        {
            currentProgressValue = currentProgress;
            targetProgressValue = targetProgress;

            UpdateProgressText();
            UpdateProgressSlider();
        }

        public void AddProgress(int addValue, Action callback)
        {
            currentProgressValue += addValue;

            UpdateProgressText();
            UpdateProgressSlider(false, callback);
        }

        private void UpdateProgressText()
        {
            if (progressText)
            {
                progressText.text = $"{currentProgressValue}/{targetProgressValue}";
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

        public void UpdateProgressArrows(int value)
        {
            Vector3 shift = Vector3.zero;
            for (var i = 0; i < progressArrowsInitPositions.Count; i++)
            {
                if (i == value)
                {
                    shift = progressArrowsInitPositions[i] - movableArrowInitPosition;
                }
            }

            movableArrow.transform.position = movableArrowInitPosition + shift;
            for (var i = 0; i < progressArrowsMovableTexts.Length; i++)
            {
                progressArrowsMovableTexts[i].transform.position = progressArrowsInitPositions[i];
            }
        }

        public void AnimateProgressArrows(int oldValue, int newValue)
        {
            Vector3 oldArrowPosition = Vector3.zero;
            Vector3 newArrowPosition = Vector3.zero;
            for (var i = 0; i < progressArrowsInitPositions.Count; i++)
            {
                if (i == oldValue)
                {
                    oldArrowPosition = progressArrowsInitPositions[i];
                }
                if (i == newValue)
                {
                    newArrowPosition = progressArrowsInitPositions[i];
                }
            }
            Vector3 shift = oldArrowPosition - movableArrowInitPosition;
            movableArrow.transform.position = oldArrowPosition;
            for (var i = 0; i < progressArrowsMovableTexts.Length; i++)
            {
                progressArrowsMovableTexts[i].transform.position = progressArrowsInitPositions[i];
            }
            DOTween.To(() => movableArrow.transform.position, value =>
            {
                movableArrow.transform.position = value;

                shift = value - movableArrowInitPosition;
                for (var i = 0; i < progressArrowsMovableTexts.Length; i++)
                {
                    progressArrowsMovableTexts[i].transform.position = progressArrowsInitPositions[i];
                }
            }, newArrowPosition, moveArrowDuration).SetEase(moveArrowCurve).SetDelay(moveArrowDelay).OnComplete(() =>
            {
                ShowTextTooltip();
                
                VibrationSequence vibrationSequence = new VibrationSequence(arrowVibrationSequence);
                vibrationSequence.StartVibrationSequence();
                
                UiManager.Instance.SoundManager.Play("[Pass-Audio] progress_lines_end");
            });
        }

        public void InitProgressArrows(int[] multipliers, int currentMultiplier)
        {
            if (multipliers != null && multipliers.Length > 0)
            {
                for (var i = 0; i < progressArrowsMovableTexts.Length; i++)
                {
                    if (multipliers.Length > i)
                    {
                        progressArrowsMovableTexts[i].text = string.Format("x{0}", multipliers[i].ToString());
                    }
                }
                for (var i = 0; i < progressArrowsNonMovableTexts.Length; i++)
                {
                    if (multipliers.Length > i)
                    {
                        progressArrowsNonMovableTexts[i].text = string.Format("x{0}", multipliers[i].ToString());
                    }
                }

                tooltipValue = multipliers[currentMultiplier];
            }

            for (var i = 0; i < progressArrowsMovableTexts.Length; i++)
            {
                progressArrowsInitPositions.Add(progressArrowsMovableTexts[i].transform.position);
            }
            movableArrowInitPosition = movableArrow.transform.position;
        }

        private void DoProgressValue(float targetValue, Action callback)
        {
            sliderAnimation?.Kill();
            sliderAnimation = DOTween.Sequence();

            sliderAnimation.OnComplete(() =>
            {
                progress.Value = targetValue;
                //callback?.Invoke();
            });
            var startValue = progress.Value;

            sliderAnimation.Insert(animationConfig.sliderAnimationDelay, DOTween.To(value =>
                {
                    progress.Value = value;
                },
                    startValue, targetValue, animationConfig.sliderFillingDuration)).SetEase(animationConfig.sliderAnimationCurve);

            sliderAnimation.InsertCallback(animationConfig.sliderAnimationDelay +
                animationConfig.sliderFillingDuration - animationConfig.callbackEnding, () =>
            {
                callback?.Invoke();
            });
        }

        [Header("Position Switch Animation")]
        [SerializeField] private AnimationCurve autoScrollEase;
        [SerializeField] private float autoScrollTime = 0.7f;

        [SerializeField] private AnimationCurve autoScrollEaseFast;
        [SerializeField] private float autoScrollTimeFast = 0.3f;
        [SerializeField] private int maxDeltaForFastScroll = 2;
        [SerializeField] private float putDownLineOffsetTop2 = 0.2f;

        [SerializeField] private VerticalLayoutGroup tableLayout;

        [Header("Animate Changes")]
        [SerializeField] private float startDelay = 0.6f;
        [SerializeField] private float endDelay = 1.0f;

        public void PlayLeaderboardAnimation(int currentPlace, int oldPlace, int playersAmount, string userId)
        {
            if (oldPlace <= currentPlace)
            {
                int index;
                var j = 0;

                for (index = 0; index <= widgetsToAnimate && index < widgetsContainer.PlayerViews.Count; index++)
                {
                    var widget = widgetsContainer.PlayerViews[index];
                    var tween = widget.Show(widgetShowDuration, widgetsShowCurve).SetDelay(widgetShowDelay * j);

                    if (index == oldPlace - 1)
                    {
                        j++;
                        widget = localPlayerWidgetReference;
                        tween = widget.Show(widgetShowDuration, widgetsShowCurve).SetDelay(widgetShowDelay * j);
                    }

                    tween.Play();
                    j++;
                }
                
                if (j > 3)
                {
                    var tweenTop = constantTopPlayerWidget.Show(widgetShowDuration, widgetsShowCurve)
                        .SetDelay(widgetShowDelay * (j - 3));
                    var tweenBottom = constantBottomPlayerWidget.Show(widgetShowDuration, widgetsShowCurve)
                        .SetDelay(widgetShowDelay * (j - 3));

                    tweenTop.Play();
                    tweenBottom.Play();
                }
            }
            else
            {
                Sequence mainSequence = DOTween.Sequence();

                constantTopPlayerWidget.GetComponent<CanvasGroup>().alpha = 0.0f;
                constantBottomPlayerWidget.GetComponent<CanvasGroup>().alpha = 0.0f;

                localPlayerWidgetReference.UpdatePositionText(oldPlace);


                var half = (int)(widgetsToAnimate * 0.5f);
                var startIndex = oldPlace - half >= 0 ? oldPlace - half : 0;

                var j = 0;

                for (var index = startIndex;
                     index < oldPlace + half && index < widgetsContainer.PlayerViews.Count;
                     index++)
                {
                    var widget = widgetsContainer.PlayerViews[index];

                    if (widget.IsPlayer)
                    {
                        continue;
                    }

                    var tween = widget.Show(widgetShowDuration, widgetsShowCurve).SetDelay(widgetShowDelay * j);

                    if (index == oldPlace - 1)
                    {
                        j++;
                        widget = localPlayerWidgetReference;
                        tween = widget.Show(widgetShowDuration, widgetsShowCurve).SetDelay(widgetShowDelay * j);
                    }

                    tween.Play();
                    j++;
                }


                mainSequence.AppendInterval(startDelay);

                Canvas lineCanvas = null;
                PlayerDartsView playerViewRef = null;

                mainSequence.Append(AnimateLeaderboardPlayerView(currentPlace, oldPlace, playersAmount, userId, (sequence, playerView) =>
                {
                    // var progressSecondTransformIndex = ProgressSecondTransform.GetSiblingIndex();
                    // var contentScrollTransformIndex = ContentScrollTransform.GetSiblingIndex();
                    //
                    // ProgressSecondTransform.SetSiblingIndex(contentScrollTransformIndex);
                    // ContentScrollTransform.SetSiblingIndex(progressSecondTransformIndex);
                    //
                    // ContentScrollMask.enabled = false;

                    // playerViewRef = playerView;
                    //
                    // lineCanvas = playerView.gameObject.AddComponent<Canvas>();
                    // lineCanvas.enabled = true;
                    // lineCanvas.overrideSorting = true;
                    // lineCanvas.sortingLayerName = "UI_Popups";
                    // lineCanvas.sortingOrder += 2;
                    // lineCanvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord3;
                    // lineCanvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord2;
                    // playerViewRef.gameObject.layer = LayerMask.NameToLayer("UiMain");
                    
                    VibrationSequence vibrationSequence = new VibrationSequence(riseVibrationSequence);
                    vibrationSequence.StartVibrationSequence();
                    
                    mainSequence.Append(playerView.PlayRise(false));

                }));

                mainSequence.AppendInterval(endDelay);

                void OnComplete()
                {
                    // var progressSecondTransformIndex = ProgressSecondTransform.GetSiblingIndex();
                    // var contentScrollTransformIndex = ContentScrollTransform.GetSiblingIndex();
                    //
                    // ProgressSecondTransform.SetSiblingIndex(contentScrollTransformIndex);
                    // ContentScrollTransform.SetSiblingIndex(progressSecondTransformIndex);
                    //
                    // ContentScrollMask.enabled = true;

                    // if (lineCanvas != null)
                    // {
                    //     Destroy(lineCanvas);
                    // }
                    //
                    // playerViewRef.gameObject.layer = LayerMask.NameToLayer("UI");
                }

                mainSequence.OnComplete(OnComplete);
                mainSequence.OnKill(OnComplete);
            }
        }

        public Sequence AnimateLeaderboardPlayerView(int currentPlace, int oldPlace, int playersAmount, string userId, Action<Sequence, PlayerDartsView> onCanInsertCupAnimation)
        {
            var visible = widgetsContainer.PlayerViews;

            void ShiftPlaces(int min, int max, int delta, int from, int to)
            {
                for (int i = 0; i < visible.Count; ++i)
                {
                    var data = visible[i];
                    if (data.Position < min || data.Position > max)
                    {
                        continue;
                    }

                    bool isCurrentPlayer = data.IsPlayer;


                    if (data.Position != from)
                    {
                        data.Position += delta;
                    }
                    else
                    {
                        data.Position = to;
                    }

                    visible[i].SetData(data, isCurrentPlayer);

                    // visible[i].SetData(playerData.name, playerData.place, playerData.cupPoints, playerData.OnChestPressed, playerData.OnPlayerPressed,
                    //     playerData.rewardsCardsCount, rewardInfo, playerData.isPlayer, playerData.isShowingChest, playerData.isShowingJoker);
                }
            }

            int fromPlaceViewIndex = -1;
            int toPlaceViewIndex = -1;

            for (int i = 0; i < visible.Count; ++i)
            {
                if (visible[i].Position == currentPlace)
                {
                    fromPlaceViewIndex = i;
                }

                if (visible[i].Position == oldPlace)
                {
                    toPlaceViewIndex = i;
                }
            }

            if (fromPlaceViewIndex == toPlaceViewIndex ||
                fromPlaceViewIndex < 0 ||
                toPlaceViewIndex < 0 ||
                fromPlaceViewIndex >= visible.Count ||
                toPlaceViewIndex >= visible.Count)
            {
                return DOTween.Sequence();
            }

            PlayerDartsView playerSourcePanel = visible[fromPlaceViewIndex];
            PlayerDartsView targetBotPanel = visible[toPlaceViewIndex];

            int max = Mathf.Max(currentPlace, oldPlace);
            int min = Mathf.Min(currentPlace, oldPlace);
            int delta = oldPlace > currentPlace ? -1 : +1;

            // rollback places

            ShiftPlaces(min, max, delta, currentPlace, oldPlace);

            bool needForceRefresh = playerSourcePanel.transform.localPosition == targetBotPanel.transform.localPosition;

            if (needForceRefresh)
            {
                // we are probably trying to move panels before layout is even built
                LayoutRebuilder.ForceRebuildLayoutImmediate(tableLayout.transform as RectTransform);
                Canvas.ForceUpdateCanvases();
            }

            int targetSiblingIndex = playerSourcePanel.transform.GetSiblingIndex();
            Vector3 targetPosition = playerSourcePanel.transform.localPosition;

            if (needForceRefresh && currentPlace == 1)
            {
                //    targetPosition -= new Vector3(0f, tableLayout.spacing, 0f);
            }

            playerSourcePanel.transform.SetSiblingIndex(targetBotPanel.transform.GetSiblingIndex());
            LayoutRebuilder.ForceRebuildLayoutImmediate(playerSourcePanel.transform.parent.GetComponent<RectTransform>());
            Canvas.ForceUpdateCanvases();

            scrollRect.FocusOnItem(playerSourcePanel.GetComponent<RectTransform>());

            Sequence sequence = DOTween.Sequence();

            onCanInsertCupAnimation?.Invoke(sequence, playerSourcePanel);

            void OnMoveUpdate(float elapsedNormalized)
            {
                int position = (int)Mathf.Lerp(oldPlace, currentPlace, elapsedNormalized);

                if (position != playerSourcePanel.GetPositionInView)
                {
                    // MMVibrationManager.Haptic(HapticTypes.LightImpact);
                }

                playerSourcePanel.UpdatePositionText(position);
            }

            sequence.AppendCallback(() => OnMoveUpdate(0f));

            AudioSourceWrapper scrollSound = null;
            sequence.AppendCallback(() => scrollSound = MainManager.Instance.SoundManager.Play("ui_meta_cashcup_autoscroll"));


            sequence.Append(MovePlayerToGoalSlot(playerSourcePanel,
                targetPosition,
                targetSiblingIndex,
                OnMoveUpdate,
                currentPlace,
                oldPlace,
                playersAmount,
                out Action onShiftCallback));

            //var slidingElements = SlidingElements(playerSourcePanel, currentPlace, oldPlace, 1.5f);
            // // // sequence.Append(slidingElements);
            //slidingElements.Play();

            var positionsSwitched = false;

            LayoutRebuilder.ForceRebuildLayoutImmediate(tableLayout.transform as RectTransform);
            Canvas.ForceUpdateCanvases();

            void SwitchPositionsToCurrent(bool completed)
            {
                if (positionsSwitched)
                {
                    return;
                }

                positionsSwitched = true;

                ShiftPlaces(min, max, -delta, oldPlace, currentPlace);
                onShiftCallback?.Invoke();
                if (scrollSound != null)
                {
                    MainManager.Instance.SoundManager.Stop(scrollSound);
                }
            }

            sequence.InsertCallback(playerSourcePanel.RiseTime + autoScrollTime * 0.5f, () => SwitchPositionsToCurrent(false));
            LayoutRebuilder.ForceRebuildLayoutImmediate(tableLayout.transform as RectTransform);
            Canvas.ForceUpdateCanvases();

            sequence.OnKill(() =>
            {
                SwitchPositionsToCurrent(true);
                constantTopPlayerWidget.GetComponent<CanvasGroup>().alpha = 1.0f;
                constantBottomPlayerWidget.GetComponent<CanvasGroup>().alpha = 1.0f;
            });
            sequence.OnComplete(() =>
            {
                SwitchPositionsToCurrent(true);
                constantTopPlayerWidget.GetComponent<CanvasGroup>().alpha = 1.0f;
                constantBottomPlayerWidget.GetComponent<CanvasGroup>().alpha = 1.0f;
            });

            return sequence;
        }

        [SerializeField] private float shuffleOffsetNormal = -0.4f;
        [SerializeField] private float shuffleOffsetNormalFast = -0.4f;
        [SerializeField] private float firstPlaceShuffleOffset = -0.4f;
        [SerializeField] private float firstPlaceRewardingOffset = -0.8f;

        [SerializeField] private float firstPlaceShuffleOffsetFast = -0.3f;
        [SerializeField] private float firstPlaceRewardingOffsetFast = -0.25f;
        [SerializeField] private float normalRewardingOffset = 0.3f;

        private Sequence SlidingElements(PlayerDartsView source, int toPlace, int fromPlace, float delay)
        {
            var sequence = DOTween.Sequence();
            var sourceRect = source.GetComponent<RectTransform>();
            var layoutRoot = sourceRect.parent.GetComponent<RectTransform>();

            var visible = widgetsContainer.PlayerViews;
            var count = fromPlace - 1 - toPlace;

            var isFastScroll = fromPlace - toPlace <= maxDeltaForFastScroll;
            var scrollTime = isFastScroll ? autoScrollTimeFast : autoScrollTime;
            var finalTime = scrollTime / count * 0.5f * 3;

            sequence.AppendInterval(delay);
            for (var i = fromPlace - 1; i > toPlace; i -= 2)
            {
                var player = visible[i];
                player.ChangeAnchorToTop();

                var rt = player.GetComponent<RectTransform>();

                var sizeDeltaBase = rt.sizeDelta;
                var sizeDeltaTarget = new Vector2(rt.sizeDelta.x, 360);

                sequence.Append(rt.DOSizeDelta(sizeDeltaTarget, finalTime));
                sequence.Append(rt.DOSizeDelta(sizeDeltaBase, finalTime));
                sequence.AppendCallback(() =>
                {
                    player.ChangeAnchorToMiddle();
                });
            }

            sequence.onUpdate = () =>
            {
                LayoutRebuilder.MarkLayoutForRebuild(layoutRoot);
            };

            sequence.OnComplete(OnComplete);
            sequence.OnKill(OnComplete);

            return sequence;

            void OnComplete()
            {
                Canvas.ForceUpdateCanvases();
            }
        }

        private Sequence MovePlayerToGoalSlot(PlayerDartsView source,
            Vector3 movePosition,
            int siblingIndex,
            Action<float> onUpdate,
            int toPlace,
            int fromPlace,
            int playersAmount,
            out Action onShiftCallback)
        {
            RectTransform sourceRect = source.GetComponent<RectTransform>();
            RectTransform layoutRoot = sourceRect.parent.GetComponent<RectTransform>();

            int originalSourceIndex = sourceRect.GetSiblingIndex();
            bool animatingDown = siblingIndex > originalSourceIndex;
            float spacing = tableLayout.spacing;

            bool isFastScroll = (fromPlace - toPlace) <= maxDeltaForFastScroll;
            AnimationCurve scrollCurve = isFastScroll ? autoScrollEaseFast : autoScrollEase;

            float scrollTime = isFastScroll ? autoScrollTimeFast : autoScrollTime;

            GameObject sourceStubObject = new GameObject();
            RectTransform sourceStubObjectRect = sourceStubObject.AddComponent<RectTransform>();
            sourceStubObjectRect.transform.SetParent(sourceRect.parent);
            sourceStubObjectRect.anchoredPosition = sourceRect.anchoredPosition;
            sourceStubObjectRect.sizeDelta = sourceRect.sizeDelta;
            sourceStubObjectRect.localScale = Vector3.one;
            sourceStubObjectRect.transform.SetSiblingIndex(sourceRect.GetSiblingIndex() + (animatingDown ? 0 : 1));

            // move on top of every other panels
            sourceRect.SetSiblingIndex(sourceRect.transform.parent.childCount);

            GameObject targetStubObject = new GameObject();
            RectTransform targetStubObjectRect = targetStubObject.AddComponent<RectTransform>();
            targetStubObjectRect.transform.SetParent(sourceRect.parent);
            targetStubObjectRect.anchoredPosition = sourceRect.anchoredPosition;
            targetStubObjectRect.sizeDelta = new Vector2(0, -spacing);
            targetStubObjectRect.localScale = Vector3.one;
            targetStubObjectRect.transform.SetSiblingIndex(siblingIndex + (animatingDown ? 1 : 0));

            LayoutElement sourceRectLayoutElement = source.gameObject.GetComponent<LayoutElement>();
            sourceRectLayoutElement.ignoreLayout = true;

            Sequence sequence = DOTween.Sequence();
            sequence.onUpdate = () =>
            {
                scrollRect.FocusOnItem(sourceRect);
                LayoutRebuilder.MarkLayoutForRebuild(layoutRoot);
            };

            float targetSize = source.NormalSize.y;

            sequence.Insert(
                0f,
                sourceStubObjectRect.DOSizeDelta(
                    new Vector2(sourceRect.sizeDelta.x, -spacing),
                    scrollTime));

            sequence.Insert(
                0f,
                targetStubObjectRect.DOSizeDelta(
                    new Vector2(sourceRect.sizeDelta.x, targetSize),
                    scrollTime));

            TweenerCore<Vector3, Vector3, VectorOptions> moveSequence =
                source.transform.DOLocalMove(movePosition, scrollTime).SetEase(scrollCurve);
            moveSequence.OnUpdate(() => onUpdate(moveSequence.ElapsedPercentage()));

            sequence.Insert(0f, moveSequence);

            float downTime = 0f;

            if (toPlace == 1)
            {
                downTime = source.DownTime;
                sequence.Insert(scrollTime, source.PlayDown());

                // downTime = source.DownTimeToFirst;
                // PlayerDartsView top1Bot = widgetsContainer.PlayerViews.FirstOrDefault(x => x.Position == 1);
                //
                // Sequence switchTop2 = DOTween.Sequence();
                //
                // switchTop2.Insert(0f, top1Bot.PlayRotate_FirstToOther(() =>
                // {
                //     top1Bot.SetStyle(true);
                //     top1Bot.SetBackStyle(2, false);
                //     top1Bot.UpdatePosition(2, 3, 10);
                // }));
                //
                // switchTop2.Insert(0f, source.PlayDownToFirstPlace(() =>
                // {
                //     source.HideRewardsInstant();
                //     source.SetStyle(false);
                //     source.SetBackStyle(toPlace, true);
                // }));
                //
                // sequence.Insert(scrollTime - putDownLineOffsetTop2, switchTop2);
            }
            else
            {
                downTime = source.DownTime;
                sequence.Insert(scrollTime, source.PlayDown());
            }

            float shuffleOffset;

            if (toPlace == 1)
            {
                shuffleOffset = isFastScroll ? firstPlaceShuffleOffsetFast : firstPlaceShuffleOffset;
            }
            else
            {
                shuffleOffset = isFastScroll ? shuffleOffsetNormalFast : shuffleOffsetNormal;
            }

            // sequence.Insert(downTime + shuffleOffset, PlayShuffleLinesAround(toPlace, playersAmount));

            bool rewardViewWillBeChanged = toPlace <= 10 && false;
            // \source.CanChangeRewardView(fromPlace, toPlace, topSize, prizePlaces);

            onShiftCallback = null;
            // if (rewardViewWillBeChanged)
            // {
            //     Sequence changeReward = DOTween.Sequence();
            //
            //     if (toPlace != 1)
            //     {
            //         if (source.HasVisibleReward)
            //         {
            //             onShiftCallback = () =>
            //             {
            //                 source.ChestVariants.SelectVariant(
            //                     source.GetRewardViewVariant(fromPlace, topSize, prizePlaces));
            //             };
            //             changeReward.Append(source.PlayDisappearRewards());
            //         }
            //         else
            //         {
            //             source.HideRewardsInstant();
            //         }
            //     }
            //
            //     changeReward.AppendCallback(() => { source.UpdatePosition(toPlace, topSize, prizePlaces); });
            //
            //     // changeReward.Append(source.PlayAppearReward(toPlace));
            //
            //     float rewardOffset = 0f;
            //
            //     if (toPlace == 1)
            //     {
            //         rewardOffset = isFastScroll ? firstPlaceRewardingOffsetFast : firstPlaceRewardingOffset;
            //     }
            //     else
            //     {
            //         rewardOffset = normalRewardingOffset;
            //     }
            //
            //     sequence.Insert(downTime + rewardOffset, changeReward);
            // }

            void OnComplete(bool kill)
            {
                DestroyImmediate(targetStubObject);
                DestroyImmediate(sourceStubObject);

                Transform sourceTransform = source.transform;

                sourceTransform.SetSiblingIndex(originalSourceIndex);
                sourceTransform.SetSiblingIndex(siblingIndex);
                sourceTransform.localScale = Vector3.one;

                sourceRectLayoutElement.ignoreLayout = false;

                source.UpdatePosition(toPlace, 3, 10);
                source.UpdatePositionText(toPlace);
                source.SetStyle(toPlace != 1);


                if (true)
                {
                    // source.KillAnimatorController();
                    LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRoot);
                    Canvas.ForceUpdateCanvases();
                    scrollRect.FocusOnItem(sourceRect);
                }

                // MMVibrationManager.Haptic(HapticTypes.MediumImpact);
            }

            sequence.OnKill(() => OnComplete(true));
            sequence.OnComplete(() => OnComplete(false));

            return sequence;
        }

        private Sequence PlayShuffleLinesAround(int toPlace, int playersAmount)
        {
            Sequence shuffleLines = DOTween.Sequence();

            //Play around lines shuffle
            for (int offset = -2; offset <= 2; offset++)
            {
                if (offset == 0) continue;

                int playerIndex = toPlace + offset - 1;
                if (playerIndex < 0 || playerIndex >= playersAmount) continue;

                PlayerDartsView aroundLine = widgetsContainer.PlayerViews[playerIndex];
                if (aroundLine == null) continue;

                shuffleLines.Insert(0f, aroundLine.PlayShuffle(offset));
            }

            return shuffleLines;
        }

        private void StopProgressCoroutine()
        {
            if (doProgressCoroutine == null)
            {
                return;
            }

            StopCoroutine(doProgressCoroutine);
            doProgressCoroutine = null;
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
    }
}
