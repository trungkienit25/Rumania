using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalEngine
{

    public class DisconnectPanel : UIPanel
    {
        private static DisconnectPanel instance;

        protected override void Awake()
        {
            base.Awake();
            instance = this;
        }

        public void OnClickQuit()
        {
            StartCoroutine(QuitRoutine());
        }

        private IEnumerator QuitRoutine()
        {
            BlackPanel.Get().Show();

            yield return new WaitForSeconds(1f);

            TheGame.Get().QuitToMenu();
        }

        public static DisconnectPanel Get()
        {
            return instance;
        }
    }
}