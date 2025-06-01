using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

namespace NetcodePlus
{

    public class ClientLobbyDedicated : ClientLobby
    {
        /// <summary>
        /// Connect to Dedicated Lobby Server
        /// </summary>

        protected static ClientLobbyDedicated instance;

        protected virtual void Awake()
        {
             instance = this;
        }

        protected virtual void OnDestroy()
        {
            instance = null;
        }

        private void Start()
        {
            enabled = (NetworkData.Get().lobby_type == LobbyType.Dedicated);
            Client.SetDefaultUrl(NetworkData.Get().lobby_host, NetworkData.Get().lobby_port);
        }

        public override async Task<bool> Connect()
        {
            Debug.Log("Connect to Lobby: " + NetworkData.Get().lobby_host);

            LobbyPlayer player = new LobbyPlayer(UserID, Username);
            WebResponse res = await Client.Send("connect", player);

            if (res.success)
            {
                client_id = res.GetInt64();
                connected = true;
                joined_game_id = "";
                Client.SetClientID(client_id);
            }

            onConnect?.Invoke(res.success);
            return res.success;
        }

        public override async void RefreshLobby()
        {
            WebResponse res = await Client.Send("refresh_list");
            LobbyGameList list = res.GetData<LobbyGameList>();
            if (res.success)
                onRefreshList?.Invoke(list);
        }

        public override async void RefreshGame()
        {
            if (!IsInLobby())
                return;

            WebResponse res = await Client.Send("refresh", joined_game_id);
            GameRefresh game = res.GetData<GameRefresh>();
            if (res.success)
                onRefresh?.Invoke(game.game);
        }

        public override async Task CreateGame(CreateGameData cdata)
        {
            WebResponse res = await Client.Send("create", cdata);
            GameRefresh game = res.GetData<GameRefresh>();
            if (res.success && game.valid)
            {
                joined_game_id = game.game.game_id;
                onRefresh?.Invoke(game.game);
            }
        }
        
        public override async void JoinGame(string game_id)
        {
            if (!connected)
                return;

            WebResponse res = await Client.Send("join", game_id);
            GameRefresh game = res.GetData<GameRefresh>();
            if (res.success && game.valid)
            {
                joined_game_id = game.game.game_id;
                onRefresh?.Invoke(game.game);
            }
        }

        public override async void QuitGame()
        {
            if (!IsInLobby())
                return;

            WebResponse res = await Client.Send("quit", joined_game_id);
            joined_game_id = "";
            GameRefresh game = res.GetData<GameRefresh>();
            if (res.success)
                onRefresh?.Invoke(game.game);
        }

        public override async void StartGame()
        {
            if (!connected || !IsInLobby())
                return;

            StartGameData sdata = new StartGameData();
            sdata.game_uid = joined_game_id;
            sdata.join_code = "";

            if (NetworkData.Get().lobby_game_type == ServerType.RelayServer)
            {
                //Before starting the game, need to create it on the relay server to get the join_code
                relay_data = await NetworkRelay.HostGame(NetworkData.Get().players_max);
                if (relay_data == null)
                    return; //Failed to create relay game
                sdata.join_code = relay_data.join_code;
                Debug.Log("RELAY HOST CODE " + relay_data.join_code);
            }

            WebResponse res = await Client.Send("start", sdata);
            GameRefresh game = res.GetData<GameRefresh>();
            if (res.success)
                onRefresh?.Invoke(game.game);
        }

        public override async void SendChat(string text)
        {
            ChatMsg chat = new ChatMsg(Username, text);
            WebResponse res = await Client.Send("chat", chat);
            GameRefresh game = res.GetData<GameRefresh>();
            if (res.success)
                onRefresh?.Invoke(game.game);
        }

        public override async void KeepAlive()
        {
            //Keep the current game on
            WebResponse res = await Client.Send("keep");
            if (res.success)
                connected = res.GetBool(); //Server returns if that client is still connected
        }

        public override async void KeepAlive(string game_id, string[] user_list)
        {
            KeepMsg msg = new KeepMsg(game_id, user_list);
            await Client.Send("keep_list", msg); //Keep the current game on
        }

        public override void StartMatchmaking(string group, string scene, int nb_players)
        {
            MatchmakingRequest req = new MatchmakingRequest();
            req.group = group;
            req.scene = scene;
            req.players = nb_players;
            req.extra = extra_data;
            StartMatchmaking(req);
        }

        public override async void StartMatchmaking(MatchmakingRequest req)
        {
            if (!connected)
                return;

            req.is_new = true;
            current_matchmaking = req;
            matchmaking_timer = 0f;

            Debug.Log("Start Matchmaking");

            WebResponse res = await Client.Send("matchmaking", req);
            LobbyGame result = res.GetData<LobbyGame>();
            ReceiveMatchmakingResult(result);
        }

        public override async void RefreshMatchmaking()
        {
            if (current_matchmaking != null)
            {
                current_matchmaking.is_new = false;
                matchmaking_timer = 0f;
                WebResponse res = await Client.Send("matchmaking", current_matchmaking);
                LobbyGame result = res.GetData<LobbyGame>();
                ReceiveMatchmakingResult(result);
            }
        }

        public override async void CancelMatchmaking()
        {
            Debug.Log("Cancel Matchmaking");
            current_matchmaking = null;
            matchmaking_timer = 0f;
            await Client.Send("cancel");
        }

        public override async void Disconnect()
        {
            if (IsInLobby())
                await Client.Send("quit", joined_game_id);
            connected = false;
            joined_game_id = "";
        }

        protected override void ReceiveMatchmakingResult(LobbyGame result)
        {
            if (result == null)
                return; //Invalid result
            if (result.IsValid())
                current_matchmaking = null; //Success! Stop matchmaking
            onMatchmaking?.Invoke(result);
        }

        public WebClient Client { get { return WebClient.Get(); } }

        public static new ClientLobbyDedicated Get()
        {
            return instance;
        }
    }
}
