using Dip.Storable;

namespace Dip.Features.Darts
{
    public static class DartsFeatureSaveController
    {
        public static DartsFeatureStorage SaveData;

        public static void MarkForSaving()
        {
            Storage.MarkForSaving<DartsFeatureStorage>();
        }
    }
}
