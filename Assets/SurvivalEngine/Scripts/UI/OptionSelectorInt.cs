using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SurvivalEngine
{
    [System.Serializable]
    public class OptionInt
    {
        public int value;
        public string title;
        public string subtitle;
    }

    public class OptionSelectorInt : MonoBehaviour
    {
        [Header("Options")]
        public OptionInt[] options;

        [Header("Display")]
        public Text select_text;
        public Text select_subtext;

        public UnityAction onChange;

        private int position = 0;
        private bool is_locked = false;

        private List<OptionInt> options_list = new List<OptionInt>();

        void Awake()
        {
            options_list.AddRange(options);
            SetIndex(0);
        }

        public void AddOption(int value, string title)
        {
            OptionInt opt = new OptionInt();
            opt.value = value;
            opt.title = title;
        }

        public void AddOption(OptionInt option)
        {
            options_list.Add(option);
            SetIndex(0);
        }

        private void AfterChangeOption()
        {
            if (select_text != null)
                select_text.text = GetSelectedTitle();
            if (select_subtext != null)
                select_subtext.text = GetSelectedSubTitle();
            onChange?.Invoke();
        }

        public void OnClickLeft()
        {
            if (is_locked)
                return;

            position = (position + options_list.Count - 1) % options_list.Count;
            AfterChangeOption();
        }

        public void OnClickRight()
        {
            if (is_locked)
                return;

            position = (position + options_list.Count + 1) % options_list.Count;
            AfterChangeOption();
        }

        public void SetIndex(int index)
        {
            position = index;
            if (select_text != null)
                select_text.text = GetSelectedTitle();
            if (select_subtext != null)
                select_subtext.text = GetSelectedSubTitle();
        }

        public void SetValue(int value)
        {
            for (int i = 0; i < options_list.Count; i++)
            {
                if (options_list[i].value == value)
                    position = i;
            }

            if (select_text != null)
                select_text.text = GetSelectedTitle();
            if (select_subtext != null)
                select_subtext.text = GetSelectedSubTitle();
        }

        public void SetLocked(bool locked)
        {
            is_locked = locked;
        }

        public OptionInt GetSelected()
        {
            return options_list[position];
        }

        public int GetSelectedValue()
        {
            return options_list[position].value;
        }

        public string GetSelectedTitle()
        {
            if (!string.IsNullOrWhiteSpace(options_list[position].title))
                return options_list[position].title;
            return options_list[position].value.ToString();
        }

        public string GetSelectedSubTitle()
        {
            return options_list[position].subtitle;
        }
    }
}