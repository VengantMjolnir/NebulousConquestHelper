using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace NebulousConquestHelper
{
    [XmlType("TeamInfo")]
    [Serializable]
    public class TeamInfo
    {
        public string ShortName;
        public string LongName;

        [XmlIgnore]
        public Dictionary<ulong, PlayerInfo> RegisteredPlayers;

        public List<OrderInfo> ActiveOrders;
        public List<OrderInfo> CompletedOrders;
        public List<PlayerInfo> SerializedPlayers;

        public TeamInfo()
        {
            RegisteredPlayers = new Dictionary<ulong, PlayerInfo>();
            ActiveOrders = new List<OrderInfo>();
            CompletedOrders = new List<OrderInfo>();
        }

        public void PostLoadInit()
        {
            foreach(PlayerInfo player in SerializedPlayers)
            {
                RegisteredPlayers[player.DiscordId] = player;
            }
        }

        public void RegisterPlayer(string name, ulong id, string rank)
        {
            PlayerInfo player = new PlayerInfo()
            {
                DiscordId = id,
                Name = name,
                Rank = rank
            };
            SerializedPlayers.Add(player);
            RegisteredPlayers[id] = player;
        }
    }

    [XmlType("PlayerInfo")]
    [Serializable]
    public class PlayerInfo
    {
        public string Name { get; set; }
        public ulong DiscordId { get; set; }
        public string Rank { get; set; }
    }
}
