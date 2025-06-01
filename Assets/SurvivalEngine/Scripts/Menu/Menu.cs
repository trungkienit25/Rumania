using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using NetcodePlus;
using UnityEngine.SceneManagement;

namespace SurvivalEngine
{

    public class Menu : MonoBehaviour
    {
        [Header("Panels")]
        public UIPanel main_panel;
        public UIPanel create_panel;
        public UIPanel load_panel;
        public UIPanel join_panel;

        [Header("Create")]
        public Text create_ip;
        public InputField create_user;
        public InputField create_world;
        public OptionSelector create_scene;
        public OptionSelector create_character;

        [Header("Load")]
        public InputField load_user;
        public InputField load_world;
        public OptionSelector load_character;

        [Header("Join")]
        public InputField join_user;
        public InputField join_host;
        public OptionSelector join_character;

        private ushort port;

        public static string username = null;
        public static string character = null;
        public static string last_menu = "Menu";

        private static Menu instance;

        private void Awake()
        {
            instance = this;
            WorldData.Unload();      //Unload current save in menu
            SettingData.LoadLast(); //Load personal settings like music volume
        }

        void Start()
        {
            foreach (PlayerChoiceData player in PlayerChoiceData.GetAll())
            {
                create_character.AddOption(player.id, player.title);
                load_character.AddOption(player.id, player.title);
                join_character.AddOption(player.id, player.title);
            }

            create_character.SetRandomValue();
            load_character.SetRandomValue();
            join_character.SetRandomValue();

            join_character.SetIndex(1);
            main_panel.Show();
            port = NetworkData.Get().game_port;
            create_ip.text = "LAN IP: " + NetworkTool.GetLocalIp();
            last_menu = SceneManager.GetActiveScene().name;

            LoadUser();
        }

        private void SaveUser(string user, string character)
        {
            Menu.username = user;
            Menu.character = character;
        }

        private void LoadUser()
        {
            string name = Menu.username;
            if (name == null)
                name = GameData.Get().GetRandomName();

            create_user.text = name;
            load_user.text = name;
            join_user.text = name;

            string character = Menu.character;
            if (character != null)
            {
                create_character.SetValue(character);
                load_character.SetValue(character);
                join_character.SetValue(character);
            }
        }

        public void OnClickGoToCreate()
        {
            main_panel.Hide();
            create_panel.Show();
        }

        public void OnClickGoToLoad()
        {
            main_panel.Hide();
            load_panel.Show();
        }

        public void OnClickGoToJoin()
        {
            main_panel.Hide();
            join_panel.Show();
        }

        public void OnClickGoToMain()
        {
            main_panel.Show();
            create_panel.Hide();
            load_panel.Hide();
            join_panel.Hide();
        }

        public void OnClickCreate()
        {
            CreateGame(create_user.text, create_world.text, create_scene.GetSelectedValue(), create_character.GetSelectedValue());
        }

        public void OnClickLoad()
        {
            LoadGame(load_user.text, load_world.text, load_character.GetSelectedValue());
        }

        public void OnClickJoin()
        {
            JoinGame(join_user.text, join_host.text, join_character.GetSelectedValue());
        }

        public void CreateGame(string user, string savefile, string scene, string character)
        {
            if (SceneNav.DoSceneExist(scene) && SaveTool.IsValidFilename(savefile) && SaveTool.IsValidFilename(user))
            {
                SaveUser(user, character);
                CreateTask(user, savefile, scene, character);
            }
        }

        public void LoadGame(string user, string savefile, string character)
        {
            if (WorldData.HasSave(savefile) && SaveTool.IsValidFilename(user))
            {
                SaveUser(user, character);
                LoadTask(user, savefile, character);
            }
        }

        public void JoinGame(string user, string host, string character)
        {
            if (SaveTool.IsValidFilename(user))
            {
                SaveUser(user, character);
                JoinTask(user, host, character);
            }
        }

        private async void CreateTask(string user, string savefile, string scene, string character)
        {
            TheNetwork.Get().Disconnect();
            BlackPanel.Get().Show();
            await Task.Yield(); //Wait a frame after the disconnect
            Authenticator.Get().LoginTest(user);
            PlayerData.NewOrLoad(Authenticator.Get().UserID, Authenticator.Get().Username);
            TheNetwork.Get().SetConnectionExtraData(character);
            TheNetwork.Get().StartHost(port);
            TheGame.NewGame(savefile, scene);
        }

        private async void LoadTask(string user, string savefile, string character)
        {
            PlayerData.NewOrLoad(user);
            TheNetwork.Get().Disconnect();
            BlackPanel.Get().Show();
            await Task.Yield(); //Wait a frame after the disconnect
            Authenticator.Get().LoginTest(user);
            PlayerData.NewOrLoad(Authenticator.Get().UserID, Authenticator.Get().Username);
            TheNetwork.Get().SetConnectionExtraData(character);
            TheNetwork.Get().StartHost(port);
            TheGame.Load(savefile);
        }

        private async void JoinTask(string user, string host, string character)
        {
            TheNetwork.Get().Disconnect();
            ConnectingPanel.Get().Show();
            await Task.Yield(); //Wait a frame after the disconnect
            Authenticator.Get().LoginTest(user);
            PlayerData.NewOrLoad(Authenticator.Get().UserID, Authenticator.Get().Username);
            TheNetwork.Get().SetConnectionExtraData(character);
            TheNetwork.Get().StartClient(host, port);
        }

        public static void GoToSimpleMenu()
        {
            SceneManager.LoadScene("Menu");
        }

        public static void GoToLobbyMenu()
        {
            SceneManager.LoadScene("MenuLobby");
        }

        public static void GoToLastMenu()
        {
            SceneManager.LoadScene(last_menu);
        }

        public static Menu Get()
        {
            return instance;
        }
    }
}
