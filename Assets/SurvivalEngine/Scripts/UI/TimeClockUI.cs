using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivalEngine
{

    /// <summary>
    /// Clock showing days and time
    /// </summary>

    public class TimeClockUI : MonoBehaviour
    {
        public Text day_txt;
        public Text time_txt;
        public Image clock_fill;

        private float update_timer = 0f;

        void Start()
        {

        }

        void Update()
        {
            update_timer += Time.deltaTime;
            if (update_timer > 0.2f)
            {
                update_timer = 0f;
                SlowUpdate();
            }
        }

        private void SlowUpdate()
        {
            WorldData pdata = WorldData.Get();
            int time_hours = Mathf.FloorToInt(pdata.day_time);
            int time_secs = Mathf.FloorToInt((pdata.day_time * 60f) % 60f);

            day_txt.text = "JOUR " + pdata.day;
            time_txt.text = time_hours + ":" + time_secs.ToString("00");

            bool clockwise = pdata.day_time <= 12f;
            clock_fill.fillClockwise = clockwise;
            if (clockwise)
            {
                float value = pdata.day_time / 12f; //0f to 1f
                clock_fill.fillAmount = value;
            }
            else
            {
                float value = (pdata.day_time - 12f) / 12f; //0f to 1f
                clock_fill.fillAmount = 1f - value;
            }
        }
    }

}