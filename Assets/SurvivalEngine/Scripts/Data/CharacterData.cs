using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NetcodePlus;

namespace SurvivalEngine
{
    /// <summary>
    /// Data file for Characters
    /// </summary>

    [CreateAssetMenu(fileName = "CharacterData", menuName = "SurvivalEngine/CharacterData", order = 5)]
    public class CharacterData : CraftData
    {
        [Header("--- CharacterData ------------------")]

        public GameObject character_prefab; //Prefab spawned when the character is built

        [Header("Ref Data")]
        public ItemData take_item_data;

        private static List<CharacterData> character_data = new List<CharacterData>();
        private static bool loaded = false;

        public static new void Load(string folder = "")
        {
            if (!loaded)
            {
                loaded = true;
                character_data.AddRange(Resources.LoadAll<CharacterData>(folder));

                foreach (CharacterData data in character_data)
                {
                    TheNetwork.Get().RegisterPrefab(data.character_prefab);
                }
            }
        }

        public new static CharacterData Get(string character_id)
        {
            foreach (CharacterData item in character_data)
            {
                if (item.id == character_id)
                    return item;
            }
            return null;
        }

        public new static List<CharacterData> GetAll()
        {
            return character_data;
        }
    }

}
