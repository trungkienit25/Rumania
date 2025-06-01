using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SurvivalEngine
{

    [System.Serializable]
    public class OptionString
    {
        public string value;
        public string title;
    }

    public class OptionSelector : MonoBehaviour
    {
        [Header("Options")]
        public OptionString[] options;

        [Header("Display")]
        public Text select_text;

        public UnityAction onChange;

        private int position = 0;

        private List<OptionString> options_list = new List<OptionString>();

        void Awake()
        {
            options_list.AddRange(options);
            SetIndex(0);
        }

        void Update()
        {

        }

        public void AddOption(string id, string title)
        {
            OptionString opt = new OptionString();
            opt.value = id;
            opt.title = title;
            AddOption(opt);
        }

        public void AddOption(OptionString option)
        {
            options_list.Add(option);
            SetIndex(0);
        }

        private void AfterChangeOption()
        {
            if (select_text != null)
                select_text.text = GetSelectedTitle();
            onChange?.Invoke();
        }

        public void OnClickLeft()
        {
            if (options_list.Count == 0)
                return;

            position = (position + options_list.Count - 1) % options_list.Count;
            AfterChangeOption();
        }

        public void OnClickRight()
        {
            if (options_list.Count == 0)
                return;

            position = (position + options_list.Count + 1) % options_list.Count;
            AfterChangeOption();
        }

        public void SetIndex(int index)
        {
            position = index;
            AfterChangeOption();
        }

        public void SetValue(string value)
        {
            for (int i = 0; i < options_list.Count; i++)
            {
                if (options_list[i].value == value)
                    position = i;
            }

            AfterChangeOption();
        }

        public void SetRandomValue()
        {
            position = Random.Range(0, options_list.Count);
            AfterChangeOption();
        }

        public OptionString GetSelected()
        {
            if (position < options_list.Count)
                return options_list[position];
            return null;
        }

        public string GetSelectedValue()
        {
            if (position < options_list.Count)
                return options_list[position].value;
            return "";
        }

        public string GetSelectedTitle()
        {
            if (position < options_list.Count)
                return options_list[position].title;
            return "";
        }
    }
}