using System.Collections.Generic;
using System.Linq;
using Multi.MahjongMessages;
using Multi.ServerData;
using Single;
using Single.MahjongDataType;
using StateMachine.Interfaces;
using UnityEngine;
using UnityEngine.Networking;

namespace Multi.GameState
{
    public class PlayerRongState : IState
    {
        public int CurrentPlayerIndex;
        public ServerRoundStatus CurrentRoundStatus;
        public int[] RongPlayerIndices;
        public Tile WinningTile;
        public MahjongSet MahjongSet;
        public PointInfo[] RongPointInfos;
        private GameSettings gameSettings;
        private IList<Player> players;
        private IList<PointTransfer> transfers;
        private bool[] responds;
        private float serverTimeOut;
        private float firstTime;

        public void OnStateEnter()
        {
            Debug.Log($"Server enters {GetType().Name}");
            gameSettings = CurrentRoundStatus.GameSettings;
            players = CurrentRoundStatus.Players;
            NetworkServer.RegisterHandler(MessageIds.ClientReadinessMessage, OnReadinessMessageReceived);
            var playerNames = RongPlayerIndices.Select(
                playerIndex => players[playerIndex].PlayerName
            ).ToArray();
            var handData = RongPlayerIndices.Select(
                playerIndex => CurrentRoundStatus.HandData(playerIndex)
            ).ToArray();
            var richiStatus = RongPlayerIndices.Select(
                playerIndex => CurrentRoundStatus.RichiStatus(playerIndex)
            ).ToArray();
            var multipliers = RongPlayerIndices.Select(
                playerIndex => gameSettings.GetMultiplier(CurrentRoundStatus.IsDealer(playerIndex), players.Count)
            ).ToArray();
            var totalPoints = RongPointInfos.Select((info, i) => info.BasePoint * multipliers[i]).ToArray();
            var netInfos = RongPointInfos.Select(info => new NetworkPointInfo
            {
                Fu = info.Fu,
                YakuValues = info.YakuList.ToArray(),
                Dora = info.Dora,
                UraDora = info.UraDora,
                RedDora = info.RedDora,
                IsQTJ = info.IsQTJ
            }).ToArray();
            Debug.Log($"The following players are claiming rong: {string.Join(",", RongPlayerIndices)}, "
                + $"PlayerNames: {string.Join(",", playerNames)}");
            var rongMessage = new ServerPlayerRongMessage
            {
                RongPlayerIndices = RongPlayerIndices,
                RongPlayerNames = playerNames,
                HandData = handData,
                WinningTile = WinningTile,
                DoraIndicators = MahjongSet.DoraIndicators,
                UraDoraIndicators = MahjongSet.UraDoraIndicators,
                RongPlayerRichiStatus = richiStatus,
                RongPointInfos = netInfos,
                TotalPoints = totalPoints
            };
            for (int i = 0; i < players.Count; i++)
            {
                players[i].connectionToClient.Send(MessageIds.ServerRongMessage, rongMessage);
            }
            // get point transfers
            transfers = new List<PointTransfer>();
            for (int i = 0; i < RongPlayerIndices.Length; i++)
            {
                var rongPlayerIndex = RongPlayerIndices[i];
                var point = RongPointInfos[i];
                var multiplier = multipliers[i];
                int pointValue = point.BasePoint * multiplier;
                int extraPoints = i == 0 ? CurrentRoundStatus.ExtraPoints * (players.Count - 1) : 0;
                transfers.Add(new PointTransfer
                {
                    From = CurrentPlayerIndex,
                    To = rongPlayerIndex,
                    Amount = pointValue + extraPoints
                });
            }
            // richi-sticks-points
            transfers.Add(new PointTransfer {
                From = -1,
                To = RongPlayerIndices[0],
                Amount = CurrentRoundStatus.RichiSticksPoints
            });
            responds = new bool[players.Count];
            // determine server time out
            serverTimeOut = MahjongConstants.SummaryPanelDelayTime * RongPointInfos.Sum(point => point.YakuList.Count)
                + MahjongConstants.SummaryPanelWaitingTime * RongPointInfos.Length
                + ServerConstants.ServerTimeBuffer;
            firstTime = Time.time;
        }

        private void OnReadinessMessageReceived(NetworkMessage message)
        {
            var content = message.ReadMessage<ClientReadinessMessage>();
            Debug.Log($"[Server] Received ClientReadinessMessage: {content}");
            if (content.Content != MessageIds.ServerPointTransferMessage)
            {
                Debug.LogError("The message contains invalid content.");
                return;
            }
            responds[content.PlayerIndex] = true;
        }

        public void OnStateExit()
        {
            Debug.Log($"Server exits {GetType().Name}");
            NetworkServer.UnregisterHandler(MessageIds.ClientReadinessMessage);
        }

        public void OnStateUpdate()
        {
            if (responds.All(r => r))
            {
                ServerBehaviour.Instance.PointTransfer(transfers);
                return;
            }
            if (Time.time - firstTime > serverTimeOut)
            {
                ServerBehaviour.Instance.PointTransfer(transfers);
                return;
            }
        }
    }
}