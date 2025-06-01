using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NetcodePlus;
using System.Threading.Tasks;

namespace SurvivalEngine
{
    public class LobbyConnectPanel : UIPanel
    {
        public InputField username_input;
        public InputField password_input;
        public OptionSelector character_select;
        public Text error_txt;

        private static LobbyConnectPanel instance;

        protected override void Awake()
        {
            base.Awake();
            instance = this;
        }

        protected override void Update()
        {
            base.Update();

        }

        protected override void Start()
        {
            base.Start();

            foreach (PlayerChoiceData player in PlayerChoiceData.GetAll())
            {
                character_select.AddOption(player.id, player.title);
            }

            LoadUser();
        }

        private void SaveUser()
        {
            Menu.username = username_input.text;
            Menu.character = character_select.GetSelectedValue();
        }

        private void LoadUser()
        {
            if (Menu.username != null)
            {
                username_input.text = Menu.username;
                character_select.SetValue(Menu.character);
            }

            if (Menu.username == null)
            {
                string name = GameData.Get().GetRandomName();
                username_input.text = name;
                character_select.SetRandomValue();
            }
        }

        public void OnClickConnect()
        {
            if (!SaveTool.IsValidFilename(username_input.text))
                return;

            SaveUser();

            string user = username_input.text;
            string pass = password_input != null ? password_input.text : "";
            ConnectToLobby(user, pass);
        }

        private async void ConnectToLobby(string user, string pass)
        {
            ConnectingPanel.Get().Show();
            error_txt.text = "";

            bool success = await Authenticator.Get().Login(user, pass);

            if (!success)
            {
                error_txt.text = Authenticator.Get().GetError();
                ConnectingPanel.Get().Hide();
                return; //Failed to connect
            }

            string user_id = Authenticator.Get().UserID;
            string username = Authenticator.Get().Username;
            PlayerData.NewOrLoad(user_id, username);

            ClientLobby.Get().SetConnectionExtraData(character_select.GetSelectedValue());
            bool connected = await ClientLobby.Get().Connect();

            ConnectingPanel.Get().Hide();
            if (connected)
            {
                MenuLobby.Get().HideAllPanels();
                LobbyPanel.Get().Show();
            }
        }

        public override void Show(bool instant = false)
        {
            base.Show(instant);
            error_txt.text = "";
        }

        public static LobbyConnectPanel Get()
        {
            return instance;
        }
    }
}