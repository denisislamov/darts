using System.Collections.Generic;
using Dip.Features.ChooseAvatar;
using Dip.Rewards;
using Dip.Ui;
using UnityEngine;

namespace Dip.Features.Darts.Ui
{
    public class PlayerDartsViewContainer : MonoBehaviour
    {
        [SerializeField] private PlayerDartsView playerViewPrefab;
        [SerializeField] private List<PlayerDartsView> widgetsPool;

        private readonly List<PlayerDartsView> playerViews = new();
        private readonly List<DartsLeaderboardWindow.DartsPlayerData> playerData = new();
        private readonly List<RewardInfo> rewards = new();

        public List<PlayerDartsView> PlayerViews => playerViews;
        public List<DartsLeaderboardWindow.DartsPlayerData> PlayerData => playerData;
        public List<RewardInfo> Rewards => rewards;

        public PlayerDartsView AddView(DartsLeaderboardWindow.DartsPlayerData playerData, RewardInfo rewardInfo)
        {
            PlayerDartsView playerView;
            if (widgetsPool.Find(x => !x.gameObject.activeInHierarchy) is PlayerDartsView freeWidget)
            {
                playerView = freeWidget;
            }
            else
            {
                playerView = Instantiate(playerViewPrefab, transform);
                widgetsPool.Add(playerView);
                
                int index = playerViews.IndexOf(playerView);
                if (index < 0)
                {
                    playerViews.Add(playerView);
                    this.playerData.Add(playerData);
                    rewards.Add(rewardInfo);
                }
                else
                {
                    playerViews[index] = playerView;
                    this.playerData[index] = playerData;
                    rewards[index] = rewardInfo;
                }
            }
            
            playerView.gameObject.SetActive(true);
            playerView.transform.SetAsLastSibling();

            playerView.SetData(playerData.name, playerData.place, playerData.cupPoints, playerData.OnChestPressed,
                playerData.OnPlayerPressed,
                playerData.rewardsCardsCount, rewardInfo, playerData.isPlayer, playerData.isShowingChest,
                playerData.isShowingJoker);

            // PlayerWidgetHelper.DecorateCommon(playerView, IPlayerWidget.PlacementSize.Common,
            //     playerData.name, playerData.avatarId, false);
            
            playerView.SetAvatarAndNameStyle(playerData);
            
            return playerView;
        }

        public void ClearData()
        {
            foreach (PlayerDartsView playerView in playerViews)
            {
                playerView.gameObject.SetActive(false);
            }

            playerViews.Clear();
            playerData.Clear();
            rewards.Clear();
        }
    }
}