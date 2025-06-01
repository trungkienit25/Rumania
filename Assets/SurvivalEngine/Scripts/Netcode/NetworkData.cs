using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NetcodePlus
{
    /// <summary>
    /// Network config data (only one file)
    /// </summary>

    [CreateAssetMenu(fileName = "NetworkData", menuName = "Netcode/NetworkData", order = 0)]
    public class NetworkData : ScriptableObject
    {
        [Header("Game Server")]
        public ushort game_port = 7700;         //Port used by Netcode for game server (default)
        public int players_max = 8;             //Maximum number of players in each game
        public GameObject player_default;       //Default player prefab

        [Header("Lobby Server")]
        public LobbyType lobby_type;            //Dedicated lobby or Unity Lobby Services
        public string lobby_host = "127.0.0.1";  //Default url (or IP) where the lobby is located
        public ushort lobby_port = 7800;        //Port used by Netcode for Lobby Server
        public ServerType lobby_game_type = ServerType.DedicatedServer; //Which type of game server will the lobby create ?  The lobby itself is always dedicated.
        public int lobby_rooms_max = 10;                                   //Maximum number of rooms in lobby

        [Header("Server Launcher")]            //For lobby in dedicated server mode only
        public ushort game_port_min = 7700;     //If game server is created by lobby, port will be selected in this range (first game server is 7700, second is 7701...)
        public ushort game_port_max = 7799;     //If game server is created by lobby, port will be selected in this range (first game server is 7700, second is 7701...)
        public string game_path_windows = "../ServerGame/Survival Engine Online.exe"; //Absolute path, unless it starts with ./ or .. , then Relative to Application.dataPath
        public string game_path_linux = "/server/game/ServerGame.x86_64"; //Absolute path, unless it starts with ./ or .. , then Relative to Application.dataPath
        public string[] game_hosts;             //Url of available game servers, if this is empty, it will use the same as lobby_url instead

        [Header("Authentication")]
        public AuthenticatorType auth_type = AuthenticatorType.Test; //Change this based on the platform you are building to
        public bool auth_auto_logout = false;                       //If true, will auto-logout from UnityServices, useful to test different users on many windows of same PC 

        public string GetExePath()
        {
            string path = GetExePathSetting();
            string fullpath = path;
            if (path.StartsWith(".."))
                fullpath = System.IO.Path.Combine(Application.dataPath, path);
            else if (path.StartsWith("./"))
                fullpath = System.IO.Path.Combine(Application.dataPath, path.Remove(0, 1));
            return fullpath;
        }

        public string GetExePathSetting()
        {
#if UNITY_STANDALONE_WIN
            return game_path_windows;
#elif UNITY_STANDALONE_LINUX
            return game_path_linux;
#else
            return "";
#endif
        }

        public static NetworkData Get()
        {
            TheNetwork net = TheNetwork.Get();
            if (net != null && net.data)
                return net.data;
            return null;
        }
    }
}