using System;
using System.Collections.Generic;
using Dip.Storable;
using Newtonsoft.Json;

namespace Dip.Features.Darts
{
    public enum DartsState
    {
        Rest = 0,
        Announce = 1,
        PreStarted = 2,
        InProgress = 3,
        Completed = 4
    }

    public class DartsFeatureStorage : StorageItem
    {
        [JsonProperty]
        public DateTime EndTime { get; set; }

        [JsonProperty]
        public DateTime StartTime { get; set; }

        [JsonProperty]
        public DartsState StateType { get; set; }

        [JsonProperty]
        public int LastScore { get; set; }

        [JsonProperty]
        public int LastMultipliersProgress { get; set; }

        [JsonProperty]
        public int MultipliersProgress { get; set; }

        [JsonProperty]
        public List<int> WatchedMultipliers { get; set; }

        [JsonProperty]
        public int PointsProgress { get; set; }

        [JsonProperty]
        public int LastPointsProgress { get; set; }

        [JsonProperty]
        public int LevelsRewardsReceived { get; set; }

        [JsonProperty]
        public int DartsId { get; set; }

        [JsonProperty]
        public bool IsLocked { get; set; }

        [JsonProperty]
        public bool IsFinalRewardReceived { get; set; }

        [JsonProperty]
        public int PreviousPosition { get; set; }

        [JsonProperty]
        public bool IsStartWindowsShown { get; set; }

        public const string StorageTypeName = "Darts";
        public const int StorageVersion = 1;


        public DartsFeatureStorage() : base(StorageTypeName, StorageVersion)
        {

        }
    }
}
