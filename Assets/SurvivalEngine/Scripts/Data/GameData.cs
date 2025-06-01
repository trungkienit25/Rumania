using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalEngine
{
    /// <summary>
    /// Generic game data (only one file)
    /// </summary>

    [CreateAssetMenu(fileName = "GameData", menuName = "SurvivalEngine/GameData", order = 0)]
    public class GameData : ScriptableObject
    {
        [Header("Game")]
        public float game_time_mult = 24f; //A value of 1 means time follows real life time. Value of 24 means 1 hour of real time will be one day in game
        public float start_day_time = 6f; //Time at start of day
        public float end_day_time = 20f; //Time at end of day

        [Header("Day/Night")]
        public float day_light_dir_intensity = 1f; //Directional light at day
        public float day_light_ambient_intensity = 1f;  //Ambient light at day
        public float night_light_dir_intensity = 0.2f; //Directional light at night
        public float night_light_ambient_intensity = 0.5f; //Ambient light at night
        public float light_update_speed = 0.2f;
        public bool rotate_shadows = true; //Will rotate shadows during the day as if sun is rotating

        [Header("Save")]
        public GameSaveType save_type = GameSaveType.OneSave; //Is the save file split or all in one, if split each player send their own save data

        [Header("Player Names")]
        public string[] random_names;

        private static GameData data;
        private static bool loaded = false;

        public string GetRandomName()
        {
            if (random_names != null && random_names.Length > 0)
                return random_names[Random.Range(0, random_names.Length)];
            return "Player";
        }

        public static void Load(string folder = "")
        {
            if (!loaded)
            {
                loaded = true;
                GameData[] alldata = Resources.LoadAll<GameData>(folder);
                if (alldata.Length > 0)
                    data = alldata[0];
                else
                    Debug.LogError("Make sure to have GameData and AssetData in the Resources Folder");
            }
        }

        public static GameData Get()
        {
            if (data == null)
                Load(TheData.Get().load_folder);
            return data;
        }
    }

    public enum GameSaveType
    {
        OneSave = 0,        //The entire data is in 1 file and is managed by the host or server
        SplitSave = 10,     //Data files are split between world and players, each player send their own, and host send the world
    }
}
