using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NetcodePlus
{
    public class ServerGame : MonoBehaviour
    {
        public string game_id = "";
        public string url = "127.0.0.1";
        public ushort port = 7700;
        public string save = "";
        public string scene = "Blank";
        public string custom = "";    //Custom value that can be used to determine the game mode or other settings
        public bool permanent = true; //If false, will auto shutdown when no more players
        public bool wait_save = true; //If true, will listen for first player to send the save file (instead of loading it locally)

        //Commands when starting server from command line (lobby server uses these to launch a server
        public const string game_id_id = "-game=";
        public const string url_id = "-url=";
        public const string port_id = "-port=";
        public const string save_id = "-save=";
        public const string scene_id = "-scene=";
        public const string custom_id = "-custom=";
        public const string permanent_id = "-permanent=";
        public const string wait_id = "-wait=";

        private float inactive_timer = 0f;

        private static ServerGame instance;

        void Awake()
        {
            instance = this;
        }

        private void Start()
        {
            TheNetwork network = TheNetwork.Get();
            if (network.IsActive())
                return; //Already connected, was probably changed to here from the network scene manager

            Application.runInBackground = true;
            Application.targetFrameRate = 60;

            LoadArguments();
            DontDestroyOnLoad(gameObject);

            if (string.IsNullOrEmpty(save))
                save = NetworkTool.GenerateRandomID(); //Generate random file name to allow saving
			
			//Store lobby data for future access
            LobbyGame lgame = new LobbyGame(ServerType.DedicatedServer, game_id);
            lgame.server_host = url;
            lgame.server_port = port;
            lgame.save = save;
            lgame.scene = scene;
            lgame.custom = custom;
            lgame.permanent = permanent;
            network.SetLobbyGame(lgame);    //Reference for future access
            network.StartServer(port); //listen on port
            network.SetWorldReceived(!wait_save); //If waiting save, world not received yet
			
			if (!string.IsNullOrEmpty(scene))
                network.LoadScene(scene); //If not switching now, will need to change scene in the gameplay after save was loaded
        }

        private void Update()
        {
            TheNetwork network = TheNetwork.Get();
            if (!network.IsServer)
                return; //Not server, ignore

            int nb = network.CountClients();

            if (nb == 0)
                inactive_timer += Time.deltaTime;
            else
                inactive_timer = 0f;

            if (!permanent && inactive_timer > 30f)
                Application.Quit(); //Quit server if no one connected
        }

        private void LoadArguments()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            string all_args = string.Join(" ", args);
            Debug.Log("Server Starting:\n" + all_args);

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith(game_id_id))
                {
                    string uid = args[i].Replace(game_id_id, "");
                    game_id = uid.Trim(new char[] { '"' });
                }
                else if (args[i].StartsWith(url_id))
                {
                    url = args[i].Replace(url_id, "");
                    url = url.Trim(new char[] { '"' });
                }
                else if (args[i].StartsWith(scene_id))
                {
                    scene = args[i].Replace(scene_id, "");
                    scene = scene.Trim(new char[] { '"' });
                }
                else if (args[i].StartsWith(save_id))
                {
                    save = args[i].Replace(save_id, "");
                    save = save.Trim(new char[] { '"' });
                }
                else if (args[i].StartsWith(custom_id))
                {
                    custom = args[i].Replace(custom_id, "");
                    custom = custom.Trim(new char[] { '"' });
                }
                else if (args[i].StartsWith(port_id))
                {
                    string port_s = args[i].Replace(port_id, "");
                    bool success = ushort.TryParse(port_s, out ushort aport);
                    if (success)
                        port = aport;
                }
                else if (args[i].StartsWith(permanent_id))
                {
                    string perm_s = args[i].Replace(permanent_id, "");
                    bool.TryParse(perm_s, out permanent);
                }
                else if (args[i].StartsWith(wait_id))
                {
                    string wait_s = args[i].Replace(wait_id, "");
                    bool.TryParse(wait_s, out wait_save);
                }
            }
        }

        public static ServerGame Get()
        {
            return instance;
        }
    }
}
