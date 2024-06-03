using System;
using Dip.AbTest.FeaturesBase;
using Dip.Configs;
using Dip.Configs.Features;
using Dip.Features.Core.Triggers;
using Dip.Rewards;
using Dip.Ui;
using Newtonsoft.Json.Utilities;
using PGOnline.RemoteConfigs;
using UnityEngine;


namespace Dip.Features.Darts
{
    [Serializable, CreateAssetMenu(menuName = "Features/Darts/Config")]
    public class DartsFeatureConfig : RemoteConfigForControllerWithInstance<DartsFeatureConfig>, IUnlockableFeature
    {
        [Serializable]
        public class PackRewardInfo
        {
            public string PackRewardViewId;
            public RewardInfo[] RewardInfos;

            public PackRewardInfo(string packRewardViewId, RewardInfo[] rewardInfos)
            {
                PackRewardViewId = packRewardViewId;
                RewardInfos = rewardInfos;
            }
        }


        [Serializable]
        public class DartsLevel
        {
            public int Points;
            public PackRewardInfo PackRewardInfo;
        }

        [field: SerializeField]
        [RemoteProperty, FeaturePriorityMember(Key = "ApplyReward")]
        public int ApplyRewardPriority { get; private set; }

        [field: SerializeField]
        [RemoteProperty, FeaturePriorityMember(Key = nameof(ShowPriority))]
        public int ShowPriority { get; private set; }

        [field: SerializeField]
        [RemoteProperty]
        public int EndLevelAnimationOrder { get; set; }

        [field: SerializeField]
        [RemoteProperty]
        public TriggerData UnlockTriggerData { get; set; }

        public ConfigAssetReference ItemToShield;

        [field: SerializeField]
        [RemoteProperty]
        public FeatureMetaWidgetConfig MetaWidgetConfig { get; private set; }

        [SerializeField]
        [RemoteProperty]
        public string LeaderboardId;

        [field: SerializeField]
        [RemoteProperty]
        public PackRewardInfo[] CompleteEventRewardInfos { get; set; }

        [field: SerializeField]
        [RemoteProperty]
        public int[] DartsMultipliers { get; set; }

        [field: SerializeField]
        [RemoteProperty]
        public DartsLevel[] DartsLevels { get; set; }

        [field: SerializeField]
        [RemoteProperty]
        public int LoseInfoPriority { get; private set; }

        [field: SerializeField]
        [RemoteProperty]
        public ConfigAssetReference LoseRewardPrefab { get; private set; }

        [field: SerializeField]
        [RemoteProperty]
        public Color LoseRewardItemTextColor { get; private set; }

        [field: SerializeField]
        [RemoteProperty]
        public string TitleLocalizationKey { get; private set; }

        [SerializeField]
        [RemoteProperty]
        private ConfigSpriteAtlasReference[] spriteAtlasRefs;

        public ConfigSpriteAtlasReference[] SpriteAtlasRefs => spriteAtlasRefs;

        [SourceLocation]
        public DartsFeatureConfig()
        {
            AotHelper.EnsureList<RewardInfo>();
            AotHelper.EnsureList<DartsLevel>();
            AotHelper.EnsureList<ConfigSpriteAtlasReference>();
        }
    }
}
