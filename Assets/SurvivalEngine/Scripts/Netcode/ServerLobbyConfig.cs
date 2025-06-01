using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NetcodePlus
{
    /// <summary>
    /// Stores NetworkData into a lobby.config file so that you can change those settings without the need to do another build each time
    /// (you still need to restart the lobby server when you change settings)
    /// </summary>

    public class ServerLobbyConfig
    {
        private const string filename = "lobby.config";

        private ServerType lobby_game_type;
        private bool lobby_enabled = true; //Set this to false secondary game-server instances, lobby is still needed to launch/stop game servers, but wont allow players to connect
        private string lobby_host;
        private ushort lobby_port;
        private int lobby_rooms_max;

        private string game_path;
        private ushort game_port;
        private ushort game_port_min;
        private ushort game_port_max;
        private int game_players_max;
        private bool game_wait_save;
        private string[] game_hosts;

        public ServerLobbyConfig(NetworkData ndata)
        {
            //Load default values
            lobby_game_type = ndata.lobby_game_type;
            lobby_enabled = true;
            lobby_host = ndata.lobby_host;
            lobby_port = ndata.lobby_port;
            lobby_rooms_max = ndata.lobby_rooms_max;
            game_path = ndata.GetExePath();
            game_port = ndata.game_port;
            game_port_min = ndata.game_port_min;
            game_port_max = ndata.game_port_max;
            game_players_max = ndata.players_max;
            game_wait_save = true;

            game_hosts = ndata.game_hosts;
            if (game_hosts == null || game_hosts.Length == 0)
                game_hosts = new string[1] { lobby_host };

            SaveLoadConfig();
            DisplayConfig();
        }

        private void SaveLoadConfig()
        {
            //Save or load config file
            string path = Application.dataPath + "/../" + filename;
            if (File.Exists(path))
            {
                string data = ReadString(path);
                ReadData(data);
            }
            //Uncomment this to auto-generate the config file
            /*else
            {
                string data = GenerateData();
                WriteString(path, data);
            }*/
        }

        private string GenerateData()
        {
            string data = "//Lobby Config\n\n";

            data += "lobby_enabled: " + lobby_enabled + "\n";
            data += "lobby_host: " + lobby_host + "\n";
            data += "lobby_port: " + lobby_port + "\n";
            data += "lobby_rooms: " + lobby_rooms_max + "\n";

            data += "game_path: " + game_path + "\n";
            data += "game_port: " + game_port + "\n";
            data += "game_port_min: " + game_port_min + "\n";
            data += "game_port_max: " + game_port_max + "\n";
            data += "game_players: " + game_players_max + "\n";
            data += "game_wait_save: " + game_wait_save + "\n";
            data += "game_hosts:\n";

            foreach (string url in game_hosts)
                data += url + "\n";

            return data;
        }

        private void ReadData(string data)
        {
            string current = "";
            List<string> urls = new List<string>();
            string[] lines = data.Split(new string[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.None);
            foreach (string aline in lines)
            {
                string line = aline.Trim();

                if (string.IsNullOrEmpty(line))
                    continue; //Empty line

                if (line.StartsWith("//"))
                    continue; //Comment

                if (line.StartsWith("lobby_enabled:"))
                {
                    current = "";
                    string val = line.Replace("lobby_enabled:", "").Trim();
                    if (!string.IsNullOrWhiteSpace(val))
                        bool.TryParse(val, out lobby_enabled);
                }

                if (line.StartsWith("lobby_host:"))
                {
                    current = "";
                    string val = line.Replace("lobby_host:", "").Trim();
                    if (!string.IsNullOrWhiteSpace(val))
                        lobby_host = val;
                }

                else if (line.StartsWith("lobby_port:"))
                {
                    current = "";
                    string val = line.Replace("lobby_port:", "").Trim();
                    bool valid = ushort.TryParse(val, out ushort ival);
                    if (valid)
                        lobby_port = ival;
                }

                else if (line.StartsWith("lobby_rooms:"))
                {
                    current = "";
                    string val = line.Replace("lobby_rooms:", "").Trim();
                    bool valid = int.TryParse(val, out int ival);
                    if (valid)
                        lobby_rooms_max = ival;
                }

                else if (line.StartsWith("game_path:"))
                {
                    current = "";
                    string val = line.Replace("game_path:", "").Trim();
                    if (!string.IsNullOrWhiteSpace(val))
                        game_path = val;
                }

                else if (line.StartsWith("game_port:"))
                {
                    current = "";
                    string val = line.Replace("game_port:", "").Trim();
                    bool valid = ushort.TryParse(val, out ushort ival);
                    if (valid)
                        game_port = ival;
                }

                else if (line.StartsWith("game_port_min:"))
                {
                    current = "";
                    string val = line.Replace("game_port_min:", "").Trim();
                    bool valid = ushort.TryParse(val, out ushort ival);
                    if (valid)
                        game_port_min = ival;
                }

                else if (line.StartsWith("game_port_max:"))
                {
                    current = "";
                    string val = line.Replace("game_port_max:", "").Trim();
                    bool valid = ushort.TryParse(val, out ushort ival);
                    if (valid)
                        game_port_max = ival;
                }

                else if (line.StartsWith("game_players:"))
                {
                    current = "";
                    string val = line.Replace("game_players:", "").Trim();
                    bool valid = int.TryParse(val, out int ival);
                    if (valid)
                        game_players_max = ival;
                }

                else if (line.StartsWith("game_wait_save:"))
                {
                    current = "";
                    string val = line.Replace("game_wait_save:", "").Trim();
                    bool valid = bool.TryParse(val, out bool bval);
                    if (valid)
                        game_wait_save = bval;
                }

                else if (line.StartsWith("game_hosts:"))
                {
                    current = "game_hosts";
                }
                else if (current == "game_hosts")
                {
                    urls.Add(line);
                }
            }

            game_hosts = urls.ToArray();
        }

        private void WriteString(string path, string data)
        {
            try
            {
                Debug.Log("Saving config to: " + path);
                File.WriteAllText(path, data);
            }
            catch (System.Exception) { Debug.LogError("Can't write config file (Check permissions?)"); }
        }

        private string ReadString(string path)
        {
            try
            {
                Debug.Log("Reading config from: " + path);
                string data = File.ReadAllText(path);
                return data;
            }
            catch (System.Exception)
            {
                Debug.LogError("Can't read config file (Check permissions?)");
                return "";
            }

        }

        private void DisplayConfig()
        {
            Debug.Log("--- Lobby Config ---");
            Debug.Log("ServerType: " + lobby_game_type);
            Debug.Log("LobbyEnabled: " + lobby_enabled);
            Debug.Log("LobbyHost: " + lobby_host);
            Debug.Log("LobbyPort: " + lobby_port);
            Debug.Log("LobbyRoomsMax: " + lobby_rooms_max);
            Debug.Log("GamePath: " + game_path);
            Debug.Log("GamePort: " + game_port);
            Debug.Log("GamePortMin: " + game_port_min);
            Debug.Log("GamePortMax: " + game_port_max);
            Debug.Log("WaitSave: " + game_wait_save);
            Debug.Log("PlayersMax: " + game_players_max);

            foreach (string url in game_hosts)
                Debug.Log("GameHosts: " + url);

            Debug.Log("--- --- ---");
        }

        public ServerType GameServerType { get { return lobby_game_type; } }
        public bool LobbyEnabled { get { return lobby_enabled; } }
        public string LobbyHost { get { return lobby_host; } }
        public ushort LobbyPort { get { return lobby_port; } }
        public int LobbyRoomsMax { get { return lobby_rooms_max; } }
        public string GamePath { get { return game_path; } }
        public ushort GamePort { get { return game_port; } }
        public ushort GamePortMin { get { return game_port_min; } }
        public ushort GamePortMax { get { return game_port_max; } }
        public int PlayersMax { get { return game_players_max; } }
        public bool WaitSave { get { return game_wait_save; } }
        public string[] GameHosts { get { return game_hosts; } }
    }
}
