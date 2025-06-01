using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace NetcodePlus
{
    /// <summary>
    /// Lobby server is used to connect players together and also to spawn game servers
    /// </summary>

    public class ServerLobby : MonoBehaviour
    {
        public NetworkData data;
        public string lobby_key = "LOBBYKEY"; //Key allowing different servers to communicate, change this value in the scene (not here)
                                               //Since the code will be included in client build, but not the scene
        
        private Dictionary<ulong, LobbyPlayer> player_list = new Dictionary<ulong, LobbyPlayer>();
        private Dictionary<string, LobbyGame> games_list = new Dictionary<string, LobbyGame>();
        private Dictionary<string, HashSet<ushort>> used_ports = new Dictionary<string, HashSet<ushort>>();
        private List<string> remove_game_list = new List<string>();
        private List<ulong> remove_player_list = new List<ulong>();

        private ServerLauncher launcher;
        private ServerMatchmaker matchmaker;
        private ServerLobbyConfig config;

        private ulong server_tick = 0;
        private float timer = 0f;
        private float info_timer = 0f;
        private bool active = false;

        private const ulong game_expiration = 60; //Seconds of inactivity when the game expires

        private static ServerLobby instance;

        void Awake()
        {
            instance = this;
            Application.runInBackground = true;
            Application.targetFrameRate = 60;

            config = new ServerLobbyConfig(data);
            launcher = new ServerLauncher(config);
            matchmaker = new ServerMatchmaker(config);
        }

        private void Start()
        {
            Client.SetKey(lobby_key);
            launcher.onServerEnd += OnServerEnd;
            Server.StartServer(config.LobbyPort);
            Server.RegisterRequest("connect", ReceiveConnect);
            Server.RegisterRequest("refresh_list", ReceiveRefreshList);
            Server.RegisterRequest("refresh", ReceiveRefresh);
            Server.RegisterRequest("create", ReceiveCreate);
            Server.RegisterRequest("join", ReceiveJoin);
            Server.RegisterRequest("quit", ReceiveQuit);
            Server.RegisterRequest("start", ReceiveStart);
            Server.RegisterRequest("chat", ReceiveChat);
            Server.RegisterRequest("keep", ReceiveKeep);
            Server.RegisterRequest("keep_list", ReceiveKeepList);
            Server.RegisterRequest("matchmaking", ReceiveMatchmaking);
            Server.RegisterRequest("cancel", ReceiveCancel);
            Server.RegisterRequest("remote_start", ReceiveRemoteStart);
            Server.RegisterRequest("remote_stop", ReceiveRemoteStop);
            Server.RegisterRequest("remote_info", ReceiveRemoteInfo);
        }

        private void OnDestroy()
        {
            launcher.StopAllGames();
            Server.UnRegisterRequest("connect");
            Server.UnRegisterRequest("refresh_list");
            Server.UnRegisterRequest("refresh");
            Server.UnRegisterRequest("create");
            Server.UnRegisterRequest("join");
            Server.UnRegisterRequest("quit");
            Server.UnRegisterRequest("start");
            Server.UnRegisterRequest("chat");
            Server.UnRegisterRequest("keep");
            Server.UnRegisterRequest("keep_list");
            Server.UnRegisterRequest("matchmaking");
            Server.UnRegisterRequest("cancel");
            Server.UnRegisterRequest("remote_start");
            Server.UnRegisterRequest("remote_stop");
            Server.UnRegisterRequest("remote_info");
        }

        void Update()
        {
            timer += Time.deltaTime;
            if (timer > 1f)
            {
                timer = 0f;
                SlowUpdate();
            }

            info_timer += Time.deltaTime;
            if (info_timer > 10f)
            {
                info_timer = 0f;
                LoadInfo();
            }
        }

        void SlowUpdate()
        {
            server_tick++;
            launcher.SlowUpdate();
            matchmaker.SlowUpdate(server_tick);

            //Remove rooms with no players
            foreach (KeyValuePair<string, LobbyGame> game in games_list)
            {
                ulong expire_at = game.Value.last_update + game_expiration;
                if (game.Value.players.Count == 0 || server_tick > expire_at)
                {
                    remove_game_list.Add(game.Key);
                }
            }
            foreach (string id in remove_game_list)
            {
                //Dont stop game here, lobby is killed but game exe could still be running, it will stop by itself when no one connected (NetworkGame)
                games_list.Remove(id); 
            }

            if (remove_game_list.Count > 0)
                remove_game_list.Clear();

            //Remove inactive players
            foreach (KeyValuePair<ulong, LobbyPlayer> player in player_list)
            {
                ulong expire_at = player.Value.last_update + game_expiration;
                if (server_tick > expire_at)
                {
                    remove_player_list.Add(player.Key);
                }
            }

            foreach (ulong id in remove_player_list)
            {
                RemovePlayerAllGames(id);
                player_list.Remove(id);
            }

            if (remove_player_list.Count > 0)
                remove_player_list.Clear();
        }

        private void LoadInfo()
        {
            if (!active)
                return; //Inactive until first player connects

            if (config.GameServerType != ServerType.DedicatedServer)
                return; //Wrong type of server

            foreach (string url in config.GameHosts)
            {
                if (url != config.LobbyHost)
                {
                    LoadInfo(url);
                }
            }
        }

        private async void LoadInfo(string url)
        {
            LobbyGameList list = await SendGetRemoteInfo(url);
            if (list != null && list.data != null)
            {
                //Refresh port being used
                ClearUsed(url);
                foreach (LobbyGame game in list.data)
                {
                    AddPort(url, game.server_port);
                }
            }
        }

        private void ReceiveConnect(WebContext context)
        {
            LobbyPlayer player = context.GetData<LobbyPlayer>();
            if (player != null && config.LobbyEnabled)
            {
                ulong token = CreatePlayer(player);
                RemovePlayerAllGames(token);
                context.SendResponse(token);
                active = true;
                Debug.Log("Connect: " + player.username);
            }
            else
            {
                context.SendError("Connection Disabled!");
            }
        }

        private void ReceiveRefreshList(WebContext context)
        {
            LobbyPlayer player = GetPlayer(context.GetClientID());
            if (player != null)
                player.last_update = server_tick;
            List<LobbyGame> rooms = new List<LobbyGame>();
            foreach (KeyValuePair<string, LobbyGame> game in games_list)
            {
                if (!game.Value.hidden)
                    rooms.Add(game.Value);
            }
            LobbyGameList list = new LobbyGameList(rooms.ToArray());
            context.SendResponse(list);
        }

        private void ReceiveRefresh(WebContext context)
        {
            string game_id = context.GetString();
            LobbyGame game = GetGame(game_id);
            LobbyPlayer player = GetPlayer(context.GetClientID());
            if (game != null)
                game.last_update = server_tick;
            if (player != null)
                player.last_update = server_tick;
            GameRefresh refresh = new GameRefresh(game);
            context.SendResponse(refresh);
        }

        private void ReceiveCreate(WebContext context)
        {
            CreateGameData dat = context.GetData<CreateGameData>();
            if (dat == null)
                return;

            if (games_list.Count >= config.LobbyRoomsMax)
                return; //Max number of rooms

            LobbyGame game = CreateGame(config.GameServerType, dat, context.GetIP());
            LobbyPlayer player = GetPlayer(context.GetClientID());
            if (game != null && player != null)
            {
                AddPlayerGame(player, game);
            }

            GameRefresh refresh = new GameRefresh(game); //Encapsulate inside another obj in case the game is null
            context.SendResponse(refresh);
        }

        private void ReceiveJoin(WebContext context)
        {
            string game_id = context.GetString();
            LobbyGame game = GetGame(game_id);
            LobbyPlayer player = GetPlayer(context.GetClientID());
            if (CanJoinGame(player, game))
            {
                game.last_update = server_tick;
                player.last_update = server_tick;
                AddPlayerGame(player, game);
            }

            GameRefresh refresh = new GameRefresh(game);
            context.SendResponse(refresh);
        }

        private void ReceiveQuit(WebContext context)
        {
            string game_id = context.GetString();
            LobbyGame game = GetGame(game_id);
            LobbyPlayer player = GetPlayer(context.GetClientID());
            if (game != null && player != null && game.state == RoomState.Waiting)
            {
                RemovePlayerGame(player, game);
            }

            GameRefresh refresh = new GameRefresh(game);
            context.SendResponse(refresh);
        }

        private void ReceiveStart(WebContext context)
        {
            StartGameData sdata = context.GetData<StartGameData>();
            LobbyGame game = GetGame(sdata.game_uid);
            if (CanStartGame(context.GetClientID(), game))
            {
                LobbyPlayer player = game.GetHost();
                if (player != null)
                {
                    player.last_update = server_tick;
                    game.last_update = server_tick;
                    game.join_code = sdata.join_code;
                    StartGame(game);
                }
            }

            GameRefresh refresh = new GameRefresh(game);
            context.SendResponse(refresh);
        }

        private void ReceiveChat(WebContext context)
        {
            ChatMsg msg = context.GetData<ChatMsg>();
            LobbyPlayer client = GetPlayer(context.GetClientID());
            LobbyGame game = GetGame(client.game_id);
            if (msg != null && client != null && game != null && game.HasPlayer(context.GetClientID()))
            {
                game.AddChat(msg);
            }

            GameRefresh refresh = new GameRefresh(game);
            context.SendResponse(refresh);
        }

        //Keep the player connected
        private void ReceiveKeep(WebContext context)
        {
            LobbyPlayer player = GetPlayer(context.GetClientID());
            if (player != null)
            {
                player.last_update = server_tick;
                LobbyGame game = GetGame(player.game_id);
                if (game != null)
                    game.last_update = server_tick;

                if (game != null && game.remote && game.state == RoomState.Playing)
                {
                    string[] users = new string[1] { player.user_id };
                    SendRemoteKeepAlive(game.server_host, game.game_id, users);
                }
            }
            context.SendResponse(player != null);
        }

        //Keep multiple players connected (sent from game server)
        private void ReceiveKeepList(WebContext context)
        {
            LobbyPlayer player = GetPlayer(context.GetClientID());
            if (player != null)
                player.last_update = server_tick; //Refresh sending player (maybe be null if not a player)

            KeepMsg msg = context.GetData<KeepMsg>();
            if (msg != null)
            {
                LobbyGame game = GetGame(msg.game_uid);
                if (game != null)
                {
                    game.last_update = server_tick;
                    foreach (string user_id in msg.user_list)
                    {
                        LobbyPlayer tplayer = GetPlayerByUserId(user_id);
                        if (tplayer != null)
                            tplayer.last_update = server_tick;
                    }

                    if(game.remote && game.state == RoomState.Playing)
                        SendRemoteKeepAlive(game.server_host, game.game_id, msg.user_list);
                }
            }
            context.SendResponse();
        }

        private void ReceiveMatchmaking(WebContext context)
        {
            LobbyPlayer player = GetPlayer(context.GetClientID());
            MatchmakingRequest req = context.GetData<MatchmakingRequest>();
            LobbyGame result = new LobbyGame();
            if (player != null && req != null)
            {
                result = matchmaker.FindMatchmaking(player, req, server_tick, context.GetIP());
            }
            context.SendResponse(result);
        }

        private void ReceiveCancel(WebContext context)
        {
            LobbyPlayer player = GetPlayer(context.GetClientID());
            if (player != null)
            {
                matchmaker.CancelMatchmaking(player);
            }
            context.SendResponse();
        }

        //Tell another server to start a game
        private async void SendRemoteStart(LobbyGame game)
        {
            Debug.Log("Start Remote Game: " + game.server_host + " " + game.server_port + " " + game.game_id);
            string rurl = Client.GetRawUrl(game.server_host, config.LobbyPort, "remote_start");
            await Client.SendUrl(rurl, game);
        }

        //Tell another server to stop a game
        private async void SendRemoteStop(LobbyGame game)
        {
            Debug.Log("Stop Remote Game: " + game.server_host + " " + game.server_port + " " + game.game_id);
            string rurl = Client.GetRawUrl(game.server_host, config.LobbyPort, "remote_stop");
            await Client.SendUrl(rurl, game.game_id);
        }

        //Retrieve other server games list
        private async Task<LobbyGameList> SendGetRemoteInfo(string host)
        {
            string rurl = Client.GetRawUrl(host, config.LobbyPort, "remote_info");
            WebResponse res = await Client.SendUrl(rurl);
            LobbyGameList list = res.GetData<LobbyGameList>();
            return list;
        }

        private async void SendRemoteKeepAlive(string url, string game_uid, string[] user_list)
        {
            KeepMsg msg = new KeepMsg(game_uid, user_list);
            string rurl = Client.GetRawUrl(url, config.LobbyPort, "keep_list");
            await Client.SendUrl(rurl, msg);
        }

        private void ReceiveRemoteStart(WebContext context)
        {
            LobbyGame game = context.GetData<LobbyGame>();
            if (game != null && context.IsKeyValid(lobby_key) && !games_list.ContainsKey(game.game_id))
            {
                game.remote = false; //No more remote here
                games_list[game.game_id] = game;
                AddPort(game.server_host, game.server_port);
                launcher.StartGame(game);
                context.SendResponse(true);
            }
            else
            {
                context.SendResponse(false);
            }
        }

        private void ReceiveRemoteStop(WebContext context)
        {
            string game_id = context.GetString();
            if (!string.IsNullOrEmpty(game_id) && context.IsKeyValid(lobby_key) && games_list.ContainsKey(game_id))
            {
                LobbyGame game = GetGame(game_id);
                if (game != null)
                    RemovePort(game.server_host, game.server_port);

                games_list.Remove(game_id);
                launcher.StopGame(game_id);
                context.SendResponse(true);
            }
            else
            {
                context.SendResponse(false);
            }
        }

        private void ReceiveRemoteInfo(WebContext context)
        {
            if (context.IsKeyValid(lobby_key))
            {
                LobbyGameList list = new LobbyGameList();
                list.data = new LobbyGame[games_list.Count];
                int index = 0;
                foreach (KeyValuePair<string, LobbyGame> pair in games_list)
                {
                    list.data[index] = pair.Value;
                    index++;
                }
                context.SendResponse(list);
            }
            else
            {
                LobbyGameList list = new LobbyGameList();
                list.data = new LobbyGame[0];
                context.SendResponse(list);
            }
        }

        private bool CanJoinGame(LobbyPlayer player, LobbyGame game)
        {
            if (player == null || game == null)
                return false;
            if (game.state == RoomState.Ended)
                return false;
            if (game.HasPlayer(player.user_id))
                return true;
            if (game.players.Count < game.players_max)
                return true;
            return false;
        }

        private bool CanStartGame(ulong client_id, LobbyGame game)
        {
            return game != null && game.state == RoomState.Waiting && game.IsHost(client_id);
        }

        public LobbyGame CreateGame(CreateGameData cdata, string host_ip)
        {
            return CreateGame(config.GameServerType, cdata, host_ip);
        }

        public LobbyGame CreateGame(ServerType type, CreateGameData cdata, string host_ip)
        {
            bool valid = FindServer(type, cdata, host_ip, out string host, out ushort port);
            if (valid)
            {
                string uid = NetworkTool.GenerateRandomID();
                LobbyGame game = new LobbyGame(type, uid);
                game.state = RoomState.Waiting;
                game.server_host = host;
                game.server_port = port;
                game.title = cdata.title;
                game.scene = cdata.scene;
                game.save = cdata.savefile;
                game.hidden = cdata.hidden;
                game.players_max = cdata.players_max;
                game.join_code = "";
                game.last_update = server_tick;
                game.remote = type == ServerType.DedicatedServer && host != config.LobbyHost;
                game.extra = cdata.extra;
                games_list[uid] = game;
                if(type == ServerType.DedicatedServer)
                    AddPort(game.server_host, game.server_port);
                return game;
            }
            return null;
        }

        public void StartGame(LobbyGame game)
        {
            game.state = RoomState.Playing;
            if (game.type == ServerType.DedicatedServer)
            {
                if (game.remote)
                {
                    SendRemoteStart(game);
                }
                else
                {
                    launcher.StartGame(game);
                }
            }
        }

        public void StopGame(string game_id)
        {
            LobbyGame game = GetGame(game_id);
            if (game != null && game.type == ServerType.DedicatedServer)
            {
                RemovePort(game.server_host, game.server_port);

                if (game.remote)
                {
                    SendRemoteStop(game);
                }
                else
                {
                    launcher.StopGame(game.game_id);
                }
            }

            games_list.Remove(game_id);
        }

        private void OnServerEnd(string game_id)
        {
            LobbyGame game = GetGame(game_id);
            if (game != null)
            {
                games_list.Remove(game_id);
                RemovePort(game.server_host, game.server_port);
            }
        }

        public ulong CreatePlayer(LobbyPlayer player)
        {
            ulong id = NetworkTool.GenerateRandomUInt64();
            player.client_id = id;
            player.last_update = server_tick;
            player_list[id] = player;
            return id;
        }

        public LobbyPlayer GetPlayer(ulong client_id)
        {
            if (player_list.ContainsKey(client_id))
                return player_list[client_id];
            return null;
        }

        public LobbyPlayer GetPlayerByUserId(string user_id)
        {
            foreach (KeyValuePair<ulong, LobbyPlayer> pair in player_list)
            {
                if (pair.Value.user_id == user_id)
                    return pair.Value;
            }
            return null;
        }

        public void RemovePlayer(ulong client_id)
        {
            LobbyPlayer player = GetPlayer(client_id);
            if (player != null)
            {
                player_list.Remove(client_id);
            }
        }

        public void AddPlayerGame(LobbyPlayer player, LobbyGame game)
        {
            if (game != null && player != null)
            {
                game.AddPlayer(player);
            }
        }

        public void RemovePlayerGame(LobbyPlayer player, LobbyGame game)
        {
            if (game != null && player != null)
            {
                game.RemovePlayer(player.client_id);
                player.game_id = "";
            }

            if (game != null && game.players.Count == 0)
            {
                games_list.Remove(game.game_id);
                RemovePort(game.server_host, game.server_port);
            }
        }

        public void RemovePlayerAllGames(ulong client_id)
        {
            LobbyPlayer client = GetPlayer(client_id);
            client.game_id = "";

            foreach (KeyValuePair<string, LobbyGame> game in games_list)
            {
                game.Value.RemovePlayer(client_id);
            }
        }

        public LobbyGame GetGame(string game_uid)
        {
            if (games_list.ContainsKey(game_uid))
                return games_list[game_uid];
            return null;
        }

        public void AddPort(string host, ushort port)
        {
            if (!used_ports.ContainsKey(host))
                used_ports[host] = new HashSet<ushort>();
            used_ports[host].Add(port);
        }

        public void RemovePort(string host, ushort port)
        {
            if (used_ports.ContainsKey(host))
                used_ports[host].Remove(port);
        }

        public void ClearUsed(string host)
        {
            if (used_ports.ContainsKey(host))
                used_ports[host].Clear();
        }

        public bool IsPortUsed(string host, ushort port)
        {
            if (used_ports.ContainsKey(host))
                return used_ports[host].Contains(port);
            return false;
        }

        public int CountGames(string host)
        {
            if (used_ports.ContainsKey(host))
                return used_ports[host].Count;
            return 0;
        }

        private bool FindServer(ServerType type, CreateGameData cdata, string host_ip, out string ohost, out ushort oport)
        {
            if (type == ServerType.DedicatedServer)
                return FindServerDedicated(cdata, out ohost, out oport);
            else if (type == ServerType.PeerToPeer || type == ServerType.RelayServer)
                return FindServerHost(host_ip, out ohost, out oport);
            ohost = ""; oport = 0;
            return false;
        }

        private bool FindServerHost(string host_ip, out string ohost, out ushort oport)
        {
            ohost = host_ip; //The host sends his own IP
            oport = config.GamePort; //Always use same port for hosted games
            bool valid = !string.IsNullOrEmpty(ohost);
            return valid;
        }

        private bool FindServerDedicated(CreateGameData cdata, out string ohost, out ushort oport)
        {
            ohost = PickHost();
            oport = GamePortMin;    //There can be multiple games running on the same server, to know which to connect, each game should have different port

            int max = GetMaxGames();
            if (CountGames(ohost) >= max)
                return false; //Maximum number of games reached

            //Find available port
            while (IsPortUsed(ohost, oport) && oport <= GamePortMax)
            {
                oport++;
            }

            return oport <= GamePortMax; //Make sure we didnt go over port limit
        }

        private string PickHost()
        {
            string best = config.LobbyHost;
            int lowest = CountGames(best);
            foreach (string host in config.GameHosts)
            {
                int count = CountGames(host);
                if (count < lowest)
                {
                    best = host;
                    lowest = count;
                }
            }
            return best;
        }

        public int GetMaxGames()
        {
            return GamePortMax - GamePortMin + 1;
        }

        public ServerMatchmaker Matchmaker { get { return matchmaker; } }
        public ushort GamePortMin { get { return config.GamePortMin; } }
        public ushort GamePortMax { get { return config.GamePortMax >= GamePortMin ? config.GamePortMax : GamePortMin; } }
        public WebServer Server { get { return WebServer.Get(); } }
        public WebClient Client { get { return WebClient.Get(); } }

        public static ServerLobby Get()
        {
            if (instance == null)
                instance = FindObjectOfType<ServerLobby>();
            return instance;
        }
    }

    public enum RoomState
    {
        None = 0,
        Waiting = 10,
        Playing = 20,
        Ended = 30,
    }

    [System.Serializable]
    public class LobbyGame
    {
        public ServerType type;
        public string game_id;
        public string server_host;
        public ushort server_port;
        public RoomState state;
        public ulong last_update;
        public bool hidden;
        public bool permanent;

        public string scene;    //First scene to load
        public string save;     //Save file to load
        public string custom;  //Custom data for game mode or other settings
        public string title;   //Game room title

        public string join_code; //For relay
        public bool remote = false; //If true, not on same server

        public List<ChatMsg> chats = new List<ChatMsg>();
        public List<LobbyPlayer> players = new List<LobbyPlayer>();
        public int players_found = 0;
        public int players_max = 0;
        public byte[] extra;

        public LobbyGame() { type = ServerType.None; game_id = ""; }
        public LobbyGame(ServerType type, string uid) { this.type = type; game_id = uid; }

        public LobbyPlayer GetPlayer(ulong client_id)
        {
            foreach (LobbyPlayer player in players)
            {
                if (player.client_id == client_id)
                    return player;
            }
            return null;
        }

        public LobbyPlayer GetPlayer(string user_id)
        {
            foreach (LobbyPlayer player in players)
            {
                if (player.user_id == user_id)
                    return player;
            }
            return null;
        }

        public LobbyPlayer GetHost()
        {
            if (players.Count > 0)
                return players[0];
            return null;
        }

        //Warning: This is the client_id on the lobby server, not game server (they arent the same)
        public bool HasPlayer(ulong client_id)
        {
            return GetPlayer(client_id) != null;
        }

        public bool HasPlayer(string user_id)
        {
            return GetPlayer(user_id) != null;
        }

        //Warning: This is the client_id on the lobby server, not game server (they arent the same)
        public bool IsHost(ulong client_id)
        {
            LobbyPlayer host = GetHost();
            return host != null && host.client_id == client_id;
        }

        public bool IsHost(string user_id)
        {
            LobbyPlayer host = GetHost();
            return host != null && host.user_id == user_id;
        }

        public void AddPlayer(LobbyPlayer player)
        {
            if (HasPlayer(player.client_id))
                return; //ID already connected

            RemovePlayer(player.user_id); //Remove dupplicate

            player.game_id = game_id;
            players.Add(player);
        }

        public void RemovePlayer(ulong client_id)
        {
            for (int i = players.Count - 1; i >= 0; i--)
            {
                if (players[i].client_id == client_id)
                {
                    players[i].game_id = "";
                    players.RemoveAt(i);
                }
            }
        }

        public void RemovePlayer(string user_id)
        {
            for (int i = players.Count - 1; i >= 0; i--)
            {
                if (players[i].user_id == user_id)
                {
                    players[i].game_id = "";
                    players.RemoveAt(i);
                }
            }
        }

        public void AddChat(ChatMsg chat)
        {
            chats.Add(chat);
            if (chats.Count > 20)
                chats.RemoveAt(0);
        }

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(game_id);
        }
    }

    [System.Serializable]
    public class GameRefresh
    {
        public bool valid;
        public LobbyGame game = null;

        public GameRefresh() { }
        public GameRefresh(LobbyGame g) { game = g; valid = (game != null); }
    }

    [System.Serializable]
    public class LobbyPlayer
    {
        public ulong client_id = 0;
        public string user_id;
        public string username;
        public string game_id = "";
        public ulong last_update = 0;

        public LobbyPlayer() { }
        public LobbyPlayer(string uid, string user) { user_id = uid; username = user; }

    }

    [System.Serializable]
    public class LobbyGameList
    {
        public LobbyGame[] data;

        public LobbyGameList() { }
        public LobbyGameList(LobbyGame[] data) { this.data = data; }

    }

    [System.Serializable]
    public class CreateGameData
    {
        public string title;
        public string scene;
        public string savefile;
        public int players_max = 8;
        public bool hidden;
        public byte[] extra;

        public CreateGameData() { }
        public CreateGameData(string t, string f, string s) { title = t; savefile = f; scene = s; }
    }

    [System.Serializable]
    public class StartGameData
    {
        public string game_uid;
        public string join_code;
    }

    [System.Serializable]
    public class ChatMsg
    {
        public string username;
        public string text;
        public int index;

        public ChatMsg() { }
        public ChatMsg(string u, string t) { username = u; text = t; }
    }

    [System.Serializable]
    public class KeepMsg
    {
        public string game_uid;
        public string[] user_list; //user_id

        public KeepMsg() { }
        public KeepMsg(string uid, string[] list) { game_uid = uid; user_list = list; }
    }
}
