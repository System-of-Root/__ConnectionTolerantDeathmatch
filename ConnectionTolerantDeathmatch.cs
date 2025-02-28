using Photon.Pun;
using RWF;
using RWF.GameModes;
using RWF.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnboundLib;
using UnboundLib.Extensions;
using UnboundLib.GameModes;
using UnboundLib.Networking;
using UnityEngine;

namespace CTD {
    public class ConnectionTolerantDeathmatch: RWFGameMode {

        public const string HookLateJoinStart = "LATE_JOIN_START";
        public const string HookLateJoinEnd = "LATE_JOIN_END";

        public enum ConectionStatusType {
            Conecting,
            GameStart,
            Waiting,
            MidJoin,
            Joined
        }
        public enum GameState {
            Initializing,
            GameStart,
            Pickphase,
            Battlephase,
            Ongoing,
            LateJoinphase
        }

        public static ConnectionTolerantDeathmatch instance;

        public GameState gameState = GameState.Initializing;

        public ConectionStatusType statusType;

        public Dictionary<int, int> HeartBeatList = new Dictionary<int, int>();

        public List<int> WaitingClients = new List<int>();

        public List<int> JoinningClients = new List<int>();

        public int lastBeat = -1;

        public List<Player> UnloadPlayers = new List<Player>();

        public void MasterSwitched(Photon.Realtime.Player newMaster) {
            RWF.CardBarHandlerExtensions.Rebuild(CardBarHandler.instance);
            UIHandler.instance.DisplayRoundStartText("Host Disconected, Restarting round with new host.");
            this.StopAllCoroutines();
            if(statusType != ConectionStatusType.Joined)
                NetworkingManager.RPC(typeof(ConnectionTolerantDeathmatch), nameof(RequestConection), PhotonNetwork.LocalPlayer.ActorNumber);
            this.StartCoroutine(this.DoRoundStart());
        }

        protected override void Awake() {
            instance = this;
            base.Awake();
        }

        protected override void Start() {
            base.Start();
            gameState = GameState.Initializing;
        }

        public void RquestLateJoin() {
            UnityEngine.Debug.Log("Requesting late join");
            PhotonNetwork.Instantiate(PlayerAssigner.instance.playerPrefab.name, Vector3.zero, Quaternion.identity, 0, new object[] { LatePlayerHandler.LateJoinPlayerString });
            NetworkingManager.RPC(typeof(ConnectionTolerantDeathmatch), nameof(RequestConection), PhotonNetwork.LocalPlayer.ActorNumber);
            var cards = FindObjectsOfType<CardInfo>().Where(c => c.gameObject.scene.buildIndex != -1).ToList();
            foreach(var card in cards) {
                UnityEngine.Debug.Log(card);
                Destroy(card.gameObject);
            }
        }

        protected virtual void Update() {
            if(statusType == ConectionStatusType.MidJoin) {
                NetworkingManager.RPC(typeof(ConnectionTolerantDeathmatch), nameof(RequestID), PhotonNetwork.LocalPlayer.ActorNumber);
                statusType = ConectionStatusType.Joined;
            }
            if(!PhotonNetwork.IsMasterClient) return;
            int time = (int)Time.unscaledTime;
            if(time > lastBeat + 5) {
                lastBeat = time;
                NetworkingManager.RPC(typeof(ConnectionTolerantDeathmatch), nameof(HeartbeatRequest), time);
                foreach(int id in HeartBeatList.Keys) {
                    if(HeartBeatList[id] != -1 && time > HeartBeatList[id] + 60 && PhotonNetwork.CurrentRoom.Players.ContainsKey(id)) {
                        PhotonNetwork.CloseConnection(PhotonNetwork.CurrentRoom.Players[id]);
                    }
                }
            }
            if(gameState == GameState.Initializing && statusType == ConectionStatusType.GameStart && WaitingClients.Count == 0) {
                gameState = GameState.GameStart;
                NetworkingManager.RPC_Others(typeof(ConnectionTolerantDeathmatch), nameof(TriggerStart));
            }

        }

        public static void LoadNewPlayer(int ActorNumber, int TeamID, int PlayerID, int ColorID) {

            UnityEngine.Debug.Log($"Loading Player {ActorNumber}, {TeamID}:{PlayerID}  ({ColorID})");
            Player player = instance.UnloadPlayers.Find(p => p.data.view.ControllerActorNr == ActorNumber);
            player.teamID = TeamID;
            player.playerID = PlayerID;
            player.AssignColorID(ColorID);
            PlayerManager.RegisterPlayer(player);
        }

        public static void RequestID(int ActorNumber) {
            UnityEngine.Debug.Log($"ID request {ActorNumber}");
            if(!PhotonNetwork.IsMasterClient) return;
            int playerID = PlayerManager.instance.players.Count;
            int teamID = PlayerManager.instance.players.Count;
            int colorID = Enumerable.Range(0, RWFMod.MaxColorsHardLimit).Except(PlayerManager.instance.players.Select(p => p.colorID()).Distinct()).FirstOrDefault();
            LoadNewPlayer(ActorNumber, teamID, playerID, colorID);
            NetworkingManager.RPC_Others(typeof(ConnectionTolerantDeathmatch), nameof(LoadNewPlayer), ActorNumber, teamID, playerID, colorID);
        }


        [UnboundRPC]
        public static void TriggerStart() {
            UnityEngine.Debug.Log($"Starting Game");
            instance.gameState = GameState.GameStart;
        }
        [UnboundRPC]
        public static void TriggerOngoing() {
            instance.gameState = GameState.Ongoing;
        }

       [UnboundRPC]
        public static void RequestConection(int requestingPlayer) {
            UnityEngine.Debug.Log($"Request Conection {requestingPlayer}");
            if(!PhotonNetwork.IsMasterClient) return;
            if(!instance.WaitingClients.Contains(requestingPlayer))
                instance.WaitingClients.Add(requestingPlayer);
            switch(instance.gameState) {
                case GameState.Initializing:
                    NetworkingManager.RPC(typeof(ConnectionTolerantDeathmatch), nameof(ConnectionResponce),requestingPlayer,(int)ConectionStatusType.GameStart);
                    instance.WaitingClients.Remove(requestingPlayer);
                    break;
                case GameState.GameStart:
                case GameState.Pickphase:
                case GameState.Battlephase:
                case GameState.Ongoing:
                    NetworkingManager.RPC(typeof(ConnectionTolerantDeathmatch), nameof(ConnectionResponce), requestingPlayer, (int)ConectionStatusType.Waiting);
                    break;
                case GameState.LateJoinphase:
                    NetworkingManager.RPC(typeof(ConnectionTolerantDeathmatch), nameof(ConnectionResponce), requestingPlayer, (int)ConectionStatusType.MidJoin);
                    break;
            }
        }

        [UnboundRPC]
        public static void ConnectionResponce(int ActorId, int responce) {
            UnityEngine.Debug.Log($"Request Responce {ActorId} {responce}");
            if(PhotonNetwork.LocalPlayer.ActorNumber != ActorId) return;
            instance.statusType = (ConectionStatusType)responce;
        }

        [UnboundRPC]
        public static void HeartbeatRequest(int data) {
            UnityEngine.Debug.Log($"Heartbeat {data}");
            NetworkingManager.RPC(typeof(ConnectionTolerantDeathmatch), nameof(HeartbeatResponce), PhotonNetwork.LocalPlayer.ActorNumber, data);
        }


        [UnboundRPC]
        public static void HeartbeatResponce(int ActorId, int data) {
            UnityEngine.Debug.Log($"Heartbeat Responce {ActorId}, {data}");
            instance.HeartBeatList[ActorId] = data;
        }



        public void OnePlayerLeft() {

        }

        public override void StartGame() {
            UnityEngine.Debug.Log("GAME START");
            if(PhotonNetwork.IsMasterClient) {
                PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { "active", true } });
            }
            base.StartGame();
            PlayerManager.instance.GetAdditionalData().pickOrder.strategy = new ConnectionTolerantPickOrder();
        }

        public override IEnumerator DoStartGame() {
            UnityEngine.Debug.Log("DO GAME START");
            if(!PhotonNetwork.IsMasterClient) {
                statusType = ConectionStatusType.Conecting;
                NetworkingManager.RPC(typeof(ConnectionTolerantDeathmatch), nameof(RequestConection), PhotonNetwork.LocalPlayer.ActorNumber);
            } else {
                foreach(Player player in PlayerManager.instance.players) {
                    if(!player.data.view.IsMine && !instance.WaitingClients.Contains(player.data.view.ControllerActorNr))
                        instance.WaitingClients.Add(player.data.view.ControllerActorNr);
                }
                statusType = ConectionStatusType.GameStart;
            }

            yield return new WaitUntil(() => statusType != ConectionStatusType.Conecting);

            UnityEngine.Debug.Log("GAME START Conection: "+statusType.ToString());
            if(statusType != ConectionStatusType.GameStart) yield break;
            statusType = ConectionStatusType.Joined;

            new WaitUntil(() => gameState == GameState.GameStart);
            if(!PlayerManager.instance.players.Any(p=>p.data.view.AmOwner))
                NetworkingManager.RPC(typeof(ConnectionTolerantDeathmatch), nameof(RequestID), PhotonNetwork.LocalPlayer.ActorNumber);
            RWF.CardBarHandlerExtensions.Rebuild(CardBarHandler.instance);
            UIHandler.instance.InvokeMethod("SetNumberOfRounds", (int)GameModeManager.CurrentHandler.Settings["roundsToWinGame"]);
            ArtHandler.instance.NextArt();

            yield return GameModeManager.TriggerHook(GameModeHooks.HookGameStart);
            if(PhotonNetwork.IsMasterClient)
                PhotonNetwork.CurrentRoom.IsOpen = true;

            GameManager.instance.battleOngoing = false;

            UIHandler.instance.ShowJoinGameText("LETS GOO!", PlayerSkinBank.GetPlayerSkinColors(1).winText);
            yield return new WaitForSecondsRealtime(0.25f);
            UIHandler.instance.HideJoinGameText();

            PlayerSpotlight.CancelFade(true);

            PlayerManager.instance.SetPlayersSimulated(false);
            MapManager.instance.LoadNextLevel(false, false);
            TimeHandler.instance.DoSpeedUp();

            yield return new WaitForSecondsRealtime(1f);

            yield return Pickphase();

            PlayerSpotlight.FadeIn();
            MapManager.instance.CallInNewMapAndMovePlayers(MapManager.instance.currentLevelID);
            TimeHandler.instance.DoSpeedUp();
            TimeHandler.instance.StartGame();
            GameManager.instance.battleOngoing = true;
            UIHandler.instance.ShowRoundCounterSmall(this.teamPoints, this.teamRounds);

            this.StartCoroutine(this.DoRoundStart());
        }

        public static void PlayerResync(int[] TeamIDs, int[] TeamPoints, int[] TeamRounds) {
            UIHandler.instance.ShowJoinGameText("Syncing New Players With Current Gamestate", PlayerSkinBank.GetPlayerSkinColors(1).winText);
            for(int i = 0; i< TeamIDs.Length; i++ ){
                instance.teamPoints[TeamIDs[i]] = TeamPoints[i];
                instance.teamRounds[TeamIDs[i]] = TeamRounds[i];
            }
        }

        public IEnumerator ResyncPlayers() {
            var tID = teamPoints.Keys;
            var tP = tID.Select(id => teamPoints[id]);
            var tR = tID.Select(id => teamRounds[id]);
            NetworkingManager.RPC(typeof(ConnectionTolerantDeathmatch), nameof(PlayerResync), tID,tP,tR);
            foreach(Player player in PlayerManager.instance.players) {
                var cards = ModdingUtils.Utils.Cards.instance.RemoveAllCardsFromPlayer(player);
                yield return new WaitForSecondsRealtime(1);
                ModdingUtils.Utils.Cards.instance.AddCardsToPlayer(player, cards, reassign: true);
                yield return new WaitForSecondsRealtime(1);
            }
            yield break;
        }

        override public IEnumerator RoundTransition(int[] winningTeamIDs) {
            yield return GameModeManager.TriggerHook(GameModeHooks.HookPointEnd);
            yield return GameModeManager.TriggerHook(GameModeHooks.HookRoundEnd);

            gameState = GameState.LateJoinphase;
            yield return GameModeManager.TriggerHook(HookLateJoinStart);
            if(PhotonNetwork.IsMasterClient) {
                if(WaitingClients.Count == 0) {
                    gameState = GameState.Ongoing;
                    NetworkingManager.RPC_Others(typeof(ConnectionTolerantDeathmatch), nameof(TriggerOngoing));
                } else {
                    foreach(int id in WaitingClients) {
                        NetworkingManager.RPC(typeof(ConnectionTolerantDeathmatch), nameof(ConnectionResponce), id, (int)ConectionStatusType.MidJoin);
                        JoinningClients.Add(id);
                    }
                    yield return new WaitUntil(() => JoinningClients.Count == 0);
                    yield return ResyncPlayers();
                    gameState = GameState.Ongoing;
                    NetworkingManager.RPC_Others(typeof(ConnectionTolerantDeathmatch), nameof(TriggerOngoing));
                }
            } else {
                yield return new WaitUntil(() => gameState != GameState.LateJoinphase);
            }
            yield return GameModeManager.TriggerHook(HookLateJoinEnd);

            UIHandler.instance.HideJoinGameText();


            int[] winningTeams = GameModeManager.CurrentHandler.GetGameWinners();
            if(winningTeams.Any()) {
                this.GameOver(winningTeamIDs);
                yield break;
            }

            this.StartCoroutine(PointVisualizer.instance.DoWinSequence(this.teamPoints, this.teamRounds, winningTeamIDs));

            yield return new WaitForSecondsRealtime(1f);
            MapManager.instance.LoadNextLevel(false, false);

            yield return new WaitForSecondsRealtime(1.3f);

            PlayerManager.instance.SetPlayersSimulated(false);
            TimeHandler.instance.DoSpeedUp();


            yield return Pickphase(winningTeamIDs);

            yield return this.StartCoroutine(this.WaitForSyncUp());
            PlayerSpotlight.FadeIn();

            TimeHandler.instance.DoSlowDown();
            MapManager.instance.CallInNewMapAndMovePlayers(MapManager.instance.currentLevelID);
            PlayerManager.instance.RevivePlayers();

            yield return new WaitForSecondsRealtime(0.3f);

            TimeHandler.instance.DoSpeedUp();
            GameManager.instance.battleOngoing = true;
            this.isTransitioning = false;
            UIHandler.instance.ShowRoundCounterSmall(this.teamPoints, this.teamRounds);

            this.StartCoroutine(this.DoRoundStart());
        }

        public virtual IEnumerator Pickphase(int[] winningTeamIDs = null) {

            UnityEngine.Debug.Log($"Picking Time");

            gameState = GameState.Pickphase;

            PlayerManager.instance.InvokeMethod("SetPlayersVisible", false);
            yield return GameModeManager.TriggerHook(GameModeHooks.HookPickStart);
            List<Player> pickOrder = PlayerManager.instance.GetPickOrder(winningTeamIDs);

            foreach(Player player in pickOrder) {
                yield return this.WaitForSyncUp();

                yield return GameModeManager.TriggerHook(GameModeHooks.HookPlayerPickStart);

                CardChoiceVisuals.instance.Show(player.playerID, true);
                yield return CardChoice.instance.DoPick(1, player.playerID, PickerType.Player);

                yield return GameModeManager.TriggerHook(GameModeHooks.HookPlayerPickEnd);

                yield return new WaitForSecondsRealtime(0.1f);
            }

            yield return this.WaitForSyncUp();
            CardChoiceVisuals.instance.Hide();

            yield return GameModeManager.TriggerHook(GameModeHooks.HookPickEnd);

            PlayerManager.instance.InvokeMethod("SetPlayersVisible", true);

            gameState = GameState.Battlephase;
        }

    }
}
