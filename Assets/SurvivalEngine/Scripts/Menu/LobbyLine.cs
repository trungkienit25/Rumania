using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NetcodePlus;

namespace SurvivalEngine
{
    public class LobbyLine : MonoBehaviour
    {
        public Text title;
        public Text scene;
        public Text players;
        public Image highlight;

        private LobbyGame game;

        void Awake()
        {

        }

        public void SetLine(LobbyGame room)
        {
            game = room;
            title.text = game.title;
            scene.text = game.scene;
            players.text = game.players.Count + "/" + game.players_max;
            highlight.enabled = false;
        }

        public void SetSelected(bool selected)
        {
            highlight.enabled = selected;
        }

        public void OnClick()
        {
            LobbyPanel.Get().OnClickLine(this);
        }

        public LobbyGame GetGame()
        {
            return game;
        }
    }
}