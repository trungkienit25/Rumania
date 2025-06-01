using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace NetcodePlus
{
    [DefaultExecutionOrder(-10)]
    public class TheNetwork : MonoBehaviour
    {
        public NetworkData data;

        //Server & Client events
        public UnityAction onTick; //Every network tick
        public UnityAction onReady;  //Event after connection fully established (save file sent, scene loaded...)
        public UnityAction onBeforeReady;  //First OnReady event called before all other OnReady
        public UnityAction onAfterReady;  //Last OnReady event called after all other OnReady
        public UnityAction onConnect;  //Event when self connect, happens before onReady, before sending any data
        public UnityAction onDisconnect; //Event when self disconnect
        public UnityAction<string> onBeforeChangeScene; //Before Changing Scene
        public UnityAction<string> onAfterChangeScene; //After Changing Scene

        public delegate bool ReadyCheckEvent();
        public ReadyCheckEvent checkReady; //Additional optional ready validations

        public UnityAction<int, FastBufferReader> onReceivePlayer;    //Server receives data from client after connection
        public UnityAction<int, FastBufferWriter> onSendPlayer;       //Client send data to server after connection
        public UnityAction<FastBufferReader> onReceiveWorld;    //Client receives data from server after connection
        public UnityAction<FastBufferWriter> onSendWorld;       //Server sends data to client after connection

        //Server only events
        public UnityAction<ulong> onClientJoin; //Server event when any client connect
        public UnityAction<ulong> onClientQuit; //Server event when any client disconnect
        public UnityAction<ulong> onClientReady; //Server event when any client become ready
        public UnityAction<string> onSaveRequest; //Server event when a player asks to save

        public UnityAction<ulong, ConnectionData> onClientApproved; //Called when a new client was succesfully approved
        public UnityAction<ulong, int> onAssignPlayerID;            //Server event after assigning id (client_id, player_id)
        public UnityAction<int, SNetworkObject> onBeforePlayerSpawn; //Server event before a player spawn (player_id, player object)
        public UnityAction<int, SNetworkObject> onSpawnPlayer; //Server event after a player is spawned (player_id, player object)

        public delegate bool ApprovalEvent(ulong client_id, ConnectionData connect_data);
        public ApprovalEvent checkApproval; //Additional approval validations for when a client connects

        public delegate Vector3 Vector3Event(int player_id);
        public Vector3Event findPlayerSpawnPos; //Find player spawn position for client
        public Vector3Event findPlayerSpawnRot; //Find player spawn position for client

        public delegate GameObject PrefabEvent(int player_id);
        public PrefabEvent findPlayerPrefab; //Find player prefab for client

        public delegate int IntEvent(ulong client_id);
        public IntEvent findPlayerID; //Find player ID for client

        //---------

        private NetworkManager network;
        private UnityTransport transport;
        private ConnectionData connection;
        private NetworkMessaging messaging;
        private Authenticator auth;
        private UnityAction refresh_callback;

        private Dictionary<ulong, ClientData> client_list = new Dictionary<ulong, ClientData>();
        private Dictionary<ulong, GameObject> prefab_list = new Dictionary<ulong, GameObject>();
        private Dictionary<ulong, SNetworkObject> players_list = new Dictionary<ulong, SNetworkObject>();
        private List<ulong> client_ready_list = new List<ulong>();

        private int player_id = -1;
        private bool changing_scene = false;            //Is loading a new scene
        private bool world_received = false;            //Save file has been received
        private bool offline_mode = false;                 //If true, means its in singleplayer mode
        private ClientState local_state = ClientState.Offline;
        private ServerType server_type = ServerType.None; //This value is only accurate on the server (client doesnt know this)
        private LobbyGame current_game = null;          //Saves lobby data for future ref
        private float update_timer = 0f;

        [System.NonSerialized]
        private static bool inited = false;
        private static TheNetwork instance;

        private const string listen_all = "0.0.0.0";
        private const int msg_size = 1024 * 1024 * 100;

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return; //Manager already exists, destroy this one
            }

            Init();
            RegisterDefaultPrefabs();
            DontDestroyOnLoad(gameObject);
        }

        public void Init()
        {
            if (!inited || transport == null)
            {
                instance = this;
                inited = true;
                network = GetComponent<NetworkManager>();
                transport = GetComponent<UnityTransport>();
                connection = new ConnectionData();
                messaging = new NetworkMessaging(this);

                transport.ConnectionData.ServerListenAddress = listen_all;
                transport.ConnectionData.Address = listen_all;

                network.ConnectionApprovalCallback += ApprovalCheck;
                network.OnClientConnectedCallback += OnClientConnect;
                network.OnClientDisconnectCallback += OnClientDisconnect;

                InitAuthentication();
            }
        }

        void Update()
        {
            float refresh_duration = 1f;
            update_timer += Time.deltaTime;
            if (update_timer > refresh_duration)
            {
                update_timer = 0f;
                SlowUpdate(refresh_duration);
            }
        }

        private void SlowUpdate(float delta)
        {
            CheckIfPlayerAssigned();
            CheckIfReady();
            auth?.Update(delta);
        }

        private async void InitAuthentication()
        {
            auth = Authenticator.Create(data.auth_type);
            await auth.Initialize();
        }

        //Start simulated host with all networking turned off
        public void StartHostOffline()
        {
            Debug.Log("Host Offline");
            ResetValues();
            offline_mode = true;
            server_type = ServerType.None;
            network.Shutdown();
            CreateClient(ClientID, auth.UserID, auth.Username);
            AfterConnected();
            OnHostConnect(ClientID);
        }

        //Start a host (client + server)
        public void StartHost(ushort port)
        {
            if (local_state != ClientState.Offline)
                return;
            if (!auth.IsConnected())
                return; //Not logged in

            Debug.Log("Host Game");
            ResetValues();
            server_type = ServerType.PeerToPeer;
            connection.user_id = auth.UserID;
            connection.username = auth.Username;
            connection.is_host = true;

            transport.SetConnectionData(listen_all, port);
            network.NetworkConfig.ConnectionData = NetworkTool.NetSerialize(connection);

            network.StartHost();
            CreateClient(ClientID, auth.UserID, auth.Username);
            AfterConnected();
            OnHostConnect(ClientID);
        }

        //Start a dedicated server
        public void StartServer(ushort port)
        {
            if (local_state != ClientState.Offline)
                return;

            Debug.Log("Create Game Server");

            ResetValues();
            server_type = ServerType.DedicatedServer;
            connection.user_id = "";
            connection.username = "";

            transport.SetConnectionData(listen_all, port);
            network.NetworkConfig.ConnectionData = NetworkTool.NetSerialize(connection);

            network.StartServer();
            AfterConnected();
            onConnect?.Invoke();
        }

        //If is_host is set to true, it means this player created the game on a dedicated server
        //so its still a client (not server) but is the one who selected game settings
        public void StartClient(string server_url, ushort port, bool is_host = false)
        {
            if (local_state != ClientState.Offline)
                return;
            if (!auth.IsConnected())
                return; //Not logged in

            Debug.Log("Join Game: " + server_url);
            ResetValues();
            server_type = ServerType.None; //Unknown, could be dedicated or peer2peer
            connection.user_id = auth.UserID;
            connection.username = auth.Username;
            connection.is_host = is_host;

            string ip = NetworkTool.HostToIP(server_url);
            transport.SetConnectionData(ip, port);
            network.NetworkConfig.ConnectionData = NetworkTool.NetSerialize(connection);

            network.StartClient();
            AfterConnected();
        }

        //Make sure the Unity Transport protocol is set to Relay when using Relay
        public void StartHostRelay(RelayConnectData relay)
        {
            if (local_state != ClientState.Offline)
                return;
            if (relay == null)
                return;
            if (!auth.IsConnected())
                return; //Not logged in

            Debug.Log("Host Relay Game");
            ResetValues();
            server_type = ServerType.RelayServer;
            connection.user_id = auth.UserID;
            connection.username = auth.Username;
            connection.is_host = true;

            transport.SetHostRelayData(relay.url, relay.port, relay.alloc_id, relay.alloc_key, relay.connect_data);
            network.NetworkConfig.ConnectionData = NetworkTool.NetSerialize(connection);

            network.StartHost();
            CreateClient(ClientID, auth.UserID, auth.Username);
            AfterConnected();
            OnHostConnect(ClientID);
        }

        public void StartClientRelay(RelayConnectData relay)
        {
            if (local_state != ClientState.Offline)
                return;
            if (relay == null)
                return;
            if (!auth.IsConnected())
                return; //Not logged in

            Debug.Log("Join Relay Game: " + relay.url + " Join Code: " + relay.join_code);
            ResetValues();
            server_type = ServerType.RelayServer;
            connection.user_id = auth.UserID;
            connection.username = auth.Username;

            transport.SetClientRelayData(relay.url, relay.port, relay.alloc_id, relay.alloc_key, relay.connect_data, relay.host_connect_data);
            network.NetworkConfig.ConnectionData = NetworkTool.NetSerialize(connection);

            network.StartClient();
            AfterConnected();
        }

        public void Disconnect()
        {
            if (!IsClient && !IsServer)
                return;

            Debug.Log("Disconnect");
            network.Shutdown();
            AfterDisconnected();
        }

        private void AfterConnected()
        {
            local_state = ClientState.Connecting;
            if (network.SceneManager != null)
                network.SceneManager.OnLoad += OnBeforeChangeScene;
            if (network.SceneManager != null)
                network.SceneManager.OnLoadComplete += OnAfterChangeScene;
            if (network.NetworkTickSystem != null)
                network.NetworkTickSystem.Tick += OnTick;
            messaging.ListenMsg("id", OnReceivePlayerID);
            messaging.ListenMsg("state", OnReceiveState);
            Messaging.ListenMsg("save", OnReceiveSave);
            Messaging.ListenMsg("request", OnReceiveRequest);
            Messaging.ListenMsg("world", OnReceiveWorld);
            Messaging.ListenMsg("player", OnReceivePlayerData);

        }

        private void AfterDisconnected()
        {
            if (local_state == ClientState.Offline)
                return;

            if (network.SceneManager != null)
                network.SceneManager.OnLoad -= OnBeforeChangeScene;
            if (network.SceneManager != null)
                network.SceneManager.OnLoadComplete -= OnAfterChangeScene;
            if (network.NetworkTickSystem != null)
                network.NetworkTickSystem.Tick -= OnTick;
            messaging.UnListenMsg("id");
            messaging.UnListenMsg("state");
            Messaging.UnListenMsg("save");
            Messaging.UnListenMsg("request");
            Messaging.UnListenMsg("world");
            Messaging.UnListenMsg("player");

            connection = new ConnectionData(); //Reset default
            client_list.Clear();
            client_ready_list.Clear();
            current_game = null;
            ResetValues();
            onDisconnect?.Invoke();
        }

        private void ResetValues()
        {
            local_state = ClientState.Offline;
            server_type = ServerType.None;
            offline_mode = false;
            changing_scene = false;
            world_received = false;
            player_id = -1; //-1 means not a player
        }

        private void OnHostConnect(ulong client_id)
        {
            ClientData client = GetClient(client_id);
            if (client == null)
                return;

            client.state = ClientState.Connecting;
            client.is_host = true;
            client.extra = connection.extra;
            world_received = true; //Host already has data

            onConnect?.Invoke();
        }

        private void OnClientConnect(ulong client_id)
        {
            if (IsServer && client_id != ServerID)
            {
                ClientData client = GetClient(client_id);
                if (client == null)
                    return;

                Debug.Log("Client Connected: " + client.client_id);

                //Ask for save file on dedicated server
                RequestWorldFrom(client);

                //Trigger join
                onClientJoin?.Invoke(client_id);
            }

            if (!IsServer)
            {
                onConnect?.Invoke(); //Connect wasn't called yet for client
            }
        }

        private void OnClientDisconnect(ulong client_id)
        {
            Debug.Log("Disconnecting: " + client_id);
            if (IsServer)
                onClientQuit?.Invoke(client_id);
            DespawnPlayer(client_id);
            RemoveClient(client_id);
            if (ClientID == client_id || client_id == ServerID)
                AfterDisconnected();
        }

        private void OnTick()
        {
            onTick?.Invoke();
        }

        private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest req, NetworkManager.ConnectionApprovalResponse res)
        {
            ConnectionData connect = NetworkTool.NetDeserialize<ConnectionData>(req.Payload);
            bool approved = ApproveClient(req.ClientNetworkId, connect);
            res.Approved = approved;

            if (req.ClientNetworkId != ServerID)
                Debug.Log("Client " + req.ClientNetworkId + " Approved: " + approved);
        }

        private bool ApproveClient(ulong client_id, ConnectionData connect)
        {
            if (client_id == ServerID)
                return true; //Server always approve itself

            if (offline_mode)
                return false;

            if (connect == null)
                return false; //Invalid data

            if (string.IsNullOrEmpty(connect.username) || string.IsNullOrEmpty(connect.user_id))
                return false; //Invalid username

            if (NetworkGame.Get() == null)
                return false; //Dont accept connection if game scene not loaded yet

            if (checkApproval != null && !checkApproval.Invoke(client_id, connect))
                return false; //Custom approval condition

            ClientData client = GetClientByUserID(connect.user_id);
            if (client != null)
                return false; //Client already connected with this user_id

            //Clear previous data
            RemoveClient(client_id);

            if (client_list.Count >= NetworkData.Get().players_max)
                return false; //Maximum number of clients

            Debug.Log("Approve connection: " + connect.user_id + " " + connect.username);
            ClientData nclient = CreateClient(client_id, connect.user_id, connect.username);
            nclient.is_host = connect.is_host;
            nclient.extra = connect.extra;

            onClientApproved?.Invoke(client_id, connect);
            return true; //New Client approved
        }

        public void SetLobbyGame(LobbyGame game)
        {
            current_game = game;
        }

        public void SetWorldReceived(bool received)
        {
            world_received = received;
        }

        public void SetConnectionExtraData(byte[] bytes)
        {
            connection.extra = bytes;
        }

        public void SetConnectionExtraData(string data)
        {
            connection.extra = NetworkTool.SerializeString(data);
        }

        public void SetConnectionExtraData<T>(T data) where T : INetworkSerializable, new()
        {
            connection.extra = NetworkTool.NetSerialize(data);
        }

        private void RegisterDefaultPrefabs()
        {
            RegisterPrefab(NetworkData.Get().player_default);
        }

        public void RegisterPrefab(GameObject prefab)
        {
            if (prefab != null)
            {
                SNetworkObject sobject = prefab.GetComponent<SNetworkObject>();
                if (sobject != null && !sobject.is_scene && !prefab_list.ContainsKey(sobject.prefab_id))
                {
                    prefab_list[sobject.prefab_id] = prefab;
                }

                NetworkObject nobject = prefab.GetComponent<NetworkObject>();
                if (nobject != null)
                {
                    network.PrefabHandler.AddHandler(prefab, new NetworkPrefabHandler(prefab));
                }
            }
        }

        public void UnRegisterPrefab(GameObject prefab)
        {
            if (prefab != null)
            {
                SNetworkObject sobject = prefab.GetComponent<SNetworkObject>();
                if (sobject != null && prefab_list.ContainsKey(sobject.prefab_id))
                {
                    prefab_list.Remove(sobject.prefab_id);
                }

                NetworkObject nobject = prefab.GetComponent<NetworkObject>();
                if (nobject != null)
                {
                    network.PrefabHandler.RemoveHandler(prefab);
                }
            }
        }

        public void SpawnPlayer(ulong client_id)
        {
            if (!IsServer)
                return;
            if (GetPlayerObject(client_id) != null)
                return; //Already Spawned
            ClientData client = GetClient(client_id);
            if (client == null)
                return; //Client not found
            if (client.player_id < 0)
                return; //Just an observer

            Vector3 pos = GetPlayerSpawnPos(client.player_id);
            Vector3 rot = GetPlayerSpawnRot(client.player_id);
            GameObject prefab = GetPlayerPrefab(client.player_id);
            if (prefab == null)
                return;

            Debug.Log("Spawn Player: " + client.user_id + " " + client.username + " " + client.player_id);

            GameObject player_obj = Instantiate(prefab, pos, Quaternion.Euler(rot));
            SNetworkObject player = player_obj.GetComponent<SNetworkObject>();
            players_list[client_id] = player;
            onBeforePlayerSpawn?.Invoke(client.player_id, player);
            player.Spawn(client_id);
            onSpawnPlayer?.Invoke(client.player_id, player);
        }

        //Use this function to spawn player manually (return null from the findPlayerPrefab event to prevent spawning automatically)
        //This function will only work if the player_id has already been assigned (after ready was called)
        public void SpawnPlayer(int player_id, GameObject prefab, Vector3 pos)
        {
            if (!IsServer)
                return;

            ClientData client = GetClientByPlayerID(player_id);
            if (client == null)
                return; //Client not found

            ulong client_id = client.client_id;
            if (GetPlayerObject(client_id) != null)
                return; //Already Spawned
            if (client.player_id < 0)
                return; //Just an observer

            Debug.Log("Spawn Player: " + client.user_id + " " + client.username + " " + client.player_id);

            GameObject player_obj = Instantiate(prefab, pos, prefab.transform.rotation);
            SNetworkObject player = player_obj.GetComponent<SNetworkObject>();
            players_list[client_id] = player;
            onBeforePlayerSpawn?.Invoke(client.player_id, player);
            player.Spawn(client_id);
            onSpawnPlayer?.Invoke(client.player_id, player);
        }

        private Vector3 GetPlayerSpawnPos(int player_id)
        {
            PlayerSpawn spawn = PlayerSpawn.Get(player_id); //Specific to this player_id
            if (spawn == null)
                spawn = PlayerSpawn.Get(); //Generic spawn position for all players
            Vector3 pos = spawn != null ? spawn.GetRandomPosition() : Vector3.zero;
            if (findPlayerSpawnPos != null)
                pos = findPlayerSpawnPos.Invoke(player_id);
            return pos;
        }

        private Vector3 GetPlayerSpawnRot(int player_id)
        {
            PlayerSpawn spawn = PlayerSpawn.Get(player_id); //Specific to this player_id
            if (spawn == null)
                spawn = PlayerSpawn.Get(); //Generic spawn position for all players
            Vector3 pos = spawn != null ? spawn.transform.rotation.eulerAngles : Vector3.forward;
            if (findPlayerSpawnRot != null)
                pos = findPlayerSpawnRot.Invoke(player_id);
            return pos;
        }

        private GameObject GetPlayerPrefab(int player_id)
        {
            if (findPlayerPrefab != null)
                return findPlayerPrefab.Invoke(player_id);
            return NetworkData.Get().player_default;
        }

        private int FindPlayerID(ulong client_id)
        {
            int player_id = (int)client_id;
            if (findPlayerID != null)
                player_id = findPlayerID.Invoke(client_id);
            return player_id;
        }

        public void DespawnPlayer(ulong client_id)
        {
            if (!IsServer)
                return;

            SNetworkObject player = GetPlayerObject(client_id);
            if (player != null)
            {
                players_list.Remove(client_id);
                player.Despawn(true);
            }
        }

        public void SpawnClientObjects(ulong client_id)
        {
            if (IsServer && client_id != ServerID)
            {
                NetworkGame.Get().Spawner.SpawnClientObjects(client_id);
            }
        }

        private void TriggerReady()
        {
            SetState(ClientState.Ready);
            onBeforeReady?.Invoke();
            TriggerReadyObjects();
            TriggerReadyPlayers();
            onReady?.Invoke();
            onAfterReady?.Invoke();
        }

        private void TriggerReadyObjects()
        {
            LinkedList<SNetworkObject> nobjs = SNetworkObject.GetAll();
            foreach (SNetworkObject obj in nobjs)
            {
                obj.TriggerReady();
            }
        }

        private void TriggerReadyPlayers()
        {
            if (IsServer && local_state == ClientState.Ready)
            {
                foreach (KeyValuePair<ulong, ClientData> pair in client_list)
                {
                    ClientData client = pair.Value;
                    if (client.state == ClientState.Ready)
                        TriggerReadyPlayer(client.client_id);
                }
            }
        }

        //This will be triggered when both the server and the player are ready
        private void TriggerReadyPlayer(ulong client_id)
        {
            if (IsServer && local_state == ClientState.Ready)
            {
                Debug.Log("Client is Ready:" + client_id);
                client_ready_list.Add(client_id);
                SpawnClientObjects(client_id);
                SpawnPlayer(client_id);
                onClientReady?.Invoke(client_id);
            }
        }

        private void CheckIfReady()
        {
            if (local_state != ClientState.Connecting || !IsConnected())
                return; //Wrong state, no need to check

            bool rvalid = checkReady == null || checkReady.Invoke(); //Custom condition
            bool pvalid = !IsClient || player_id >= 0; //Client has ID assigned
            bool gvalid = NetworkGame.Get() != null; //Game scene is loaded
            if (rvalid && pvalid && gvalid && !changing_scene && world_received)
            {
                Debug.Log("Ready!");
                TriggerReady();
            }
        }

        //Loop on connected player and assign a Player ID, will only work if the server is ready yet (world received)
        private void CheckIfPlayerAssigned()
        {
            if (IsServer && world_received)
            {
                foreach (KeyValuePair<ulong, ClientData> pair in client_list)
                {
                    ClientData client = pair.Value;
                    if (client.player_id < 0)
                        AssignPlayerID(client.client_id);
                }
            }
        }

        //This will be triggered when the server is ready and a player has joined
        private void AssignPlayerID(ulong client_id)
        {
            //Assign self before assigning other clients
            if (client_id == ServerID || local_state == ClientState.Ready)
            {
                ClientData client = GetClient(client_id);
                if (client != null && client.player_id < 0)
                {
                    int player_id = FindPlayerID(client_id);
                    if (player_id >= 0)
                    {
                        client.player_id = player_id;
                        Debug.Log("Player ID " + player_id + " assigned to: " + client.user_id + " " + client.username);

                        //If self, change this.player_id
                        if (client_id == ClientID)
                            this.player_id = player_id;

                        if (onAssignPlayerID != null)
                            onAssignPlayerID.Invoke(client_id, player_id);

                        //If not self, send the ID to client
                        if (client_id != ClientID)
                        {
                            messaging.SendInt("id", client_id, player_id, NetworkDelivery.ReliableSequenced);
                        }
                    }
                }
            }
        }

        //Call from server only
        public void LoadScene(string scene, bool force_reload = false)
        {
            bool reload = force_reload || scene != SceneManager.GetActiveScene().name;
            if (IsServer && reload && !changing_scene)
            {
                if (offline_mode)
                {
                    OnBeforeChangeScene(ClientID, scene, LoadSceneMode.Single, null);
                    SceneManager.LoadScene(scene);
                    OnAfterChangeScene(ClientID, scene, LoadSceneMode.Single);
                }
                else
                {
                    changing_scene = true;
                    local_state = ClientState.Connecting;
                    network.SceneManager.LoadScene(scene, LoadSceneMode.Single);

                    foreach (KeyValuePair<ulong, ClientData> client in client_list)
                        client.Value.state = ClientState.Connecting;
                }
            }
        }

        public void RestartScene()
        {
            if (IsServer)
            {
                string scene = SceneManager.GetActiveScene().name;
                if (IsOnline)
                    network.SceneManager.LoadScene(scene, LoadSceneMode.Single);
                else
                    SceneManager.LoadScene(scene);
            }
        }

        public void SetState(ClientState state)
        {
            local_state = state;
            if (!IsServer)
                messaging.SendInt("state", ServerID, (int)state, NetworkDelivery.Reliable);
            else
                ReceiveState(ServerID, local_state);
        }

        private void ReceiveState(ulong client_id, ClientState state)
        {
            if (IsServer)
            {
                ClientData client = GetClient(client_id);
                if (client != null && client.player_id >= 0)
                {
                    client.state = state;

                    if (local_state == ClientState.Ready && state == ClientState.Ready && client_id != ServerID)
                    {
                        TriggerReadyPlayer(client_id);
                    }
                }
            }
        }

        private void OnReceiveState(ulong client_id, FastBufferReader reader)
        {
            reader.ReadValueSafe(out int istate);
            ClientState state = (ClientState)istate;
            ReceiveState(client_id, state);
        }

        //Send save file to client_id (or to dedicated server)
        public void SendWorld(ulong client_id)
        {
            if (client_id == ClientID)
                return;

            Debug.Log("Send World Data to: " + client_id);
            FastBufferWriter writer = new FastBufferWriter(128, Allocator.Temp, MsgSizeMax);
            onSendWorld?.Invoke(writer); //Write the save file in this callback
            Messaging.Send("world", client_id, writer, NetworkDelivery.ReliableFragmentedSequenced);
            writer.Dispose();
        }

        private void OnReceiveWorld(ulong client_id, FastBufferReader reader)
        {
            if (IsServer && server_type != ServerType.DedicatedServer)
                return; //Servers cant receive world (except dedicated server)
            if (world_received)
                return; //Already received

            Debug.Log("Receive World Data from: " + client_id);
            world_received = true;
            onReceiveWorld?.Invoke(reader); //Read the save file in this callback

            refresh_callback?.Invoke();
            refresh_callback = null;
        }

        //Send player save file to server
        public void SendPlayerData()
        {
            if (IsServer)
                return; //No need to send on server

            Debug.Log("Send Player Data");
            FastBufferWriter writer = new FastBufferWriter(128, Allocator.Temp, MsgSizeMax);
            onSendPlayer?.Invoke(player_id, writer); //Write the save file in this callback
            Messaging.Send("player", ServerID, writer, NetworkDelivery.ReliableFragmentedSequenced);
            writer.Dispose();
        }

        private void OnReceivePlayerData(ulong client_id, FastBufferReader reader)
        {
            if (!IsServer)
                return; //Clients cant receive player data 

            ClientData client = GetClient(client_id);
            if (client == null || client.data_received)
                return; //Already received, or client invalid

            Debug.Log("Receive Player Data from: " + client_id);
            client.data_received = true;
            onReceivePlayer?.Invoke(client.player_id, reader); //Read the save file in this callback

            SendWorld(client_id); //Send back world
        }

        //Client asks the server to send back the up-to-date save file
        public void RequestWorld(UnityAction callback = null)
        {
            if (!IsServer)
            {
                world_received = false;
                refresh_callback = callback;
                Messaging.SendEmpty("request", ServerID, NetworkDelivery.Reliable);
            }
        }

        public void RequestWorldFrom(ClientData client)
        {
            if (!IsHost && ServerID != client.client_id && !world_received)
            {
                LobbyGame lgame = GetLobbyGame();
                bool first_client = !lgame.IsValid() && CountClients() == 1; //For direct connect when game_id is 0
                if (client.is_host || first_client)
                    Messaging.SendEmpty("request", client.client_id, NetworkDelivery.Reliable);
            }
        }

        //A "request" is asking for the save file
        private void OnReceiveRequest(ulong client_id, FastBufferReader reader)
        {
            SendWorld(client_id); //Send back world file to client
        }

        //Client asks the server to save the world (will be saved on the server)
        public void SaveWorld(string savefile)
        {
            if (!IsServer)
            {
                Messaging.SendString("save", ServerID, savefile, NetworkDelivery.Reliable);
            }
        }

        private void OnReceiveSave(ulong client_id, FastBufferReader reader)
        {
            if (IsServer && !IsClient)
            {
                reader.ReadValueSafe(out string savefile);
                if (!string.IsNullOrEmpty(savefile))
                    onSaveRequest?.Invoke(savefile);
            }
        }

        private void OnReceivePlayerID(ulong client_id, FastBufferReader reader)
        {
            reader.ReadValueSafe(out player_id);
            if (player_id >= 0)
            {
                SendPlayerData();
            }
        }

        private void OnBeforeChangeScene(ulong client_id, string scene, LoadSceneMode loadSceneMode, AsyncOperation async)
        {
            Debug.Log("Change Scene: " + scene);
            local_state = ClientState.Connecting;
            changing_scene = true;
            onBeforeChangeScene?.Invoke(scene);
        }

        private void OnAfterChangeScene(ulong client_id, string scene, LoadSceneMode loadSceneMode)
        {
            if (client_id == ClientID)
                Debug.Log("Completed Load Scene: " + scene);

            if (ClientID == client_id)
            {
                changing_scene = false;
                onAfterChangeScene?.Invoke(scene);
            }
        }

        private ClientData CreateClient(ulong client_id, string user_id, string username)
        {
            ClientData client = new ClientData(client_id);
            client.user_id = user_id;
            client.username = username;
            client.client_id = client_id;
            client.player_id = -1; //Not assigned yet
            client.state = ClientState.Connecting;
            client.data_received = client_id == ServerID;
            client_list[client_id] = client;
            return client;
        }

        private void RemoveClient(ulong client_id)
        {
            if (client_list.ContainsKey(client_id))
                client_list.Remove(client_id);
            client_ready_list.Remove(client_id);
        }

        public ClientData GetClient(ulong client_id)
        {
            if (client_list.ContainsKey(client_id))
                return client_list[client_id];
            return null;
        }

        public ClientData GetClientByUserID(string user_id)
        {
            foreach (ClientData client in client_list.Values)
            {
                if (client.user_id == user_id)
                    return client;
            }
            return null;
        }

        public ClientData GetClientByPlayerID(int player_id)
        {
            foreach (ClientData client in client_list.Values)
            {
                if (client.player_id == player_id)
                    return client;
            }
            return null;
        }

        public bool HasClient(ulong client_id)
        {
            return client_list.ContainsKey(client_id);
        }

        public ClientState GetClientState(ulong client_id)
        {
            ClientData client = GetClient(client_id);
            if (client != null)
                return client.state;
            return ClientState.Offline;
        }

        public GameObject GetPrefab(ulong prefab_id)
        {
            if (prefab_list.ContainsKey(prefab_id))
                return prefab_list[prefab_id];
            return null;
        }

        public SNetworkObject GetNetworkObject(ulong net_id)
        {
            return NetworkGame.Get().Spawner.GetSpawnedObject(net_id);
        }

        public SNetworkBehaviour GetNetworkBehaviour(ulong net_id, ushort behaviour_id)
        {
            SNetworkObject nobj = NetworkGame.Get().Spawner.GetSpawnedObject(net_id);
            if (nobj != null)
                return nobj.GetBehaviour(behaviour_id);
            return null;
        }

        public SNetworkObject GetPlayerObject(ulong client_id)
        {
            //Works on server only
            if (players_list.ContainsKey(client_id))
                return players_list[client_id];
            return null;
        }

        public int GetClientPlayerID(ulong client_id)
        {
            if (client_list.ContainsKey(client_id))
                return client_list[client_id].player_id;
            return -1;
        }

        public Dictionary<ulong, ClientData> GetClientsData()
        {
            return client_list;
        }

        public IReadOnlyList<ulong> GetClientsIds()
        {
            return network.ConnectedClientsIds;
        }

        public List<ulong> GetReadyClients()
        {
            return client_ready_list;
        }

        public int CountClients()
        {
            if (offline_mode)
                return 1;
            if (IsServer && IsConnected())
                return network.ConnectedClientsIds.Count;
            return 0;
        }

        public LobbyGame GetLobbyGame()
        {
            return current_game;
        }

        public ConnectionData GetConnectionData()
        {
            return connection;
        }

        public bool HasAuthority(NetworkObject obj)
        {
            return IsServer || ClientID == obj.OwnerClientId;
        }

        public bool IsGameScene()
        {
            return !changing_scene && NetworkGame.Get() != null; //NetworkGame shouldnt be in menus, just in the game scenes
        }

        public bool IsChangingScene()
        {
            return changing_scene;
        }

        public bool IsConnecting()
        {
            return IsActive() && !IsConnected(); //Trying to connect but not yet
        }

        public bool IsConnected()
        {
            return offline_mode || network.IsServer || network.IsConnectedClient;
        }

        public bool IsActive()
        {
            return offline_mode || network.IsServer || network.IsClient;
        }

        public bool IsReady()
        {
            return local_state == ClientState.Ready && IsConnected();
        }

        public string Address
        {
            get { return transport.ConnectionData.Address; }
            set { transport.ConnectionData.Address = value; }
        }

        public ushort Port
        {
            get { return transport.ConnectionData.Port; }
            set { transport.ConnectionData.Port = value; }
        }

        public ulong ClientID { get { return offline_mode ? ServerID : network.LocalClientId; } } //ID of this client (if host, will be same than ServerID), changes for every reconnection, assigned by Netcode
        public ulong ServerID { get { return NetworkManager.ServerClientId; } } //ID of the server

        public int PlayerID { get { return player_id; } }         //Player ID is specific to this game only and stays the same for this user when reconnecting (unlike clientID), assigned by NetcodePlus
        public string UserID { get { return auth.UserID; } }      //User ID is linked to authentication and not related to a specific game, assigned by the Authenticator
        public string Username { get { return auth.Username; } }  //Display name of the player

        public bool IsServer { get { return offline_mode || network.IsServer; } }
        public bool IsClient { get { return offline_mode || network.IsClient; } }
        public bool IsHost { get { return IsClient && IsServer; } }     //Host is both a client and server
        public bool IsOnline { get { return !offline_mode && IsActive(); } }

        public ClientState State { get { return local_state; } }
        public ServerType ServerType { get { return server_type; } }

        public NetworkTime LocalTime { get { return network.LocalTime; } }
        public NetworkTime ServerTime { get { return network.ServerTime; } }
        public float DeltaTick { get { return 1f / network.NetworkTickSystem.TickRate; } }

        public NetworkManager NetworkManager { get { return network; } }
        public UnityTransport Transport { get { return transport; } }
        public NetworkMessaging Messaging { get { return messaging; } }
        public Authenticator Auth { get { return auth; } }
        public NetworkSpawner Spawner { get { return NetworkGame.Get().Spawner; } }

        public static string ListenAll { get { return listen_all; } }
        public static int MsgSizeMax { get { return msg_size; } }
        public static int MsgSize => MsgSizeMax; //Old name

        public static TheNetwork GetIfExist()
        {
            return instance;
        }

        public static TheNetwork Get()
        {
            if (instance == null)
            {
                TheNetwork net = FindObjectOfType<TheNetwork>();
                net?.Init();
            }
            return instance;
        }
    }

    [System.Serializable]
    public class ConnectionData : INetworkSerializable
    {
        public string user_id = "";
        public string username = "";
        public bool is_host = false; //Client created the game? (Could be true for client that created the game on dedicated server)

        public byte[] extra = new byte[0];

        //If you add extra data, make sure the total size of ConnectionData doesn't exceed Netcode max unfragmented msg (1400 bytes)
        //Fragmented msg are not possible for connection data, since connection is done in a single request

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref user_id);
            serializer.SerializeValue(ref username);
            serializer.SerializeValue(ref is_host);
            serializer.SerializeValue(ref extra);
        }
    }
    
    public enum LobbyType
    {
        Dedicated = 0,
        UnityServices = 10,
    }

    public enum ServerType
    {
        None = 0,
        DedicatedServer = 10,
        RelayServer = 20,
        PeerToPeer = 30, //Requires Port Forwarding for the host
    }

    public enum ClientState
    {
        Offline = 0,    //Not connected
        Connecting = 5, //Waiting to change scene or receive world data
        Ready = 10,       //Everything is loaded and spawned
    }

    public class ClientData
    {
        public ulong client_id;
        public int player_id;
        public string user_id;
        public string username;
        public bool is_host;        //Doesn't necessarily mean its the server host, just that its the one that created the game
        public bool data_received;  //Data was received from this user
        public ClientState state = ClientState.Offline;
        public byte[] extra = new byte[0];

        public ClientData() { }
        public ClientData(ulong client_id)
        {
            this.client_id = client_id;
        }

        public string GetExtraString()
        {
            return NetworkTool.DeserializeString(extra);
        }

        public T GetExtraData<T>() where T : INetworkSerializable, new()
        {
            return NetworkTool.NetDeserialize<T>(extra);
        }
    }
}
