using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NetcodePlus;
using Unity.Netcode;

#if MAP_MINIMAP
using MapMinimap;
#endif

namespace SurvivalEngine
{
    /// <summary>
    /// Wrapper class for Map Minimap
    /// </summary>

    public class MapMinimapWrap : MonoBehaviour
    {

#if MAP_MINIMAP

        private HashSet<PlayerCharacter> inited_players = new HashSet<PlayerCharacter>();
        private int fog_count = 0;
        private float timer = 1f;

        static MapMinimapWrap()
        {
            MapData.getData += GetData;
            MapData.getDataId += GetDataId;
        }

        void Awake()
        {
            TheNetwork network = TheNetwork.Get();
            if (network != null)
            {
                network.onConnect += OnConnect;
                network.onDisconnect += OnDisconnect;
                network.onReady += OnReady;
                network.onSpawnPlayer += OnSpawnPlayer;

                if (network.IsConnected())
                    OnConnect(); //Run now if already connected
            }

            MapManager map_manager = FindObjectOfType<MapManager>();
            if (map_manager == null)
            {
                Debug.LogError("Map Minimap: Integration failed - Make sure to add the MapManager to the scene");
            }
        }

        private void OnDestroy()
        {
            TheNetwork network = TheNetwork.GetIfExist();
            if (network != null)
            {
                network.onConnect -= OnConnect;
                network.onDisconnect -= OnDisconnect;
                network.onReady -= OnReady;
                network.onSpawnPlayer -= OnSpawnPlayer;
            }
        }

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer > 1f)
            {
                timer = 0f;
                SlowUpdate();
            }
        }

        private void SlowUpdate()
        {
            foreach (PlayerCharacter player in PlayerCharacter.GetAll())
            {
                if (player != null && !inited_players.Contains(player) && player.IsSpawned)
                {
                    inited_players.Add(player);
                    InitPlayer(player);
                }
            }

            RefreshServer();
        }

        private void InitPlayer(PlayerCharacter player)
        {
            MapReveal reveal = player.GetComponent<MapReveal>();
            if(reveal != null)
                reveal.reveal_id = player.player_id;
        }

        private void OnConnect()
        {
            TheNetwork.Get().Messaging.ListenMsg("map_data", OnReceiveRefresh);
        }

        private void OnDisconnect()
        {
            TheNetwork.Get().Messaging.UnListenMsg("map_data");
        }

        private void OnReady()
        {
            MapManager map_manager = MapManager.Get();
            if (map_manager != null)
            {
                map_manager.reveal_id = TheNetwork.Get().PlayerID;
            }
        }

        private void OnSpawnPlayer(int player_id, SNetworkObject nobj)
        {
            PlayerCharacter player = PlayerCharacter.Get(player_id);
            MapReveal reveal = player?.GetComponent<MapReveal>();
            if (reveal != null)
                reveal.reveal_id = player_id;
        }

        //Send the MapData to server
        private void RefreshServer()
        {
            if (TheNetwork.Get().IsServer)
                return; //Only client can send

            MapData ndata = MapData.Get();
            if (ndata != null)
            {
                MapSceneData scene = ndata.GetSceneData();
                int count = scene.fog_reveal.Count;
                if (count == fog_count)
                    return; //No change

                fog_count = count;

                FastBufferWriter writer = new FastBufferWriter(1024, Unity.Collections.Allocator.Temp, TheNetwork.MsgSize);
                byte[] bytes = NetworkTool.Serialize(ndata);
                writer.WriteValueSafe(bytes.Length);
                if (bytes.Length > 0)
                    writer.WriteBytesSafe(bytes, bytes.Length);
                TheNetwork.Get().Messaging.SendBuffer("map_data", TheNetwork.Get().ServerID, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }

        //Server receives map data of player
        private void OnReceiveRefresh(ulong client_id, FastBufferReader reader)
        {
            if (!TheNetwork.Get().IsServer)
                return; //Only server can receive

            reader.ReadValueSafe(out int count);
            if (count > 0)
            {
                byte[] bytes = new byte[count];
                reader.ReadBytesSafe(ref bytes, count);
                MapData ndata = NetworkTool.Deserialize<MapData>(bytes);
                if (ndata != null)
                {
                    int player_id = TheNetwork.Get().GetClientPlayerID(client_id);
                    PlayerData pdata = PlayerData.Get(player_id);
                    if(pdata != null)
                        pdata.map_data = ndata;
                }
            }
        }

        private static MapData GetData()
        {
            PlayerData pdata = PlayerData.GetSelf();
            if(pdata != null)
                return pdata.map_data;
            return null;
        }

        private static MapData GetDataId(int player_id)
        {
            PlayerData pdata = PlayerData.Get(player_id);
            if(pdata != null)
                return pdata.map_data;
            return null;
        }

#endif

    }
}

