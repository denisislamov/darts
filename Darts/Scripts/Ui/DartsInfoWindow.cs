using System;
using System.Collections.Generic;
using DG.Tweening;
using Dip.Features.Pvp;
using Dip.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace Dip.Features.Darts.Ui
{
    public class DartsInfoWindow : BaseFrame
    {
        private const string StepFourLocalize = "pvpnew.info_4";

        [SerializeField] private Button fade;

        [SerializeField] private List<Transform> showingElements;
        [SerializeField] private float showingElementStartTime;
        [SerializeField] private float showingElementDuration;
        [SerializeField] private Ease showingElementEase;

        [SerializeField] private float fadeOutDuration;
        [SerializeField] private float fadeOutDelay;
        [SerializeField] private Image fadeOutImage;
        [SerializeField] private ScreenTitleLocalize title;
        [SerializeField] private TextMeshProUGUI stepFourText;
        [SerializeField] private List<TextMeshProUGUI> levelsMultipliersTexts;

        public Action OnCloseCallback { get; set; }

        private BaseFrame _parentWindow;
        public BaseFrame ParentWindow
        {
            get => _parentWindow;
            set
            {
                _parentWindow = value;
                SetHud(false);
            }
        }

        private bool isReadyToSkip = true;

        private Sequence sequence;

        public override Action OnCloseRequestedByBackButton
        {
            get
            {
                if (isReadyToSkip)
                {
                    return () => uiManager.Popups.Hide();
                }
                else
                {
                    return null;
                }
            }
        }

        protected override void Awake()
        {
            base.Awake();
            fade.onClick.AddListener(() =>
            {
                if (isReadyToSkip)
                {
                    uiManager.Popups.Hide();
                }

                fade.onClick.RemoveAllListeners();

                base.OnHide();
                SetHud(true);
                sequence?.Kill(true);
                OnCloseCallback?.Invoke();
                OnCloseCallback = null;

                Destroy(gameObject);
            });

            foreach (var showingElement in showingElements)
                showingElement.localScale = Vector3.zero;
        }

        protected override void OnShow()
        {
            base.OnShow();
            SetHud(false);
            isReadyToSkip = false;

            ShowElements();
        }

        [Sirenix.OdinInspector.Button]
        private void ShowElements()
        {
            foreach (var showingElement in showingElements)
                showingElement.localScale = Vector3.zero;
            title.gameObject.SetActive(false);

            sequence = DOTween.Sequence();

            sequence.InsertCallback(0.05f, () => title.gameObject.SetActive(true));

            for (var idx = 0; idx < showingElements.Count; idx++)
            {
                sequence.Insert(showingElementStartTime * idx,
                    showingElements[idx].DOScale(Vector3.one, showingElementDuration).SetEase(showingElementEase, 2.4f));
            }

            sequence.InsertCallback(showingElementStartTime * (showingElements.Count - 1), () => isReadyToSkip = true);

            sequence.SetDelay(fadeOutDelay);

            var cachedColor = fadeOutImage.color;
            fadeOutImage.color = Color.clear;
            fadeOutImage.DOColor(cachedColor, fadeOutDuration).SetDelay(fadeOutDelay);
        }

        // protected override void OnHide()
        // {
        //     base.OnHide();
        //     SetHud(true);
        //     sequence?.Kill(true);
        //     OnCloseCallback?.Invoke();
        //     OnCloseCallback = null;
        // }


        public void SetInfo(DartsFeatureConfig config)
        {
            if (!levelsMultipliersTexts.IsNullOrEmpty() && config.DartsMultipliers != null)
            {
                for (int i = 0; i < config.DartsMultipliers.Length; i++)
                {
                    if (levelsMultipliersTexts.Count > i)
                    {
                        levelsMultipliersTexts[i].text = string.Format("x{0}", config.DartsMultipliers[i]);
                    }
                }
            }
        }

        private void SetHud(bool isEnabled, float time = 0.1f)
        {
            var frames = new[]
            {
                UiManager.Instance.GetFrame(Dip.Ui.Constants.Windows.MetaScreen),
                UiManager.Instance.GetFrame(Dip.Ui.Constants.Windows.MetaHudScreen),
                ParentWindow,
            };

            foreach (BaseFrame frame in frames)
            {
                if (frame != null)
                {
                    CanvasGroup canvasGroup = frame.GetComponent<CanvasGroup>();

                    if (canvasGroup != null && time > 0)
                    {
                        float fadeValue = isEnabled ? 1f : 0f;
                        canvasGroup.DOFade(fadeValue, time);
                    }
                    else
                    {
                        frame.GetComponent<Canvas>().enabled = isEnabled;
                    }
                }
            }
        }
    }
}
