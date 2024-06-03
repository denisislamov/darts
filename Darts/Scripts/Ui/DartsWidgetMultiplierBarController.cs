using UnityEngine;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Collections;

namespace Dip.Features.Darts.Ui
{
    public class DartsWidgetMultiplierBarController
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
        }

        public void PlayAnimation(Action callback)
        {
            if (saveData.LastMultipliersProgress != saveData.MultipliersProgress)
            {
                dartsWidgetController.DartsWidgetMultiplierBar.PlayAnimation(config.DartsMultipliers[saveData.LastMultipliersProgress], config.DartsMultipliers[saveData.MultipliersProgress], callback);
            }
            else
            {
                callback?.Invoke();
            }
        }
    }
}
