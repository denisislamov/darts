using System;
using System.Collections.Generic;
using System.ComponentModel;
using DG.Tweening;
using Dip.Leaderboards;
using Dip.MetaGame.Rewards;
using Dip.Rewards;
using Dip.Ui;
using Dip.Ui.Rewards;
using TMPro;
using Ui.Manager;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

namespace Dip.Features.Darts.Ui
{
    public class PlayerDartsView : MonoBehaviour, IPlayerWidget
    {
        [SerializeField] private Transform boxContainer;
        [SerializeField] private Transform cupContainer;

        [Header("Places")]
        [SerializeField] private GameObject firstPlaceContainerPrefab;
        [SerializeField] private GameObject secondPlaceContainerPrefab;
        [SerializeField] private GameObject thirdPlaceContainerPrefab;

        [SerializeField] private Material firstPlaceTmpMaterial;
        [SerializeField] private Material secondPlaceTmpMaterial;
        [SerializeField] private Material thirdPlaceTmpMaterial;


        [SerializeField] private Transform placeRoot;
        [SerializeField] private TextMeshProUGUI winPlaceText;
        [SerializeField] private TextMeshProUGUI generalPlaceText;

        [Header("Cup and Chest")]
        [SerializeField] private Transform chestsTransform;
        [SerializeField] private GameObject normalChestPrefab;
        [SerializeField] private GameObject firstPlaceChestPrefab;
        [SerializeField] private GameObject secondPlaceChestPrefab;
        [SerializeField] private GameObject thirdPlaceChestPrefab;
        [SerializeField] private TextMeshProUGUI cupPointsText;
        [SerializeField] private Transform prizeContainer;

        [Header("Season Collections")]
        [SerializeField] private Transform seasonPassLabesRoot;
        [SerializeField] private GameObject jokerLabelPrefab;
        [SerializeField] private GameObject ordinaryLabelPrefab;

        [Header("Player name, avatar and plank color")]
        [SerializeField] private GameObject plankNormal;
        [SerializeField] private GameObject plankGreen;
        [SerializeField] private GameObject plankPlayerCupGreen;
        [SerializeField] private GameObject plankPlayerCupNormal;
        [SerializeField] private TextMeshProUGUI userNameText;
        [SerializeField] private TextMeshProUGUI highlightedUserNameText;
        [SerializeField] private Image playerAvatar;
        [SerializeField] private Image avatarFrame;
        [SerializeField] private Image decorFrameGold;

        [Header("Extra settings")]
        [SerializeField] private Button chestInfoButton;
        [SerializeField] private Transform container;
        [SerializeField] private Transform hideAnchor;
        [SerializeField] private Button playerInfoButton;

        [Header("Rewards config")]
        [SerializeField]
        private RewardViewConfig rewardViewConfig;

        [SerializeField] private Color normalNameColor;
        [SerializeField] private Sprite frameSprite;

        [SerializeField] private float downNormalSoundDelay = 0.0f;
        [SerializeField] private float downFirstPlaceSoundDelay = 0.0f;

        private RewardItemControlFactory rewardItemControlFactory;
        private RewardItemControl currentRewardItemControl;

        public Transform ChestTransform => chestsTransform;

        public int Position { get; set; }
        public bool IsPlayer { get; set; }

        public float RiseTime => 1.0f;

        public int GetPositionInView => int.Parse(generalPlaceText.text);

        [field: SerializeField]
        public Vector2 NormalSize { get; private set; } = new Vector2(1040, 185);
        [field: SerializeField]
        public Vector2 FirstPlaceSize { get; private set; } = new Vector2(1070, 205);

        private static readonly int rotate = Animator.StringToHash("Rotate");
        private static readonly int downToFirst = Animator.StringToHash("DownToFirst");
        private static readonly int down = Animator.StringToHash("Down");

        [SerializeField] private AnimationClip downClip;
        [SerializeField] private AnimationClip rotateClip;
        [SerializeField] private Animator lineAnimator;
        // [SerializeField] private CashCupLineAnimationEventsHandler animationEventsHandler;

        public float DownTimeToFirst => downToFirstClip.length;
        public float DownTime => downClip.length;

        private static readonly int shuffleTrigger_m2 = Animator.StringToHash("Shuffle_-2");
        private static readonly int shuffleTrigger_m1 = Animator.StringToHash("Shuffle_-1");
        private static readonly int shuffleTrigger_1 = Animator.StringToHash("Shuffle_1");
        private static readonly int shuffleTrigger_2 = Animator.StringToHash("Shuffle_2");

        private static readonly Dictionary<int, int> shuffleTriggers = new Dictionary<int, int>()
        {
            {-2, shuffleTrigger_m2},
            {-1, shuffleTrigger_m1},
            {1, shuffleTrigger_1},
            {2, shuffleTrigger_2},
        };

        [SerializeField] private AnimationClip shuffleClip_1;
        [SerializeField] private AnimationClip shuffleClip_2;
        [SerializeField] private AnimationClip shuffleClip_m1;
        [SerializeField] private AnimationClip shuffleClip_m2;

        [SerializeField] private RectTransform containerRectTransform;

        private static readonly int resetTrigger = Animator.StringToHash("Reset");
        public Sequence PlayShuffle(int index)
        {
            Dictionary<int, AnimationClip> triggerClips = new Dictionary<int, AnimationClip>()
            {
                {shuffleTrigger_1, shuffleClip_1},
                {shuffleTrigger_2, shuffleClip_2},
                {shuffleTrigger_m1, shuffleClip_m1},
                {shuffleTrigger_m2, shuffleClip_m2},
            };
            //
            Sequence sequence = DOTween.Sequence();
            lineAnimator.SetEnabledChecked(true);
            //
            float clipLength = 0f;
            sequence.InsertCallback(0f, () =>
            {
                if (shuffleTriggers.TryGetValue(index, out int triggerHash))
                {
                    lineAnimator.SetTrigger(triggerHash);
                }

                if (triggerClips.TryGetValue(triggerHash, out AnimationClip clip))
                {
                    clipLength = clip.length;
                }
            });

            sequence.AppendInterval(clipLength);

            sequence.OnKill(KillAnimatorController);

            return sequence;
        }

        public Sequence PlayRotate_FirstToOther(Action onRotate)
        {
            Sequence sequence = DOTween.Sequence();
            // animationEventsHandler.onRotatedFromFirst = onRotate;
            sequence.InsertCallback(0f, () =>
            {
                lineAnimator.SetTrigger(rotate);
            });

            sequence.AppendInterval(rotateClip.length);

            return sequence;
        }

        [SerializeField] private AnimationClip downToFirstClip;
        public Sequence PlayDownToFirstPlace(Action onRotate)
        {
            Sequence sequence = DOTween.Sequence();

            sequence.InsertCallback(0f, () =>
            {
                lineAnimator.SetTrigger(downToFirst);
            });

            sequence.InsertCallback(downFirstPlaceSoundDelay, () =>
            {
                UiManager.Instance.SoundManager.Play("ui_meta_cashcup_winpopup_prize");
            });

            //sequence.InsertCallback(downToFirstClip.length * 0.1f,() => MainManager.Instance.SoundManager.Play(CashCupSoundKeys.BarPut));
            sequence.AppendInterval(downToFirstClip.length);

            return sequence;
        }

        public Sequence PlayDown()
        {
            Sequence sequence = DOTween.Sequence();

            sequence.InsertCallback(0f, () =>
            {
                lineAnimator.SetEnabledChecked(true);
                lineAnimator.SetTrigger(down);
            });

            sequence.InsertCallback(downNormalSoundDelay, () =>
            {
                UiManager.Instance.SoundManager.Play("[Pass-Audio] tutor_popup");
            });

            // sequence.InsertCallback(downClip.length * 0.1f,() => MainManager.Instance.SoundManager.Play(CashCupSoundKeys.BarPut));
            sequence.AppendInterval(downClip.length);

            return sequence;
        }

        public void KillAnimatorController()
        {
            lineAnimator.Rebind();
            lineAnimator.SetTrigger(resetTrigger);

            var rt = container as RectTransform;
            if (rt != null)
            {
                rt.sizeDelta = Vector2.zero;
                rt.localScale = Vector3.one;
            }

            // shadow.DOOnlyAlpha(shadowOriginalAlpha, 0f);
            // (shadow.transform as RectTransform).anchoredPosition = shadowOriginalAnchoredPosition;
            // shadow.transform.localScale = Vector3.one;
            //
            // sideVariants.gameObject.SetActiveChecked(false);
            // backSideVariants.gameObject.SetActiveChecked(false);
            // chestVariants.transform.localScale = Vector3.one;

            //rewardsAppearAnimator.SetEnabledChecked(false);
        }

        public void SetStyle(bool isNormal)
        {
            // mainRect.sizeDelta = isNormal ? NormalSize : FirstPlaceSize;
            // cupPointsText.gameObject.SetActiveChecked(isNormal);
            // cupPointsText_firstPlace.gameObject.SetActiveChecked(!isNormal);
            //
            // userName_firstPlace.gameObject.SetActiveChecked(!isNormal);
            // userNameText.gameObject.SetActiveChecked(isNormal);
            //
            // layoutElement.preferredHeight = isNormal ? NormalSize.y : FirstPlaceSize.y;
            // layoutElement.preferredWidth = isNormal ? NormalSize.x : FirstPlaceSize.x;
            //
            // LayoutRebuilder.ForceRebuildLayoutImmediate(mainRect);
        }

        public void SetBackStyle(int position, bool isPlayer)
        {
            //0 Normal
            //1 Green
            //2 Gold
            // int backVariant;
            //
            // if (position == 1)
            // {
            //     backVariant = 2;
            // }
            // else
            // {
            //     backVariant = isPlayer ? 1 : 0;
            // }
            //
            // cupBackVariants.SelectVariant(backVariant);
            // plankBackVariants.SelectVariant(backVariant);
        }

        private int cachedPosition;

        public void UpdatePosition(int position, int topSize, int prizePlaces)
        {
            if (position == cachedPosition)
            {
                return;
            }

            cachedPosition = position;

            generalPlaceText.text = position.ToString();
            winPlaceText.text = position.ToString();

            switch (position)
            {
                case 1:
                    firstPlaceContainer.SetActive(true);
                    secondPlaceContainer.SetActive(false);
                    thirdPlaceContainer.SetActive(false);
                    break;
                case 2:
                    firstPlaceContainer.SetActive(false);
                    secondPlaceContainer.SetActive(true);
                    thirdPlaceContainer.SetActive(false);
                    break;
                case 3:
                    firstPlaceContainer.SetActive(false);
                    secondPlaceContainer.SetActive(false);
                    thirdPlaceContainer.SetActive(true);
                    break;
                default:
                    firstPlaceContainer.SetActive(false);
                    secondPlaceContainer.SetActive(false);
                    thirdPlaceContainer.SetActive(false);
                    break;
            }
            // generalPlaceText.gameObject.SetActiveChecked(position > medalVariants.VariantsAmount);
            //
            // int rewardVariant = GetRewardViewVariant(position, topSize, prizePlaces);
            //
            // chestVariants.SelectVariant(rewardVariant);
            // medalVariants.SelectVariant(position - 1);
        }

        public void HideRewardsInstant()
        {
            //    rewardsAppearAnimator.SetTrigger(hideRewardsTrigger);
        }

        public void UpdatePositionText(int position)
        {
            generalPlaceText.text = position.ToString();
            winPlaceText.text = position.ToString();

            switch (position)
            {
                case 1:
                    firstPlaceContainer.SetActive(true);
                    secondPlaceContainer.SetActive(false);
                    thirdPlaceContainer.SetActive(false);
                    break;
                case 2:
                    firstPlaceContainer.SetActive(false);
                    secondPlaceContainer.SetActive(true);
                    thirdPlaceContainer.SetActive(false);
                    break;
                case 3:
                    firstPlaceContainer.SetActive(false);
                    secondPlaceContainer.SetActive(false);
                    thirdPlaceContainer.SetActive(true);
                    break;
                default:
                    firstPlaceContainer.SetActive(false);
                    secondPlaceContainer.SetActive(false);
                    thirdPlaceContainer.SetActive(false);
                    break;
            }
        }

        // public void SetData(PlayerDartsView playerDataView)
        // {
        //     Position = playerDataView.Position;
        //     IsPlayer = playerDataView.IsPlayer;
        //     
        //     userNameText.text = playerDataView.userNameText.text;
        //     highlightedUserNameText.text = playerDataView.highlightedUserNameText.text;
        //     winPlaceText.text = playerDataView.winPlaceText.text;
        //     
        //     generalPlaceText.text = playerDataView.generalPlaceText.text;
        //     cupPointsText.text = playerDataView.cupPointsText.text;
        //     
        //     if (IsPlayer)
        //     {
        //         plankGreen.SetActive(true);
        //         plankPlayerCupGreen.SetActive(true);
        //         plankPlayerCupNormal.SetActive(false);
        //         plankNormal.SetActive(false);
        //     }
        //     else
        //     {
        //         plankGreen.SetActive(false);
        //         plankPlayerCupGreen.SetActive(false);
        //         plankPlayerCupNormal.SetActive(true);
        //         plankNormal.SetActive(true);
        //     }
        //
        //     // PlacePrizeAssign(playerDataView.Position, rewardCardsCount, rewardInfo, isShowingChest, isShowingJoker);
        //     // todo switch place
        // }

        public void SetData(PlayerDartsView playerDataView, /*int prizePlaces, int topSize,*/ bool isPlayer)
        {
            Position = playerDataView.Position;
            IsPlayer = playerDataView.IsPlayer;

            userNameText.text = playerDataView.userNameText.text;
            highlightedUserNameText.text = playerDataView.highlightedUserNameText.text;
            winPlaceText.text = playerDataView.winPlaceText.text;

            generalPlaceText.text = playerDataView.generalPlaceText.text;
            cupPointsText.text = playerDataView.cupPointsText.text;

            if (isPlayer)
            {
                plankGreen.SetActive(true);
                plankPlayerCupGreen.SetActive(true);
                plankPlayerCupNormal.SetActive(false);
                plankNormal.SetActive(false);
            }
            else
            {
                plankGreen.SetActive(false);
                plankPlayerCupGreen.SetActive(false);
                plankPlayerCupNormal.SetActive(true);
                plankNormal.SetActive(true);
            }

            // todo switch place
        }


        public void SetData(string playerName, int place, int cupPoints, Action onChestPressed, Action onPlayerPressed,
            int rewardCardsCount, RewardInfo rewardInfo, bool isPlayer = false, bool isShowingChest = false, bool? isShowingJoker = false)
        {
            this.Position = place;
            this.IsPlayer = isPlayer;

            chestInfoButton.onClick.RemoveListener(() => onChestPressed?.Invoke());
            chestInfoButton.onClick.AddListener(() => onChestPressed?.Invoke());

            playerInfoButton.onClick.RemoveListener(() => onPlayerPressed?.Invoke());
            playerInfoButton.onClick.AddListener(() => onPlayerPressed?.Invoke());

            userNameText.text = playerName;
            highlightedUserNameText.text = playerName;
            winPlaceText.text = place.ToString();
            generalPlaceText.text = place.ToString();
            cupPointsText.text = cupPoints.ToString();

            if (isPlayer)
            {
                plankGreen.SetActive(true);
                plankPlayerCupGreen.SetActive(true);
                plankPlayerCupNormal.SetActive(false);
                plankNormal.SetActive(false);
            }
            else
            {
                plankGreen.SetActive(false);
                plankPlayerCupGreen.SetActive(false);
                plankPlayerCupNormal.SetActive(true);
                plankNormal.SetActive(true);
            }

            PlacePrizeAssign(place, rewardCardsCount, rewardInfo, isShowingChest, isShowingJoker);
        }

        private GameObject firstPlaceContainer;
        private GameObject secondPlaceContainer;
        private GameObject thirdPlaceContainer;

        private void PlacePrizeAssign(int place, int rewardCardsCount, RewardInfo rewardInfo, bool isShowingChest,
            bool? isShowingJoker)
        {
            firstPlaceContainer = InstantiateScreenPart(firstPlaceContainerPrefab, placeRoot);
            secondPlaceContainer = InstantiateScreenPart(secondPlaceContainerPrefab, placeRoot);
            thirdPlaceContainer = InstantiateScreenPart(thirdPlaceContainerPrefab, placeRoot);

            firstPlaceContainer.SetActive(false);
            secondPlaceContainer.SetActive(false);
            thirdPlaceContainer.SetActive(false);

            switch (place)
            {
                case 1:
                    InstantiateScreenPart(firstPlaceChestPrefab, chestsTransform);
                    firstPlaceContainer.SetActive(true);

                    winPlaceText.gameObject.SetActive(true);
                    winPlaceText.fontMaterial = firstPlaceTmpMaterial;
                    generalPlaceText.gameObject.SetActive(false);

                    if (isShowingJoker != null && (bool)isShowingJoker)
                        InstantiateScreenPart(jokerLabelPrefab, seasonPassLabesRoot);
                    else
                    {
                        if (rewardCardsCount > 0) InstantiateScreenPart(ordinaryLabelPrefab, seasonPassLabesRoot);
                    }
                    break;
                case 2:
                    InstantiateScreenPart(secondPlaceChestPrefab, chestsTransform);
                    secondPlaceContainer.SetActive(true);

                    winPlaceText.gameObject.SetActive(true);
                    winPlaceText.fontMaterial = secondPlaceTmpMaterial;
                    generalPlaceText.gameObject.SetActive(false);
                    if (rewardCardsCount > 0) InstantiateScreenPart(ordinaryLabelPrefab, seasonPassLabesRoot);
                    break;
                case 3:
                    InstantiateScreenPart(thirdPlaceChestPrefab, chestsTransform);
                    thirdPlaceContainer.SetActive(true);

                    winPlaceText.gameObject.SetActive(true);
                    winPlaceText.fontMaterial = thirdPlaceTmpMaterial;

                    generalPlaceText.gameObject.SetActive(false);
                    if (rewardCardsCount > 0) InstantiateScreenPart(ordinaryLabelPrefab, seasonPassLabesRoot);
                    break;
                default:
                    if (isShowingChest)
                    {
                        InstantiateScreenPart(normalChestPrefab, chestsTransform);
                    }
                    else if (rewardInfo.Type != null)
                    {
                        var rewardItemControl = CreateRewardItem(rewardInfo.Type, rewardInfo.Subtype);
                        if (rewardItemControl != null)
                        {
                            SetupRewardControl(rewardItemControl, rewardInfo, null);
                        }
                    }

                    winPlaceText.gameObject.SetActive(false);
                    generalPlaceText.gameObject.SetActive(true);
                    if (rewardCardsCount > 0) InstantiateScreenPart(ordinaryLabelPrefab, seasonPassLabesRoot);
                    break;
            }
        }

        private GameObject InstantiateScreenPart(GameObject placePrefab, Transform root)
        {
            GameObject instance = Instantiate(placePrefab, root, false);
            instance.transform.localPosition = Vector3.zero;

            return instance;
        }

        private int lastPlayerInfoHash = -1;
        public void SetAvatarAndNameStyle(DartsLeaderboardWindow.DartsPlayerData playerData)
        {
            string data = playerData.name + playerData.avatarId + playerData.avatarFrame + playerData.playerNameColor;
            int newDataHash = data.GetHashCode();

            if (newDataHash == lastPlayerInfoHash) return;

            lastPlayerInfoHash = newDataHash;

            Sprite frame = null;
            TMP_ColorGradient playerNameColor = null;

            if (!string.IsNullOrEmpty(playerData.avatarFrame))
            {
                frame = Addressables.LoadAssetAsync<Sprite>(playerData.avatarFrame).WaitForCompletion();
            }

            if (!string.IsNullOrEmpty(playerData.playerNameColor))
            {
                playerNameColor = Addressables.LoadAssetAsync<TMP_ColorGradient>(playerData.playerNameColor).WaitForCompletion();
            }

            AvatarsCacheManager.LoadAvatarAsync(string.IsNullOrEmpty(playerData.avatarId) ? "avatar_0" : playerData.avatarId, (sprite) =>
            {
                DisplayPlayerAvatar(sprite, frame, playerNameColor);
            });

            var avatar = playerData.avatarId;
            if (avatar is { Length: > 30 })
            {
                var avatarName = MainManager.Instance.AvatarsManager.GetAvatarIdByGuid(avatar);

                if (string.IsNullOrEmpty(avatarName) == false)
                {
                    avatar = avatarName;
                }
            }

            MainManager.Instance.AvatarsManager.Loader.TryGetAvatar(avatar, false, (id, sprite) =>
            {
                SetAvatar(sprite);
            });
        }

        private void DisplayPlayerAvatar(Sprite avatarSprite, Sprite avatarFrame, TMP_ColorGradient nameColorGradient)
        {
            playerAvatar.sprite = avatarSprite;
            playerAvatar.preserveAspect = true;

            if (avatarFrame != null)
            {
                this.avatarFrame.sprite = avatarFrame;

                decorFrameGold.gameObject.SetActive(true);
            }
            else
            {
                decorFrameGold.gameObject.SetActive(false);

                // TODO
                // Sprite frameSprite = Addressables.LoadAssetAsync<Sprite>("avatar_frame_common").WaitForCompletion();

                if (frameSprite != null)
                {
                    this.avatarFrame.sprite = frameSprite;
                }
            }

            if (nameColorGradient != null)
            {
                userNameText.color = new Color(1, 1, 1, 1);
                userNameText.colorGradientPreset = nameColorGradient;


                userNameText.fontMaterial.shader = Shader.Find(userNameText.fontMaterial.shader.name);
                userNameText.fontMaterial.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                userNameText.fontMaterial.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.6f);
                userNameText.fontMaterial.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.254902f, 0.0882353f, 0, 1));

            }
            else
            {
                userNameText.colorGradientPreset = null;
                userNameText.color = normalNameColor;

                userNameText.fontMaterial.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0f);
            }
        }

        public Tween Show(float duration, AnimationCurve moveCurve, Action onComplete = null)
        {
            MoveToHiddenPosition();

            return container.DOLocalMove(Vector3.zero, duration).
                OnComplete(() => onComplete?.Invoke())
                .SetEase(moveCurve);
        }

        private void MoveToHiddenPosition()
        {
            container.localPosition = hideAnchor.localPosition;
        }

        private RewardItemControl CreateRewardItem(string type, string subtype)
        {
            DestroyCurrentRewardItem();
            UpdateRewardFactory();
            var control = rewardItemControlFactory.CreateRewardItem(type, subtype, prizeContainer.transform);

            currentRewardItemControl = control;
            return control;
        }

        private RewardItemControl CreateSpecialRewardItem(string specialView)
        {
            DestroyCurrentRewardItem();
            UpdateRewardFactory();
            var control = rewardItemControlFactory.CreateSpecialRewardItem(specialView, prizeContainer.transform);

            currentRewardItemControl = control;
            return control;
        }

        private void SetupRewardControl(RewardItemControl control, RewardInfo rewardInfo, Action<RewardInfo> showMultiplierTooltip)
        {
            var countControl = control as RewardItemCountControl;
            if (countControl != null)
            {
                countControl.drawOneCount = true;
            }
            RewardsUtils.SetupRewardItemControlWithMultiplier(control, rewardInfo, showMultiplierTooltip);
            control.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
        }

        private void DestroyCurrentRewardItem()
        {
            if (currentRewardItemControl)
            {
                Destroy(currentRewardItemControl.gameObject);
                currentRewardItemControl = null;
            }
        }

        private static readonly int riseTrigger = Animator.StringToHash("Rise");
        public Sequence PlayRise(bool isFirstPlace)
        {
            Sequence sequence = DOTween.Sequence();

            sequence.InsertCallback(0f, () =>
            {
                // sideVariants.SelectVariant(isFirstPlace ? 1 : 0);
                // backSideVariants.SelectVariant(isFirstPlace ? 1 : 0);

                lineAnimator.SetEnabledChecked(true);
                lineAnimator.SetTrigger(riseTrigger);
            });

            // sequence.AppendCallback(() => MainManager.Instance.SoundManager.Play(CashCupSoundKeys.BarLift));
            sequence.AppendInterval(1.0f);

            return sequence;
        }

        public void ChangeAnchorToTop()
        {
            containerRectTransform.anchorMin = new Vector2(0f, 1);
            containerRectTransform.anchorMax = new Vector2(1f, 1);
        }

        public void ChangeAnchorToMiddle()
        {
            containerRectTransform.anchorMin = new Vector2(0f, 0.5f);
            containerRectTransform.anchorMax = new Vector2(1f, 0.5f);
        }

        private void UpdateRewardFactory()
        {
            if (rewardItemControlFactory == null)
            {
                rewardItemControlFactory = new RewardItemControlFactory(rewardViewConfig);
            }
        }

        public void SetPlayerName(string name, string color = null)
        {
            userNameText.text = name;
        }

        public void SetBackground(Sprite background)
        {
            avatarFrame.sprite = background;
        }

        public void SetAvatar(Sprite sprite)
        {
            playerAvatar.sprite = sprite;
        }

        public void SetDeco(bool isActive)
        {
            decorFrameGold.gameObject.SetActive(isActive);
        }

        public TMP_Text GetPlayerNameLabel()
        {
            return null;
        }
    }
}
