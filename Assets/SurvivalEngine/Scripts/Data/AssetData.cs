using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalEngine
{
    /// <summary>
    /// Generic asset data (only one file)
    /// </summary>

    [CreateAssetMenu(fileName = "AssetData", menuName = "SurvivalEngine/AssetData", order = 0)]
    public class AssetData : ScriptableObject
    {
        [Header("Systems Prefabs")]
        public GameObject ui_canvas;
        public GameObject ui_canvas_mobile;
        public GameObject audio_manager;
        
        [Header("UI")]
        public GameObject action_selector;
        public GameObject action_progress;

        [Header("FX")]
        public GameObject item_take_fx;
        public GameObject item_select_fx;
        public GameObject item_drag_fx;
        public GameObject item_merge_fx;

        [Header("Music")]
        public AudioClip[] music_playlist;

        private static AssetData assets;
        private static bool loaded = false;

        public static void Load(string folder = "")
        {
            if (!loaded)
            {
                loaded = true;
                AssetData[] alldata = Resources.LoadAll<AssetData>(folder);
                if (alldata.Length > 0)
                    assets = alldata[0];
                else
                    Debug.LogError("Make sure to have GameData and AssetData in the Resources Folder");
            }
        }

        public static AssetData Get()
        {
            if (assets == null)
                Load(TheData.Get().load_folder);
            return assets;
        }
    }

}
