using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NetcodePlus;

namespace SurvivalEngine
{
    public class LobbyRoomPanel : UIPanel
    {
        [Header("Game Info")]
        public Text info_title;
        public Text info_scene;
        public Text info_status;
        public Text info_players;
        public Transform players_grid;
        public PlayerLine players_line_template;
        public Button start_btn;

        [Header("Chat")]
        public Transform chat_group;
        public GameObject chat_line_template;
        public InputField chat_field;
        public int max_chat_lines = 12;

        private LobbyGame game = null;
        private float removed_timer = 0f;

        private List<PlayerLine> players_lines = new List<PlayerLine>();
        private List<Text> chat_lines = new List<Text>();


        private static LobbyRoomPanel instance;

        protected override void Awake()
        {
            base.Awake();
            instance = this;
            chat_line_template.SetActive(false);
            players_line_template.gameObject.SetActive(false);
        }

        protected override void Update()
        {
            base.Update();

            if (Input.GetKeyDown(KeyCode.Return))
                OnPressChatEnter();

            if (removed_timer > 15f)
                OnClickBack(); //Didnt received msg from server
        }

        protected override void Start()
        {
            base.Start();

            ClientLobby client = ClientLobby.Get();
            client.onRefresh += ReceiveRefresh;
            client.RefreshGame();
        }

        private void ReceiveRefresh(LobbyGame room)
        {
            if (!IsVisible())
                return;

            if (room == null)
            {
                OnClickBack(); //Room dont exists anymore, quit
                return;
            }

            if (room.HasPlayer(ClientLobby.Get().UserID))
            {
                game = room;
                RefreshGameRoom();

                if (game.state == RoomState.Playing)
                    StartGame();
            }
            else
            {
                OnClickBack();
            }
        }

        private void ClearGameRoom()
        {
            foreach (PlayerLine line in players_lines)
                Destroy(line.gameObject);
            foreach (Text line in chat_lines)
                Destroy(line.gameObject);
            players_lines.Clear();
            chat_lines.Clear();
            chat_field.text = "";
        }

        private void RefreshGameRoom()
        {
            if (this == null || game == null || !IsVisible())
                return;

            removed_timer = 0f;
            start_btn.interactable = CanStartGame();
            info_title.text = game.title;
            info_scene.text = game.scene;
            info_players.text = game.players.Count + "/" + game.players_max;
            info_status.text = game.state.ToString();

            foreach (PlayerLine line in players_lines)
                line.Hide();

            //Players
            for (int i = 0; i < game.players.Count; i++)
            {
                PlayerLine line = GetPlayerLine(i);
                line.SetLine(game.players[i]);
            }

            //Chat
            int start = Mathf.Max(game.chats.Count - max_chat_lines, 0);
            for (int i= 0; i< game.chats.Count - start; i++)
            {
                ChatMsg chat = game.chats[start + i];
                Text line = GetChatLine(i);
                line.text = "<b>" + chat.username + "</b>: " + chat.text;
            }
        }

        private PlayerLine GetPlayerLine(int index)
        {
            if (index < players_lines.Count)
                return players_lines[index];
            GameObject obj = Instantiate(players_line_template.gameObject, players_grid);
            obj.SetActive(true);
            PlayerLine line = obj.GetComponent<PlayerLine>();
            players_lines.Add(line);
            return line;
        }

        private Text GetChatLine(int index)
        {
            if (index < chat_lines.Count)
                return chat_lines[index];
            GameObject obj = Instantiate(chat_line_template.gameObject, chat_group);
            obj.SetActive(true);
            Text line = obj.GetComponent<Text>();
            chat_lines.Add(line);
            return line;
        }

        public void ShowGame(LobbyGame room)
        {
            game = room;
            ClearGameRoom();
            Show();
            RefreshGameRoom();
        }

        public void StartGame()
        {
            if (game == null)
                return;

            Hide();
            MenuLobby.Get().ConnectToGame(game);
        }

        public void OnClickStart()
        {
            if (CanStartGame())
            {
                ClientLobby.Get().StartGame();
            }
        }

        public void OnPressChatEnter()
        {
            if (!chat_field.isFocused)
            {
                chat_field.Select();
                chat_field.ActivateInputField();
            }

            OnClickSendChat();
        }

        public void OnClickSendChat()
        {
            if (game == null || chat_field.text.Length == 0)
                return;

            ClientLobby.Get().SendChat(chat_field.text);
            chat_field.text = "";
        }

        public void OnClickBack()
        {
            ClientLobby.Get().QuitGame();
            LobbyPanel.Get().Show();
            Hide();
        }

        private bool CanStartGame()
        {
            if (game == null) return false;
            bool host = game.state == RoomState.Waiting && game.IsHost(ClientLobby.Get().UserID);
            return game.state == RoomState.Playing || host;
        }

        public static LobbyRoomPanel Get()
        {
            return instance;
        }
    }
}