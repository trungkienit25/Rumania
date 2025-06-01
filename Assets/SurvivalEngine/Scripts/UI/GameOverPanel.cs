using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivalEngine
{
    public class GameOverPanel : UISlotPanel
    {
        private static GameOverPanel _instance;

        protected override void Awake()
        {
            base.Awake();
            _instance = this;
        }

        protected override void Start()
        {
            base.Start();

        }

        protected override void Update()
        {
            base.Update();

        }

        public void OnClickRevive()
        {
            PlayerCharacter character = PlayerCharacter.GetSelf();
            if(character != null)
                character.ReviveAtSpawn();
            Hide();
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

        public static GameOverPanel Get()
        {
            return _instance;
        }
    }

}
