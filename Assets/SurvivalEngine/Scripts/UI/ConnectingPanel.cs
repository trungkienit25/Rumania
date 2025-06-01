using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NetcodePlus;

namespace SurvivalEngine
{

    public class ConnectingPanel : UIPanel
    {
        private static ConnectingPanel instance;

        protected override void Awake()
        {
            base.Awake();
            instance = this;
        }

        public void OnClickQuit()
        {
            TheNetwork.Get()?.Disconnect();
            ClientLobby.Get()?.Disconnect();
            Menu.GoToLastMenu();
        }

        public static ConnectingPanel Get()
        {
            return instance;
        }
    }
}