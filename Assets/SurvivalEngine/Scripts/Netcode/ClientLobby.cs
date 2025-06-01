using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

namespace NetcodePlus
{
    /// <summary>
    /// Base class for ClientLobbyService and ClientLobby
    /// </summary>

    public abstract class ClientLobby : MonoBehaviour
    {
        public UnityAction<bool> onConnect;
        public UnityAction<LobbyGameList> onRefreshList;
        public UnityAction<LobbyGame> onRefresh;
        public UnityAction<LobbyGame> onMatchmaking;

        protected RelayConnectData relay_data = null;
        protected ulong client_id;
        protected string joined_game_id;
        protected byte[] extra_data = new byte[0];
        protected bool connected = false;

        protected MatchmakingRequest current_matchmaking = null;
        protected float matchmaking_timer = 0f;
        protected float refresh_timer = 0f;
        protected float keep_timer = 0f;

        private static ClientLobby instance;

        protected virtual void Update()
        {
            if (!connected)
                return;

            //Refresh lobby
            refresh_timer += Time.deltaTime;
            if (refresh_timer > 1f)
            {
                refresh_timer = 0;
                RefreshGame();
            }

            //Keep alive connection
            keep_timer += Time.deltaTime;
            if (keep_timer > 8f)
            {
                keep_timer = 0;
                KeepAlive();
            }

            //Refresh matchmaking
            matchmaking_timer += Time.deltaTime;
            if (IsMatchmaking() && matchmaking_timer > 2f)
            {
                matchmaking_timer = 0f;
                RefreshMatchmaking();
            }
        }

        public virtual async Task<bool> Connect()
        {
            await TimeTool.Delay(0);
            return false;
        }

        public void SetConnectionExtraData(byte[] bytes)
        {
            extra_data = bytes;
        }

        public void SetConnectionExtraData(string data)
        {
            extra_data = System.Text.Encoding.UTF8.GetBytes(data);
        }

        public void SetConnectionExtraData<T>(T data) where T : INetworkSerializable, new()
        {
            extra_data = NetworkTool.NetSerialize(data);
        }

        public virtual async void RefreshLobby()
        {
            await TimeTool.Delay(0);
        }

        public virtual async void RefreshGame()
        {
            await TimeTool.Delay(0);
        }

        public virtual async Task CreateGame(CreateGameData cdata)
        {
            await TimeTool.Delay(0);
        }
        
        public virtual async void JoinGame(string game_id)
        {
            await TimeTool.Delay(0);
        }

        public virtual async void QuitGame()
        {
            await TimeTool.Delay(0);
        }

        public virtual async void StartGame()
        {
            await TimeTool.Delay(0);
        }

        public virtual async void SendChat(string text)
        {
            await TimeTool.Delay(0);
        }

        public virtual async void KeepAlive()
        {
            await TimeTool.Delay(0);
        }

        public virtual async void KeepAlive(string game_id, string[] user_list)
        {
            await TimeTool.Delay(0);
        }

        //Only players requesting the same group will be matched
        public virtual void StartMatchmaking(string group)
        {
            //Scene will need to be defined by ServerMatchmaker with onMatchmaking
            StartMatchmaking(group, "", NetworkData.Get().players_max);
        }
        
        public virtual void StartMatchmaking(string group, string scene)
        {
            StartMatchmaking(group, scene, NetworkData.Get().players_max);
        }

        public virtual void StartMatchmaking(string group, string scene, int nb_players)
        {
            
        }

        public virtual async void StartMatchmaking(MatchmakingRequest req)
        {
            await TimeTool.Delay(0);
        }

        public virtual async void RefreshMatchmaking()
        {
            await TimeTool.Delay(0);
        }

        public virtual async void CancelMatchmaking()
        {
            await TimeTool.Delay(0);
        }

        public virtual async void Disconnect()
        {
            await TimeTool.Delay(0);
        }

        protected virtual void ReceiveMatchmakingResult(LobbyGame result)
        {
            
        }

        public virtual async Task ConnectToGame(LobbyGame game)
        {
            if (!CanConnectToGame())
                return;

            TheNetwork.Get().Disconnect(); //Disconnect previous connections
            await TimeTool.Delay(500);

            if (!connected)
                return;

            bool host = game.IsHost(client_id);
            TheNetwork.Get().SetConnectionExtraData(extra_data);
            TheNetwork.Get().SetLobbyGame(game);

            if (game.type == ServerType.DedicatedServer)
            {
                TheNetwork.Get().StartClient(game.server_host, game.server_port, host);
            }
            else if (game.type == ServerType.PeerToPeer)
            {
                if (host)
                {
                    TheNetwork.Get().StartHost(game.server_port);
                    TheNetwork.Get().LoadScene(game.scene);
                }
                else
                {
                    TheNetwork.Get().StartClient(game.server_host, game.server_port);
                }
            }
            else if (game.type == ServerType.RelayServer)
            {
                if (host)
                {
                    TheNetwork.Get().StartHostRelay(relay_data); //Relay data was already accessed when creating the game
                    TheNetwork.Get().LoadScene(game.scene);
                }
                else
                {
                    Debug.Log("RELAY CLIENT CODE: " + game.join_code);
                    RelayConnectData data = await NetworkRelay.JoinGame(game.join_code); //Relay data need to be retrieved now
                    TheNetwork.Get().StartClientRelay(data);
                }
            }

            while (TheNetwork.Get().IsConnecting())
                await TimeTool.Delay(200);
        }

        public CreateGameData GetCreateData(string title, string filename, string scene)
        {
            CreateGameData cdata = new CreateGameData(title, filename, scene);
            cdata.players_max = NetworkData.Get().players_max;
            cdata.hidden = false; //Game not hidden in lobby list
            return cdata;
        }

        public virtual bool IsMatchmaking()
        {
            return current_matchmaking != null;
        }

        public virtual bool CanConnectToGame() { return connected && !TheNetwork.Get().IsConnected(); }
        public virtual bool IsConnected() { return connected; }
        public virtual bool IsInLobby() { return connected && !string.IsNullOrEmpty(joined_game_id); }

        public string UserID { get { return Authenticator.Get().UserID; } }
        public string Username { get { return Authenticator.Get().Username; } }
        public ulong ClientID { get { return client_id; } }

        public static ClientLobby Get()
        {
            if (instance == null)
            {
                if (NetworkData.Get().lobby_type == LobbyType.Dedicated)
                    instance = ClientLobbyDedicated.Get();
                if (NetworkData.Get().lobby_type == LobbyType.UnityServices)
                    instance = ClientLobbyService.Get();
            }
            return instance;
        }
    }
}
