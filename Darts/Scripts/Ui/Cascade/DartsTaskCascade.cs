using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

using DG.Tweening;
using Dip.Ui;
using Dip.Ui.Rewards;

namespace Dip.Features.Darts.Ui
{
    public class DartsTaskCascade : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text slotNumberText;

        [SerializeField]
        private TMP_Text slotNumberTextAdditional;
        
        [SerializeField]
        private GameObject slotIcon;
        
        [SerializeField]
        private GameObject slotCompleteIcon;
        
        [SerializeField]
        private GameObject slotFxIcon;
        
        [SerializeField]
        private GameObject slotLockIcon;

        [SerializeField] 
        private GameObject prizeContainer;
        
        [SerializeField] 
        private GameObject completeIcon;
        
        [SerializeField] 
        private GameObject topLine;
        
        [SerializeField] 
        private GameObject topLineAdditional;
        
        [SerializeField] 
        private GameObject bottomLine;

        [SerializeField]
        private Button button;

        [SerializeField]
        private RewardViewConfig rewardViewConfig;

        [SerializeField] 
        private RectTransform rectTransform;
        
        [Header("Progress Bar")]
        [SerializeField] 
        private PaddingProgressBar progressBar;
        [SerializeField]
        private float progressBarDuration = 0.5f;

        private RewardItemControl rewardItem;
        private RewardItemControl additionRewardItem;
        private RewardItemControlFactory rewardItemControlFactory;

        private int currentSlotNumber;

        private Sequence lockAnimation;
        
        public DartsTaskCascade()
        {
            bottomLine = default;
            prizeContainer = default;
        }

        public UnityEvent Clicked { get; } = new UnityEvent();

        public RectTransform RectTransform => rectTransform;

        public Transform RewardTransform => prizeContainer.transform;

        public Vector2 RewardPosition
        {
            get => RewardTransform.position;
        }

        private void Awake()
        {
            if (button)
            {
                button.onClick.AddListener(PlayLockAnimation);
                button.onClick.AddListener(Clicked.Invoke);
                button.onClick.AddListener(() => UiManager.Instance.SoundManager.Play("NotificationPopupShow"));
            }

            rewardItemControlFactory = new RewardItemControlFactory(rewardViewConfig);
        }

        protected void OnDestroy()
        {
            DestroyRewardItem();
        }

        public void SetSlotNumber(int number)
        {
            if (currentSlotNumber == number)
                return;

            currentSlotNumber = number;

            if (slotNumberText)
            {
                slotNumberText.text = number.ToString();
            }
            
            if (slotNumberTextAdditional)
            {
                slotNumberTextAdditional.text = number.ToString();
            }
        }

        public void SetActiveLines(bool isActiveTopLine,bool isActiveTopLineAdditional, bool isActiveBottomLine)
        {
            if (topLine)
            {
                topLine.SetActive(isActiveTopLine);
            }

            if (topLineAdditional)
            {
                topLineAdditional.SetActive(isActiveTopLineAdditional);
            }

            if (bottomLine)
            {
                bottomLine.SetActive(isActiveBottomLine);
            }
        }

        public void SetCompleted(int animationIndex, bool needAnimate)
        {
            DestroyRewardItem();
            DestroyAdditionalRewardItem();

            completeIcon.SetActive(true);
            slotCompleteIcon.SetActive(true);
            slotFxIcon.SetActive(false);
            slotIcon.SetActive(false);
            slotLockIcon.SetActive(false);
            prizeContainer.SetActive(false);
            progressBar.Value = 1f;

            if (needAnimate)
            {
                progressBar.Value = 0f;
                var animation = DOTween.Sequence();
                animation.AppendInterval(progressBarDuration * animationIndex);
                animation.Append(DOFillPaddingBar(0f, 1f, progressBarDuration));
            }
        }

        private Tweener DOFillPaddingBar(float startValue, float endValue, float duration)
        {
            progressBar.Value = startValue;
            return DOTween.To(() => progressBar.Value, v => progressBar.Value = v, endValue, duration);
        }

        public void SetActive(int index, float progress)
        {
            completeIcon.SetActive(false);
            slotCompleteIcon.SetActive(false);
            slotIcon.SetActive(true);
            prizeContainer.SetActive(true);

            slotFxIcon.SetActive(true);
            slotLockIcon.SetActive(false);

            if (progress < 1f)
            {
                progressBar.Value = 0f;
            }
            else
            {
                progressBar.Value = 1f;
            }
            // TODO: Unkomment if need show progress bar of current step ;)
            //progressBar.Value = progress;
            //if (needAnimate)
            //{
            //    var animation = DOTween.Sequence();
            //    animation.AppendInterval(progressBarDuration * index);
            //    animation.Append(DOFillPaddingBar(0f, progress, progressBarDuration * progress));
            //}
        }

        public void SetLocked()
        {
            completeIcon.SetActive(false);
            slotCompleteIcon.SetActive(false);
            slotIcon.SetActive(true);
            prizeContainer.SetActive(true);

            slotFxIcon.SetActive(false);
            slotLockIcon.SetActive(true);
            progressBar.Value = 0f;
        }

        public void SetInteractableButton(bool isInteractable)
        {
            if (button)
            {
                button.interactable = isInteractable;
            }
        }

        public RewardItemControl CreateRewardItem(string type, string subtype, int place)
        {
            DestroyRewardItem();
            
            rewardItem = rewardItemControlFactory.CreateRewardItem(type, subtype, prizeContainer.transform);
            return rewardItem;
        }
        
        public RewardItemControl CreateSpecialRewardItem(string specialView)
        {
            DestroyRewardItem();
            rewardItem = rewardItemControlFactory.CreateSpecialRewardItem(specialView, prizeContainer.transform);
            return rewardItem;
        }

        public void DestroyRewardItem()
        {
            if (rewardItem)
            {
                Destroy(rewardItem.gameObject);
                rewardItem = null;
            }
        }

        public RewardItemControl CreateAdditionalRewardItem(string type, string subtype)
        {
            DestroyAdditionalRewardItem();
            additionRewardItem = rewardItemControlFactory.CreateRewardItem(type, subtype, prizeContainer.transform);
            return additionRewardItem;
        }

        public void DestroyAdditionalRewardItem()
        {
            if (additionRewardItem)
            {
                Destroy(additionRewardItem.gameObject);
                additionRewardItem = null;
            }
        }

        private void PlayLockAnimation()
        {
            if (lockAnimation != null && lockAnimation.IsPlaying())
            {
                return;
            }

            if (lockAnimation != null)
            {
                lockAnimation.Kill();
                lockAnimation = null;
            }

            if (slotLockIcon.activeInHierarchy)
            {
                lockAnimation = DOTween.Sequence();
                lockAnimation.Append(slotLockIcon.transform.DOPunchRotation(new Vector3(0, 0, 20.0f), 0.4f));
                lockAnimation.OnComplete(() => slotLockIcon.transform.localRotation = Quaternion.identity);
            }
        }
    }
}