using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace SurvivalEngine
{
    /// <summary>
    /// SettingData is the local save file data for each player, it only saves player preferences and nothing related to the online world.
    /// </summary>

    [System.Serializable]
    public class SettingData
    {
        public string filename;
        public string version;
        public DateTime last_save;

        //-------------------

        public float master_volume = 1f;
        public float music_volume = 1f;
        public float sfx_volume = 1f;

        //-------------------

        private static string loaded_file = "";
        public static SettingData loaded_data = null;

        public const string extension = ".settings";
        public const string last_save_id = "last_save_settings";

        public SettingData(string name)
        {
            filename = name;
            version = Application.version;
            last_save = DateTime.Now;

            master_volume = 1f;
            music_volume = 1f;
            sfx_volume = 1f;
        }

        public void FixData()
        {

        }

        //--- Save / load -----

        public bool IsVersionValid()
        {
            return version == Application.version;
        }

        public void Save()
        {
            Save(loaded_file, this);
        }

        public void Save(string filename)
        {
            Save(filename, this);
        }

        public static void Save(string filename, SettingData data)
        {
            if (!string.IsNullOrEmpty(filename) && data != null)
            {
                data.filename = filename;
                data.last_save = DateTime.Now;
                data.version = Application.version;
                loaded_data = data;
                loaded_file = filename;

                SaveTool.SaveFile<SettingData>(filename + extension, data);
                SetLastSave(filename);
            }
        }

        public static void NewGame()
        {
            NewGame(GetLastSave()); //default name
        }

        //You should reload the scene right after NewGame
        public static SettingData NewGame(string filename)
        {
            loaded_file = filename;
            loaded_data = new SettingData(filename);
            loaded_data.FixData();
            return loaded_data;
        }

        public static SettingData Load(string filename)
        {
            if (loaded_data == null || loaded_file != filename)
            {
                loaded_data = SaveTool.LoadFile<SettingData>(filename + extension);
                if (loaded_data != null)
                {
                    loaded_file = filename;
                    loaded_data.FixData();
                }
            }
            return loaded_data;
        }

        public static SettingData LoadLast()
        {
            return AutoLoad(GetLastSave());
        }

        //Load if found, otherwise new game
        public static SettingData AutoLoad(string filename)
        {
            if (loaded_data == null)
                loaded_data = Load(filename);
            if (loaded_data == null)
                loaded_data = NewGame(filename);
            return loaded_data;
        }

        public static void SetLastSave(string filename)
        {
            if (SaveTool.IsValidFilename(filename))
            {
                PlayerPrefs.SetString(last_save_id, filename);
            }
        }

        public static string GetLastSave()
        {
            string name = PlayerPrefs.GetString(last_save_id, "");
            if (string.IsNullOrEmpty(name))
                name = "player"; //Default name
            return name;
        }

        public static bool HasLastSave()
        {
            return HasSave(GetLastSave());
        }

        public static bool HasSave(string filename)
        {
            return SaveTool.DoesFileExist(filename + extension);
        }

        public static void Unload()
        {
            loaded_data = null;
            loaded_file = "";
        }

        public static void Delete(string filename)
        {
            if (loaded_file == filename)
            {
                loaded_data = new SettingData(filename);
                loaded_data.FixData();
            }

            SaveTool.DeleteFile(filename + extension);
        }

        public static bool IsLoaded()
        {
            return loaded_data != null && !string.IsNullOrEmpty(loaded_file);
        }

        public static SettingData Get()
        {
            return loaded_data;
        }
    }

}