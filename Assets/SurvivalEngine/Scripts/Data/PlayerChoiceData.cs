using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NetcodePlus;

namespace SurvivalEngine
{
    /// <summary>
    /// Data file for Characters
    /// </summary>

    [CreateAssetMenu(fileName = "PlayerChoiceData", menuName = "SurvivalEngine/PlayerChoiceData", order = 5)]
    public class PlayerChoiceData : ScriptableObject
    {
        public string id;
        public string title;
        public Sprite portrait;
        public GameObject prefab; //Prefab spawned when the character is built
        public int sort_order;

        private static List<PlayerChoiceData> player_data = new List<PlayerChoiceData>();
        private static bool loaded = false;

        public static void Load(string folder = "")
        {
            if (!loaded)
            {
                loaded = true;
                player_data.AddRange(Resources.LoadAll<PlayerChoiceData>(folder));
                player_data.Sort((PlayerChoiceData p1, PlayerChoiceData p2) => { return p1.sort_order.CompareTo(p2.sort_order); });

                foreach (PlayerChoiceData data in player_data)
                {
                    TheNetwork.Get().RegisterPrefab(data.prefab);
                }
            }
        }

        public static PlayerChoiceData Get(string character_id)
        {
            foreach (PlayerChoiceData item in player_data)
            {
                if (item.id == character_id)
                    return item;
            }
            return null;
        }

        public static List<PlayerChoiceData> GetAll()
        {
            return player_data;
        }
    }

}
