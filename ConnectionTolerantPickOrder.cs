using RWF.Algorithms;
using System.Collections.Generic;
using System.Linq;

namespace CTD {
    class ConnectionTolerantPickOrder:IPickOrderStrategy {
        private Dictionary<int, List<Player>> playerOrders { get {
                var order = new Dictionary<int, List<Player>>();
                foreach(Player player in PlayerManager.instance.players) {

                    if(!order.ContainsKey(player.teamID)) {
                        order.Add(player.teamID, new List<Player>());
                    }
                    order[player.teamID].Add(player);
                }
                return order;
            } }
        private List<int> teamOrder {
            get {
                return PlayerManager.instance.players.Select(p => p.teamID).Distinct().ToList() ;
            }
        }

        public ConnectionTolerantPickOrder() {
        }


        public void AddPlayer(Player player) {
        }

        public void RefreshOrder(int[] winningTeamIDs) {
        }

        public IEnumerable<Player> GetPlayers(int[] winningTeamIDs) {
            int maxTeamPlayers = this.playerOrders.Max(p => p.Value.Count);

            for(int playerIndex = 0; playerIndex < maxTeamPlayers; playerIndex++) {
                foreach(int teamID in this.teamOrder.Where(id => !winningTeamIDs.Contains(id))) {
                    var playerOrder = this.playerOrders[teamID];
                    if(playerIndex < playerOrder.Count) {
                        yield return playerOrder[playerIndex];
                    }
                }
            }
        }
    }
}