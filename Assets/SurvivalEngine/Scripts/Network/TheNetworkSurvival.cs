using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using NetcodePlus;
using Unity.Collections;
using UnityEngine.Events;

namespace SurvivalEngine
{
    /// <summary>
    /// Additional NetworkManager code specific to Survival Engine
    /// Mostly handles transfering the save file across the network, and also finds the player spawn prefabs/positions
    /// </summary>

    public class TheNetworkSurvival : MonoBehaviour
    {
        private static TheNetworkSurvival instance;

        private void Awake()
        {
            instance = this;
        }

        void Start()
        {
            TheNetwork network = TheNetwork.Get();
            network.checkApproval += CheckApprove;
            network.checkReady += CheckReady;
            network.onConnect += OnConnect;
            network.onSendWorld += OnSendWorld;
            network.onReceiveWorld += OnReceiveWorld;
            network.onSendPlayer += OnSendPlayer;
            network.onReceivePlayer += OnReceivePlayer;
            network.onSaveRequest += OnReceiveSave;
            network.findPlayerSpawnPos += FindPlayerSpawnPos;
            network.findPlayerPrefab += FindPlayerPrefab;
            network.findPlayerID += FindPlayerID;

            if(network.IsConnected())
                OnConnect(); //Run now if already connected
        }

        void OnDestroy()
        {
            TheNetwork network = TheNetwork.Get();
            network.checkApproval -= CheckApprove;
            network.checkReady -= CheckReady;
            network.onConnect -= OnConnect;
            network.onSendWorld -= OnSendWorld;
            network.onReceiveWorld -= OnReceiveWorld;
			network.onSendPlayer -= OnSendPlayer;
			network.onReceivePlayer -= OnReceivePlayer;
            network.onSaveRequest -= OnReceiveSave;
            network.findPlayerSpawnPos -= FindPlayerSpawnPos;
            network.findPlayerPrefab -= FindPlayerPrefab;
            network.findPlayerID -= FindPlayerID;
        }

        private bool CheckApprove(ulong client_id, ConnectionData connection)
        {
            WorldData sdata = WorldData.Get();
            return sdata != null && sdata.HasPlayerID(connection.user_id); //Check if there is a player_id available
        }

        private bool CheckReady()
        {
            return TheGame.IsValid(); //Scene and data loaded
        }

        private void OnConnect()
        {
            if (IsServer)
            {
                if (TheNetwork.Get().ServerType == ServerType.DedicatedServer)
                {
                    //Dedicated server
                    ServerGame sgame = ServerGame.Get();
                    if (sgame != null)
                        TheGame.NewOrLoad(sgame.save, sgame.scene);
                }
                else
                {
                    //Lobby in peer-to-peer mode
                    LobbyGame game = TheNetwork.Get().GetLobbyGame();
                    if (game != null)
                    {
                        TheGame.NewOrLoad(game.save, game.scene);
                    }
                }
            }
        }

        private void OnSendWorld(FastBufferWriter writer)
        {
            //Not using writer.WriteNetworkSerializable like everywhere else,
            //because a small change in the data structure of the save (like if loading an old save) would crash everything
            //instead, using NetworkTool.Serialize allow to be more flexible, and uses the same serialization as when saving to disk
            WorldData sdata = WorldData.Get();
            if (sdata != null)
            {
                byte[] bytes = NetworkTool.Serialize(sdata);
                writer.WriteValueSafe(bytes.Length);
                if(bytes.Length > 0)
                    writer.WriteBytesSafe(bytes, bytes.Length);
            }
            else
            {
                writer.WriteValueSafe(0);
            }
        }

        private void OnReceiveWorld(FastBufferReader reader)
        {
            //Not using reader.ReadNetworkSerializable like everywhere else,
            //because a small change in the data structure of the save (like if loading an old save) would crash everything
            //instead, using NetworkTool.Deserialize allow to be more flexible, and uses the same serialization as when saving to disk
            reader.ReadValueSafe(out int count);
            if (count > 0)
            {
                byte[] bytes = new byte[count];
                reader.ReadBytesSafe(ref bytes, count);
                WorldData sdata = NetworkTool.Deserialize<WorldData>(bytes);
                if (sdata != null)
                {
                    sdata.FixData(); //Fix old save file
                    WorldData.Override(sdata);
                }
            }
        }

        private void OnSendPlayer(int player_id, FastBufferWriter writer)
        {
            PlayerData sdata = PlayerData.GetLoaded();
            if (sdata != null && GameData.Get().save_type == GameSaveType.SplitSave)
            {
                byte[] bytes = NetworkTool.Serialize(sdata);
                writer.WriteValueSafe(bytes.Length);
                if (bytes.Length > 0)
                    writer.WriteBytesSafe(bytes, bytes.Length);
            }
            else
            {
                writer.WriteValueSafe(0);
            }
        }

        private void OnReceivePlayer(int player_id, FastBufferReader reader)
        {
            reader.ReadValueSafe(out int count);
            if (count > 0 && GameData.Get().save_type == GameSaveType.SplitSave)
            {
                byte[] bytes = new byte[count];
                reader.ReadBytesSafe(ref bytes, count);
                PlayerData sdata = NetworkTool.Deserialize<PlayerData>(bytes);
                if (sdata != null)
                {
                    sdata.FixData(); //Fix old save file
                    WorldData.Get().OverridePlayer(player_id, sdata);
                }
            }
        }

        //Client asks the server to save locally
        private void OnReceiveSave(string file)
        {
            WorldData sdata = WorldData.Get();
            sdata.Save(file);
        }

        private Vector3 FindPlayerSpawnPos(int player_id)
        {
            WorldData sdata = WorldData.Get();
            PlayerData player = PlayerData.Get(player_id);
            Vector3 pos = Vector3.zero;
            if (player != null)
                pos = player.position;

            if (player == null || !player.IsValidScene() || !sdata.IsValidScene())
            {
                PlayerSpawn spawn = PlayerSpawn.Get(sdata.entry);
                ExitZone zone = ExitZone.Get(sdata.entry);
                if (zone != null)
                    pos = zone.GetRandomPosition();
                else if (spawn != null)
                    pos = spawn.GetRandomPosition();
                else
                    pos = Vector3.zero;
            }
            return pos;
        }

        private GameObject FindPlayerPrefab(int player_id)
        {
            ClientData client = TheNetwork.Get().GetClientByPlayerID(player_id);
            if (client == null)
                return null;

            string character = client.GetExtraString();

            //Check character selected by player
            PlayerChoiceData player_data = PlayerChoiceData.Get(character);
            if (player_data != null)
                return player_data.prefab;

            //Check character saved in the save file
            PlayerData pdata = PlayerData.Get(client.player_id);
            if (pdata != null)
                player_data = PlayerChoiceData.Get(pdata.character);
            if (player_data != null)
                return player_data.prefab;

            return NetworkData.Get().player_default;
        }

        private int FindPlayerID(ulong client_id)
        {
            WorldData sdat = WorldData.Get();
            ClientData client = TheNetwork.Get().GetClient(client_id);
            if (client == null || sdat == null)
                return -1; //Client or world not found


            //Find player id by user id
            PlayerData id_player = sdat.GetPlayer(client.user_id);
            if (id_player != null)
            {
                id_player.filename = client.username;
                id_player.username = client.username;
                return id_player.player_id;
            }

            //Find player id by username
            PlayerData user_player = sdat.GetPlayerByUsername(client.username);
            if (user_player != null && TheNetwork.Get().GetClientByPlayerID(user_player.player_id) == null)
            {
                user_player.filename = client.username;
                user_player.user_id = client.user_id;
                return user_player.player_id; //Return only if no other user already connected with this username
            }

            //No player found, assign new player ID and set selected character
            int player_id = sdat.FindNewPlayerID(client.user_id, client.username, NetworkData.Get().players_max);
            PlayerData player = sdat.GetPlayer(player_id);
            if (player != null)
                player.character = client.GetExtraString();
            return player_id;
        }

        public bool IsServer { get { return TheNetwork.Get().IsServer; } }
        public bool IsClient { get { return TheNetwork.Get().IsClient; } }
        public ulong ServerID { get { return TheNetwork.Get().ServerID; } }
        public NetworkMessaging Messaging { get { return TheNetwork.Get().Messaging; } }

        public static TheNetworkSurvival Get()
        {
            if (instance == null)
                instance = FindObjectOfType<TheNetworkSurvival>();
            return instance;
        }
    }

}
