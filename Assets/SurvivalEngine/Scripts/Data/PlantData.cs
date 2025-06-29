﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NetcodePlus;

namespace SurvivalEngine
{
    /// <summary>
    /// Data file for plants
    /// </summary>

    [CreateAssetMenu(fileName = "PlantData", menuName = "SurvivalEngine/PlantData", order = 5)]
    public class PlantData : CraftData
    {
        [Header("--- PlantData ------------------")]

        [Header("Prefab")]
        public GameObject plant_prefab; //Default plant prefab
        public GameObject[] growth_stage_prefabs; //Prefabs at each stages (index 0 is first stage like sprout)

        private static List<PlantData> plant_data = new List<PlantData>();

        public GameObject GetStagePrefab(int stage)
        {
            if (stage >= 0 && stage < growth_stage_prefabs.Length)
                return growth_stage_prefabs[stage];
            return plant_prefab;
        }

        private static bool loaded = false;
        public static new void Load(string folder = "")
        {
            if (!loaded)
            {
                loaded = true;
                plant_data.AddRange(Resources.LoadAll<PlantData>(folder));

                foreach (PlantData data in plant_data)
                {
                    foreach(GameObject obj in data.growth_stage_prefabs)
                        TheNetwork.Get().RegisterPrefab(obj);
                }
            }
        }

        public new static PlantData Get(string construction_id)
        {
            foreach (PlantData item in plant_data)
            {
                if (item.id == construction_id)
                    return item;
            }
            return null;
        }

        public new static List<PlantData> GetAll()
        {
            return plant_data;
        }
    }

}