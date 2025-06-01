using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Authentication;
using Unity.Services.Core;

namespace NetcodePlus
{
    /// <summary>
    /// Connect to Unity Lobby Services
    /// </summary>

    public class ClientLobbyService : ClientLobby
    {

        private Lobby lobby;
        private LobbyEventCallbacks lobby_events;
        private string unity_player_id = "";

        private const int max_lobby_display = 32;

        protected static ClientLobbyService instance;

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
            enabled = (NetworkData.Get().lobby_type == LobbyType.UnityServices);

            if (NetworkData.Get().lobby_type == LobbyType.UnityServices && NetworkData.Get().lobby_game_type != ServerType.RelayServer)
            {
                Debug.LogError("Unity Services Lobby is only supported with Relay Server!");
            }
        }

        public override async Task<bool> Connect()
        {
            //If auth system is NOT unity services, need to login anymously to unity services
            if (!Authenticator.Get().IsUnityServices())
                await LoginUnity();

            bool is_login = Authenticator.Get().IsConnected();
            client_id = NetworkTool.GenerateRandomUInt64();
            unity_player_id = Authenticator.Get().IsUnityServices() ? Authenticator.Get().UserID : AuthenticationService.Instance.PlayerId;
            connected = is_login;
            onConnect?.Invoke(is_login);
            return true;
        }

        private async Task<bool> LoginUnity()
        {
            try
            {
                if(UnityServices.State == ServicesInitializationState.Uninitialized)
                    await UnityServices.InitializeAsync();

                if (!AuthenticationService.Instance.IsAuthorized && NetworkData.Get().auth_auto_logout)
                    AuthenticationService.Instance.ClearSessionToken();

                if (!AuthenticationService.Instance.IsAuthorized)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
            catch (AuthenticationException ex) { Debug.LogException(ex); }
            catch (RequestFailedException ex) { Debug.LogException(ex); }
            return AuthenticationService.Instance.IsAuthorized;
        }

        public override async void RefreshLobby()
        {
            List<QueryFilter> filters = new List<QueryFilter>();
            QueryLobbiesOptions queryOptions = new QueryLobbiesOptions
            {
                Count = max_lobby_display,
                Filters = filters
            };

            QueryResponse res = await LobbyService.Instance.QueryLobbiesAsync(queryOptions);
            LobbyGameList list = ConvertList(res.Results);
            onRefreshList?.Invoke(list);
        }

        public override async void RefreshGame()
        {
            if (!IsInLobby())
                return;

            lobby = await LobbyService.Instance.GetLobbyAsync(lobby.Id);

            LobbyGame game = ConvertLobby(lobby);
            onRefresh?.Invoke(game);
        }

        public override async Task CreateGame(CreateGameData cdata)
        {
            Dictionary<string, PlayerDataObject> user_data = new Dictionary<string, PlayerDataObject>();
            PlayerDataObject user_obj = new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, Authenticator.Get().Username);
            user_data.Add("Username", user_obj);

            PlayerDataObject client_obj = new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, client_id.ToString());
            user_data.Add("ClientID", client_obj);

            int state = (int) RoomState.Waiting;
            CreateLobbyOptions createOptions = new CreateLobbyOptions
            {
                IsPrivate = cdata.hidden,
                Player = new Player(id: unity_player_id, data: user_data),
                Data = new Dictionary<string, DataObject>()
                {
                    {"Save", new DataObject(DataObject.VisibilityOptions.Public, cdata.savefile)},
                    {"Scene", new DataObject(DataObject.VisibilityOptions.Public, cdata.scene)},
                    {"State", new DataObject(DataObject.VisibilityOptions.Public, state.ToString())},
                    {"Extra", new DataObject(DataObject.VisibilityOptions.Public, NetworkTool.DeserializeString(cdata.extra))},
                }
            };

            lobby = await LobbyService.Instance.CreateLobbyAsync(cdata.title, cdata.players_max, createOptions);
            LinkEvents();

            LobbyGame game = ConvertLobby(lobby);
            onRefresh?.Invoke(game);
        }
        
        public override async void JoinGame(string game_id)
        {
            Dictionary<string, PlayerDataObject> user_data = new Dictionary<string, PlayerDataObject>();

            PlayerDataObject user_obj = new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, Authenticator.Get().Username);
            user_data.Add("Username", user_obj);

            PlayerDataObject client_obj = new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, client_id.ToString());
            user_data.Add("ClientID", client_obj);

            JoinLobbyByIdOptions joinOptions = new JoinLobbyByIdOptions { Player = new Player(id: unity_player_id, data: user_data) };
            lobby = await LobbyService.Instance.JoinLobbyByIdAsync(game_id, joinOptions);
            LinkEvents();

            LobbyGame game = ConvertLobby(lobby);
            onRefresh?.Invoke(game);
        }

        public override async void QuitGame()
        {
            if (!IsInLobby())
                return;

            string lobby_id = lobby.Id;
            await LobbyService.Instance.RemovePlayerAsync(lobby_id, unity_player_id);

            RemoveFromLobby();
            RefreshLobby();
        }

        public override async void StartGame()
        {
            if (!IsInLobby())
                return;

            string join_code = "";
            if (NetworkData.Get().lobby_game_type == ServerType.RelayServer)
            {
                //Before starting the game, need to create it on the relay server to get the join_code
                relay_data = await NetworkRelay.HostGame(NetworkData.Get().players_max);
                if (relay_data == null)
                    return; //Failed to create relay game
                join_code = relay_data.join_code;
                Debug.Log("RELAY HOST CODE " + relay_data.join_code);
            }

            int state = (int)RoomState.Playing;
            UpdateLobbyOptions updateOptions = new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>()
                {
                    {"State", new DataObject(DataObject.VisibilityOptions.Public, state.ToString())},
                    {"JoinCode", new DataObject(DataObject.VisibilityOptions.Member, join_code)}
                }
            };

            lobby = await LobbyService.Instance.UpdateLobbyAsync(lobby.Id, updateOptions);

            LobbyGame game = ConvertLobby(lobby);
            onRefresh?.Invoke(game);
        }

        public override async void SendChat(string text)
        {
            LobbyGame game = ConvertLobby(lobby);
            ChatMsg msg = new ChatMsg(Username, text);
            game.chats.Add(msg);

            if (game.chats.Count > 20)
                game.chats.RemoveAt(0);

            string data = WriteChat(game.chats, Username);

            UpdatePlayerOptions updateOptions = new UpdatePlayerOptions
            {
                Data = new Dictionary<string, PlayerDataObject>()
                {
                    {"Chat", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, data)}
                }
            };

            lobby = await LobbyService.Instance.UpdatePlayerAsync(lobby.Id, unity_player_id, updateOptions);

            LobbyGame ugame = ConvertLobby(lobby);
            onRefresh?.Invoke(ugame);
        }

        public override void StartMatchmaking(string group, string scene, int nb_players)
        {
            Debug.LogError("Matchmaking NOT supported with Unity Services");
        }

        public override async void Disconnect()
        {
            connected = false;
            unity_player_id = "";
            RemoveFromLobby();
            await TimeTool.Delay(0);
        }

        public override async void KeepAlive()
        {
            if (!IsInLobby())
                return;

            await LobbyService.Instance.SendHeartbeatPingAsync(lobby.Id);
        }

        private async void LinkEvents()
        {
            lobby_events = new LobbyEventCallbacks();
            lobby_events.LobbyDeleted += OnLobbyDelete;
            lobby_events.KickedFromLobby += OnKicked;
            lobby_events.PlayerJoined += OnPlayerJoin;
            lobby_events.PlayerLeft += OnPlayerLeft;

            await LobbyService.Instance.SubscribeToLobbyEventsAsync(lobby.Id, lobby_events);
        }

        private void RemoveFromLobby()
        {
            lobby = null;
            lobby_events = new LobbyEventCallbacks();
            joined_game_id = "";
        }

        private void OnLobbyDelete()
        {
            if (lobby != null)
            {
                RemoveFromLobby();
                RefreshLobby();
            }
        }

        private void OnKicked()
        {
            if (lobby != null)
            {
                RemoveFromLobby();
                RefreshLobby();
            }
        }

        private void OnPlayerJoin(List<LobbyPlayerJoined> players)
        {

        }

        private void OnPlayerLeft(List<int> players)
        {

        }

        public LobbyGameList ConvertList(List<Lobby> lobbies)
        {
            LobbyGameList list = new LobbyGameList();
            List<LobbyGame> games = new List<LobbyGame>();
            for (int i = 0; i < lobbies.Count; i++)
            {
                LobbyGame game = ConvertLobby(lobbies[i]);
                if(game.state == RoomState.Waiting)
                    games.Add(game);
            }
            list.data = games.ToArray();
            return list;
        }

        public LobbyGame ConvertLobby(Lobby lobby)
        {
            string game_uid = lobby.Id;
            LobbyGame game = new LobbyGame(NetworkData.Get().lobby_game_type, game_uid);
            game.title = lobby.Name;
            game.players_max = lobby.MaxPlayers;
            game.players = new List<LobbyPlayer>();
            game.hidden = lobby.IsPrivate;

            game.scene = GetValue(lobby, "Scene");
            game.save = GetValue(lobby, "Save");
            game.join_code = GetValue(lobby, "JoinCode");

            string state_str = GetValue(lobby, "State");
            bool valid_state = int.TryParse(state_str, out int state_int);
            game.state = valid_state ? (RoomState)state_int : RoomState.None;

            string extra = GetValue(lobby, "Extra");
            game.extra = NetworkTool.SerializeString(extra);

            ulong player_index = 1;
            foreach (Player player in lobby.Players)
            {
                string username = GetValue(player, "Username");
                string client_id = GetValue(player, "ClientID");
                bool valid_cid = ulong.TryParse(client_id, out ulong cid);

                LobbyPlayer lplayer = new LobbyPlayer(player.Id, username);
                lplayer.game_id = game_uid;
                lplayer.client_id = valid_cid ? cid : player_index;
                game.players.Add(lplayer);
                player_index++;
            }

            game.chats = ReadChat(lobby.Players);

            return game;
        }

        private string GetValue(Lobby lobby, string id)
        {
            if (lobby.Data != null)
            {
                bool valid = lobby.Data.TryGetValue(id, out DataObject val);
                return valid ? val.Value : "";
            }
            return "";
        }

        private string GetValue(Player player, string id)
        {
            if (player.Data != null)
            {
                bool valid = player.Data.TryGetValue(id, out PlayerDataObject val);
                return valid ? val.Value : "";
            }
            return "";
        }

        public string WriteChat(List<ChatMsg> msgs, string username)
        {
            int index = 0;
            List<string> lines = new List<string>();
            foreach (ChatMsg msg in msgs)
            {
                if (msg.username == username)
                {
                    string line = index + "--::--" + msg.text;
                    lines.Add(line);
                }
                index++;
            }
            return string.Join("--$$--", lines);
        }

        public List<ChatMsg> ReadChat(List<Player> players)
        {
            List<ChatMsg> msgs = new List<ChatMsg>();
            foreach (Player player in players)
            {
                string username = GetValue(player, "Username");
                string chat_str = GetValue(player, "Chat");
                List<ChatMsg> player_chat = ReadChat(chat_str, username);
                msgs.AddRange(player_chat);
            }

            msgs.Sort((ChatMsg a, ChatMsg b) => { return a.index.CompareTo(b.index); });

            return msgs;
        }

        public List<ChatMsg> ReadChat(string data, string username)
        {
            string[] lines = data.Split(new string[] { "--$$--" }, System.StringSplitOptions.None);
            List<ChatMsg> msgs = new List<ChatMsg>();
            foreach (string line in lines)
            {
                if (line.Contains("--::--"))
                {
                    ChatMsg msg = new ChatMsg();
                    string[] parts = line.Split(new string[] { "--::--" }, System.StringSplitOptions.None);
                    int.TryParse(parts[0], out int index);
                    msg.index = index;
                    msg.text = parts[1];
                    msg.username = username;
                    msgs.Add(msg);
                }
            }
            return msgs;
        }

        public override bool IsInLobby() { return connected && lobby != null; }

        public static new ClientLobbyService Get()
        {
            return instance;
        }

    }
}
