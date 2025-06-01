using NetcodePlus;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivalEngine
{
    public class LobbyPanel : UIPanel
    {
        [Header("Lines Template")]
        public Transform lobby_grid;
        public LobbyLine line_template;
        public UIPanel load_panel;

        [Header("Game Info")]
        public UIPanel info_panel;
        public Text info_title;
        public Text info_scene;
        public Text info_players;
        public Transform players_grid;
        public PlayerLine players_line_template;


        private List<LobbyGame> game_list = new List<LobbyGame>();
        private List<LobbyLine> lobby_lines = new List<LobbyLine>();
        private List<PlayerLine> players_lines = new List<PlayerLine>();

        private LobbyGame selected_room = null;
        private bool joining = false;
        private float timer = 0f;

        private static LobbyPanel instance;

        protected override void Awake()
        {
            base.Awake();
            instance = this;
            line_template.gameObject.SetActive(false);
            players_line_template.gameObject.SetActive(false);
        }

        protected override void Update()
        {
            base.Update();

            timer += Time.deltaTime;

            if (joining && IsVisible())
            {
                if (timer > 5f || Input.GetKeyDown(KeyCode.Escape))
                {
                    joining = false;
                    ConnectingPanel.Get().Hide();
                    ClientLobby.Get().RefreshLobby();
                }
            }
        }

        protected override void Start()
        {
            base.Start();

            ClientLobby client = ClientLobby.Get();
            client.onRefreshList += ReceiveRefreshList;
            client.onRefresh += ReceiveRefresh;
        }

        private void RefreshPanel()
        {
            ClientLobby client = ClientLobby.Get();
            client.RefreshLobby();
            load_panel.Show();
            info_panel.Hide();
            joining = false;
            selected_room = null;
        }

        private void ReceiveRefreshList(LobbyGameList list)
        {
            game_list.Clear();
            game_list.AddRange(list.data);
            load_panel.Hide();

            foreach (LobbyLine line in lobby_lines)
                Destroy(line.gameObject);
            lobby_lines.Clear();

            foreach (LobbyGame game in game_list)
            {
                GameObject obj = Instantiate(line_template.gameObject, lobby_grid);
                obj.SetActive(true);
                LobbyLine line = obj.GetComponent<LobbyLine>();
                line.SetLine(game);
                lobby_lines.Add(line);
            }
        }

        private void RefreshInfoPanel(LobbyGame room)
        {
            if (room == null)
                return;

            foreach (LobbyLine line in lobby_lines)
                line.SetSelected(room == line.GetGame());

            info_title.text = room.title;
            info_scene.text = room.scene;
            info_players.text = room.players.Count + "/" + room.players_max;
            info_panel.Show();

            //Players
            foreach (PlayerLine line in players_lines)
                Destroy(line.gameObject);
            players_lines.Clear();

            foreach (LobbyPlayer player in room.players)
            {
                GameObject obj = Instantiate(players_line_template.gameObject, players_grid);
                obj.SetActive(true);
                PlayerLine line = obj.GetComponent<PlayerLine>();
                line.SetLine(player);
                players_lines.Add(line);
            }
        }

        private void ReceiveRefresh(LobbyGame room)
        {
            if (joining && room.HasPlayer(ClientLobby.Get().UserID))
            {
                Hide();
                joining = false;
                ConnectingPanel.Get().Hide();
                LobbyRoomPanel.Get().ShowGame(room);
            }
        }

        public void OnClickCreate()
        {
            LobbyCreatePanel.Get().Show();
            Hide();
        }

        public void OnClickRefresh()
        {
            RefreshPanel();
        }

        public void OnClickLine(LobbyLine line)
        {
            selected_room = line.GetGame();
            RefreshInfoPanel(selected_room);
        }

        public void OnClickJoin()
        {
            if (selected_room != null)
            {
                joining = true;
                timer = 0f;
                ConnectingPanel.Get().Show();
                ClientLobby.Get().JoinGame(selected_room.game_id);
            }
        }

        public void WaitForCreate()
        {
            Show();
            ConnectingPanel.Get().Show();
            joining = true;
            timer = 0f;
        }

        public void OnClickBack()
        {
            LobbyConnectPanel.Get().Show();
            Hide();
        }

        public override void Show(bool instant = false)
        {
            base.Show(instant);
            RefreshPanel();
        }

        public static LobbyPanel Get()
        {
            return instance;
        }
    }
}