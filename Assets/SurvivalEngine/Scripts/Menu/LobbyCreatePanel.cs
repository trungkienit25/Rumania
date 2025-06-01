using NetcodePlus;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivalEngine
{
    public class LobbyCreatePanel : UIPanel
    {
        public InputField title_field;
        public InputField save_field;
        public OptionSelector scene_field;
        public GameObject scene_group;
        public Toggle toggle_new;
        public Toggle toggle_load;

        private bool creating = false;
        private float timer = 0f;

        private static LobbyCreatePanel instance;

        protected override void Awake()
        {
            base.Awake();
            instance = this;
        }

        protected override void Update()
        {
            base.Update();

            if (creating && IsVisible())
            {
                if (timer > 5f || Input.GetKeyDown(KeyCode.Escape))
                {
                    creating = false;
                    ConnectingPanel.Get().Hide();
                }
            }

            RefreshScene();
        }

        protected override void Start()
        {
            base.Start();

            ClientLobby client = ClientLobby.Get();
            client.onConnect += OnConnect;
            client.onRefresh += ReceiveRefresh;

            RefreshScene();
        }

        private void OnConnect(bool success)
        {
            title_field.text = ClientLobby.Get().Username + "'s World";
        }

        public void OnClickCreate()
        {
            if (!SceneNav.DoSceneExist(scene_field.GetSelectedValue()))
                return; //Scene invalid
            if (title_field.text.Length == 0 || save_field.text.Length == 0)
                return; //Title/save invaild
            if (toggle_load.isOn && !WorldData.HasSave(save_field.text))
                return; //Not such save file

            creating = true;
            timer = 0f;
            LobbyPanel.Get().WaitForCreate();
            Hide();

            WorldData.Unload();

            //Create game
            WorldData save;
            if (toggle_load.isOn)
                save = WorldData.Load(save_field.text);
            else
                save = WorldData.NewGame(save_field.text, scene_field.GetSelectedValue());

            MenuLobby.Get().CreateGame(title_field.text, save.filename, save.scene);
        }

        private void ReceiveRefresh(LobbyGame room)
        {
            if (creating && room.HasPlayer(ClientLobby.Get().UserID))
            {
                Hide();
                creating = false;
                ConnectingPanel.Get().Hide();
                LobbyRoomPanel.Get().ShowGame(room);
            }
        }

        private void RefreshScene()
        {
            scene_group?.SetActive(toggle_new.isOn);
            scene_field.gameObject.SetActive(toggle_new.isOn);
        }

        public void OnClickBack()
        {
            LobbyPanel.Get().Show();
            Hide();
        }


        public static LobbyCreatePanel Get()
        {
            return instance;
        }
    }
}