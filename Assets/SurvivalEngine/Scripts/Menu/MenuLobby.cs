using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NetcodePlus;
using UnityEngine.UI;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace SurvivalEngine
{
    public class MenuLobby : MonoBehaviour
    {
        private static MenuLobby instance;

        private void Awake()
        {
            instance = this;
        }

        void Start()
        {
            WorldData.Unload();
            SettingData.LoadLast();
            Menu.last_menu = SceneManager.GetActiveScene().name;

            LobbyConnectPanel.Get().Show();
        }

        public void HideAllPanels()
        {
            LobbyPanel.Get().Hide();
            LobbyConnectPanel.Get().Hide();
            LobbyCreatePanel.Get().Hide();
            LobbyRoomPanel.Get().Hide();
            ConnectingPanel.Get().Hide();
        }

        //Create a game on the lobby (for others to join)
        public void CreateGame(string title, string file, string scene)
        {
            CreateGameData cdata = ClientLobby.Get().GetCreateData(title, file, scene);
            CreateGameTask(cdata);
        }

        private async void CreateGameTask(CreateGameData cdata)
        {
            await ClientLobby.Get().CreateGame(cdata);
        }

        //Connect to previously created game
        public void ConnectToGame(LobbyGame game)
        {
            if (game != null)
            {
                ConnectToGameTask(game);
            }
        }

        private async void ConnectToGameTask(LobbyGame game)
        {
            HideAllPanels();
            ConnectingPanel.Get().Show();

            Debug.Log("Connecting to game: " + game.game_id + " " + game.server_host + ":" + game.server_port + " " + game.scene);

            await TimeTool.Delay(500);

            bool host = game.IsHost(ClientLobby.Get().UserID);
            if (host)
                await ConnectToGameHost(game);
            else
                await ConnectToGameJoin(game);
        }

        private async Task ConnectToGameHost(LobbyGame game)
        {
            int tries = 0;
            ClientLobby client = ClientLobby.Get();
            while (client != null && client.CanConnectToGame() && tries < 10)
            {
                await TimeTool.Delay(1000);
                await ClientLobby.Get().ConnectToGame(game); //Try connect again
                tries++;
            }
        }

        private async Task ConnectToGameJoin(LobbyGame game)
        {
            await TimeTool.Delay(3000); //Wait for host to connect

            int tries = 0;
            ClientLobby client = ClientLobby.Get();
            while (client != null && client.CanConnectToGame() && tries < 10)
            {
                await TimeTool.Delay(1000);
                await ClientLobby.Get().ConnectToGame(game); //Try connect again
                tries++;
            }
        }

        public void OnClickSwitch()
        {
            Menu.GoToSimpleMenu();
        }

        public static MenuLobby Get()
        {
            return instance;
        }
    }
}