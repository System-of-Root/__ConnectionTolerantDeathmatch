using RWF;
using RWF.GameModes;
using System.Collections.Generic;
using System.Linq;
using UnboundLib;

namespace CTD {
    public class ConnectionTolerantDeathmatchHandler:RWFGameModeHandler<ConnectionTolerantDeathmatch> {
        internal const string GameModeName = "ConnectionTolerantDeathmatch";
        internal const string GameModeID = "ConnectionTolerantDeathmatch";
        public override bool OnlineOnly => true;
        public ConnectionTolerantDeathmatchHandler() : base(
            name: GameModeName,
            gameModeId: GameModeID,
            allowTeams: false,
            pointsToWinRound: 2,
            roundsToWinGame: 3,
            // null values mean RWF's instance values
            playersRequiredToStartGame: null,
            maxPlayers: null,
            maxTeams: null,
            maxClients: null,
            description: "Like Deathmatch, but people <i>Should</i> be able to leave/late join",
            videoURL: "https://github.com/olavim/RoundsWithFriends/raw/main/Media/Deathmatch.mp4") {


        }

        // Mostly code from RWF/Unbound
        public override void PlayerLeft(Player leftPlayer) {
            // store old teamIDs so that we can make a dictionary of old to new teamIDs
            Dictionary<Player, int> oldTeamIDs = PlayerManager.instance.players.ToDictionary(p => p, p => p.teamID);

            // UnboundLib handles PlayerManager fixing, which includes reassigning playerIDs and teamIDs
            // as well as card bar fixing
            List<Player> remainingPlayers = PlayerManager.instance.players.Where(p => p != leftPlayer).ToList();
            int playersAlive = remainingPlayers.Count(p => !p.data.dead);

            if(!leftPlayer.data.dead) {
                try {
                    PlayerDied(leftPlayer, playersAlive);
                } catch {
                    // ignored
                }
            }

            // get new playerIDs
            Dictionary<Player, int> newPlayerIDs = new Dictionary<Player, int>();
            int playerID = 0;
            foreach(Player player in remainingPlayers.OrderBy(p => p.playerID)) {
                newPlayerIDs[player] = playerID;
                playerID++;
            }

            // fix cardbars by reassigning CardBarHandler.cardBars
            // this leaves the disconnected player(s)' bar unchanged, since removing it can cause issues with other mods
            List<CardBar> cardBars = ((CardBar[])CardBarHandler.instance.GetFieldValue("cardBars")).ToList();
            List<CardBar> newCardBars = new List<CardBar>();
            newCardBars.AddRange(
                from p in newPlayerIDs.Keys
                orderby newPlayerIDs[p]
                select cardBars[p.playerID]
            );
            CardBarHandler.instance.SetFieldValue("cardBars", newCardBars.ToArray());

            // reassign playerIDs
            foreach(Player player in newPlayerIDs.Keys) {
                player.AssignPlayerID(newPlayerIDs[player]);
            }

            // reassign teamIDs
            Dictionary<int, List<Player>> teams = new Dictionary<int, List<Player>>();
            foreach(Player player in remainingPlayers.OrderBy(p => p.teamID).ThenBy(p => p.playerID)) {
                if(!teams.ContainsKey(player.teamID)) { teams[player.teamID] = new List<Player>() { }; }

                teams[player.teamID].Add(player);
            }

            int teamID = 0;
            foreach(int oldID in teams.Keys) {
                foreach(Player player in teams[oldID]) {
                    player.AssignTeamID(teamID);
                }
                teamID++;
            }

            PlayerManager.instance.players = remainingPlayers.ToList();

            // count number of unique teams remaining, if equal to 1, the game is borked, wait for reconects
            if(PlayerManager.instance.players.Select(p => p.teamID).Distinct().Count() <= 1) {
                GameMode.OnePlayerLeft();
            }

            // get new teamIDs
            Dictionary<Player, int> newTeamIDs = PlayerManager.instance.players.ToDictionary(p => p, p => p.teamID);

            // update team scores
            Dictionary<int, int> newTeamPoints = new Dictionary<int, int>() { };
            Dictionary<int, int> newTeamRounds = new Dictionary<int, int>() { };

            foreach(Player player in newTeamIDs.Keys) {
                if(!newTeamPoints.Keys.Contains(newTeamIDs[player])) {
                    newTeamPoints[newTeamIDs[player]] = this.GameMode.teamPoints[oldTeamIDs[player]];
                }
                if(!newTeamRounds.Keys.Contains(newTeamIDs[player])) {
                    newTeamRounds[newTeamIDs[player]] = this.GameMode.teamRounds[oldTeamIDs[player]];
                }
            }

            this.GameMode.teamPoints = newTeamPoints;
            this.GameMode.teamRounds = newTeamRounds;

            // fix score counter
            UIHandler.instance.roundCounter.GetData().teamPoints = newTeamPoints;
            UIHandler.instance.roundCounter.GetData().teamRounds = newTeamRounds;
            UIHandler.instance.roundCounterSmall.GetData().teamPoints = newTeamPoints;
            UIHandler.instance.roundCounterSmall.GetData().teamRounds = newTeamRounds;


            CardBarHandlerExtensions.Rebuild(CardBarHandler.instance);
        }

    }
}

