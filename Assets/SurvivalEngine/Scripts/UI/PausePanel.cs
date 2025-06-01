using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivalEngine
{
    public class PausePanel : UISlotPanel
    {
        [Header("Pause Panel")]
        public Image speaker_btn;
        public Sprite speaker_on;
        public Sprite speaker_off;

        private static PausePanel _instance;

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

            if(speaker_btn != null)
                speaker_btn.sprite = SettingData.Get().master_volume > 0.1f ? speaker_on : speaker_off;

        }

        public override void Hide(bool instant = false)
        {
            base.Hide(instant);

        }

        public void OnClickSave()
        {
            TheGame.Get().Save();
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

        public void OnClickMusicToggle()
        {
            SettingData.Get().master_volume = SettingData.Get().master_volume > 0.1f ? 0f : 1f;
            TheAudio.Get().RefreshVolume();
        }

        public static PausePanel Get()
        {
            return _instance;
        }
    }

}
