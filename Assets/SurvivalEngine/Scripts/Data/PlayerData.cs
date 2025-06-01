using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using NetcodePlus;

namespace SurvivalEngine
{
    //Save file portion linked to a player (and sent by that player instead of the host)

    [System.Serializable]
    public class PlayerData
    {
        public string filename;     //File name, usually same as username

        public int player_id;       //Player ID assigned to player by NetcodePlus (usually 0 is the host)
        public string user_id = ""; //Authentication ID of the player
        public string username = ""; //Username of the player
        public string character = ""; //Character prefab selected by player

        public Vector3Data position;   //Saved position within scene, if PlayerData scene/world dont match WorldData scene/world, this will be ignored
        public string scene;           //Save last visited scene, if it doesn't match the current scene, will go to default spawn location instead of position
        public ulong world;            //World id also need to match this to spawn at position (otherwise go to default spawn pos)
        public int gold = 0;

        public Dictionary<string, InventoryData> inventories = new Dictionary<string, InventoryData>();
        public Dictionary<string, PlayerLevelData> levels = new Dictionary<string, PlayerLevelData>();
        public Dictionary<AttributeType, float> attributes = new Dictionary<AttributeType, float>();
        public Dictionary<BonusType, TimedBonusData> timed_bonus_effects = new Dictionary<BonusType, TimedBonusData>();

        public Dictionary<string, int> crafted_count = new Dictionary<string, int>();
        public Dictionary<string, int> kill_count = new Dictionary<string, int>();
        public Dictionary<string, int> unlocked_ids = new Dictionary<string, int>();
        public Dictionary<string, PlayerPetData> pets = new Dictionary<string, PlayerPetData>();

#if MAP_MINIMAP
        public MapMinimap.MapData map_data = null;
#endif

#if DIALOGUE_QUESTS
        public DialogueQuests.NarrativeData narrative_data = null;
#endif

        private bool need_full_refresh = false;
        private bool need_level_refresh = false;

        private static string loaded_file = "";
        private static PlayerData loaded_data = null;

        public const string extension = ".player";
        public const string last_save_id = "last_save_player";

        public PlayerData(int id) { filename = user_id = username = ""; player_id = id; FixData(); }
        public PlayerData(string f) { filename = f.ToLower(); user_id = f; username = f; FixData(); }

        public void FixData()
        {
            //Fix data to make sure old save files compatible with new game version
            if (inventories == null)
                inventories = new Dictionary<string, InventoryData>();
            if (levels == null)
                levels = new Dictionary<string, PlayerLevelData>();
            if (attributes == null)
                attributes = new Dictionary<AttributeType, float>();
            if (unlocked_ids == null)
                unlocked_ids = new Dictionary<string, int>();
            if (timed_bonus_effects == null)
                timed_bonus_effects = new Dictionary<BonusType, TimedBonusData>();

            if (crafted_count == null)
                crafted_count = new Dictionary<string, int>();
            if (kill_count == null)
                kill_count = new Dictionary<string, int>();

            if (pets == null)
                pets = new Dictionary<string, PlayerPetData>();

#if MAP_MINIMAP
            if(map_data == null)
                map_data = new MapMinimap.MapData(filename);
            map_data.FixData();
#endif

#if DIALOGUE_QUESTS
            if (narrative_data == null)
                narrative_data = new DialogueQuests.NarrativeData(filename, player_id);
            narrative_data.FixData();
#endif
        }

        // -- Inventory -----

        public InventoryData GetInventory(InventoryType type, string inventory_uid)
        {
            InventoryData sdata = null;
            if (!string.IsNullOrEmpty(inventory_uid))
            {
                if (inventories.ContainsKey(inventory_uid))
                {
                    sdata = inventories[inventory_uid];
                }
                else
                {
                    //Create new if dont exist
                    sdata = new InventoryData(type, inventory_uid);
                    sdata.owner = player_id;
                    inventories[inventory_uid] = sdata;
                }
            }
            return sdata;
        }

        public InventoryData GetInventory(string inventory_uid)
        {
            InventoryData sdata = null;
            if (!string.IsNullOrEmpty(inventory_uid))
            {
                if (inventories.ContainsKey(inventory_uid))
                {
                    sdata = inventories[inventory_uid];
                }
            }
            return sdata;
        }

        public InventoryData GetInventory(InventoryType type)
        {
            string uid = (type == InventoryType.Equipment) ? GetPlayerEquipUID() : GetPlayerUID();
            return GetInventory(type, uid);
        }

        public void RemoveInventory(string uid)
        {
            if (inventories.ContainsKey(uid))
                inventories.Remove(uid);
        }

        public bool HasInventory()
        {
            return HasInventory(GetPlayerUID());
        }

        public bool HasEquipInventory()
        {
            return HasInventory(GetPlayerEquipUID());
        }

        public bool HasInventory(string inventory_uid)
        {
            if (!string.IsNullOrEmpty(inventory_uid))
            {
                if (inventories.ContainsKey(inventory_uid))
                    return true;
            }
            return false;
        }

        public void OverrideInventory(string inventory_uid, InventoryData data)
        {
            inventories[inventory_uid] = data;
        }

        //--- Attributes ----

        public bool HasAttribute(AttributeType type)
        {
            return attributes.ContainsKey(type);
        }

        public float GetAttributeValue(AttributeType type)
        {
            if (attributes.ContainsKey(type))
                return attributes[type];
            return 0f;
        }

        public void SetAttributeValue(AttributeType type, float value, float max)
        {
            attributes[type] = Mathf.Clamp(value, 0f, max);
        }

        public void AddAttributeValue(AttributeType type, float value, float max)
        {
            if (!attributes.ContainsKey(type))
                attributes[type] = value;
            else
                attributes[type] += value;

            attributes[type] = Mathf.Clamp(attributes[type], 0f, max);
        }

        public void AddTimedBonus(BonusType type, float value, float duration)
        {
            TimedBonusData new_bonus = new TimedBonusData();
            new_bonus.bonus = type;
            new_bonus.value = value;
            new_bonus.time = duration;

            if (!timed_bonus_effects.ContainsKey(type) || timed_bonus_effects[type].time < duration)
                timed_bonus_effects[type] = new_bonus;
        }

        public void RemoveTimedBonus(BonusType type)
        {
            if (timed_bonus_effects.ContainsKey(type))
                timed_bonus_effects.Remove(type);
        }

        public float GetTotalTimedBonus(BonusType type)
        {
            if (timed_bonus_effects.ContainsKey(type) && timed_bonus_effects[type].time > 0f)
                return timed_bonus_effects[type].value;
            return 0f;
        }

        // ---- Levels ------

        public void GainXP(string id, int xp)
        {
            PlayerLevelData ldata = GetLevelData(id);
            ldata.xp += xp;
            need_level_refresh = true;
        }

        public void SetXP(string id, int xp)
        {
            PlayerLevelData ldata = GetLevelData(id);
            ldata.xp = xp;
            need_level_refresh = true;
        }

        public void GainLevel(string id)
        {
            PlayerLevelData ldata = GetLevelData(id);
            LevelData current = LevelData.GetLevel(id, ldata.level);
            LevelData next = LevelData.GetLevel(id, ldata.level + 1);
            if (next != null && current != next)
            {
                ldata.level = next.level;
                ldata.xp = Mathf.Max(ldata.xp, next.xp_required);
                need_level_refresh = true;
            }
        }

        public void SetLevel(string id, int level)
        {
            PlayerLevelData ldata = GetLevelData(id);
            LevelData current = LevelData.GetLevel(id, ldata.level);
            LevelData next = LevelData.GetLevel(id, level);
            if (next != null && current != next)
            {
                ldata.level = next.level;
                ldata.xp = Mathf.Max(ldata.xp, next.xp_required);
                need_level_refresh = true;
            }
        }

        public int GetLevel(string id)
        {
            PlayerLevelData ldata = GetLevelData(id);
            return ldata.level;
        }

        public int GetXP(string id)
        {
            PlayerLevelData ldata = GetLevelData(id);
            return ldata.xp;
        }

        public PlayerLevelData GetLevelData(string id)
        {
            if (levels.ContainsKey(id))
                return levels[id];
            PlayerLevelData data = new PlayerLevelData();
            data.id = id;
            data.level = 1;
            levels[id] = data;
            return data;
        }

        public float GetLevelBonusValue(BonusType type, GroupData target = null)
        {
            float val = 0f;
            foreach (KeyValuePair<string, PlayerLevelData> pair in levels)
            {
                PlayerLevelData ldata = pair.Value;
                LevelData level = LevelData.GetLevel(ldata.id, ldata.level);
                if (level != null)
                {
                    foreach (LevelUnlockBonus bonus in level.unlock_bonuses)
                    {
                        if (bonus.bonus == type && target == bonus.target_group)
                            val += bonus.bonus_value;
                    }
                }
            }
            return val;
        }

        // ---- Unlock groups -----

        public void UnlockID(string id)
        {
            if (!string.IsNullOrEmpty(id))
                unlocked_ids[id] = 1;
            need_full_refresh = true;
        }

        public void RemoveUnlockedID(string id)
        {
            if (unlocked_ids.ContainsKey(id))
                unlocked_ids.Remove(id);
            need_full_refresh = true;
        }

        public bool IsIDUnlocked(string id)
        {
            if (unlocked_ids.ContainsKey(id))
                return unlocked_ids[id] > 0;
            return false;
        }

        // --- Craftable crafted
        public void AddCraftCount(string craft_id, int value = 1)
        {
            if (!string.IsNullOrEmpty(craft_id))
            {
                if (crafted_count.ContainsKey(craft_id))
                    crafted_count[craft_id] += value;
                else
                    crafted_count[craft_id] = value;

                if (crafted_count[craft_id] <= 0)
                    crafted_count.Remove(craft_id);
                need_full_refresh = true;
            }
        }

        public int GetCraftCount(string craft_id)
        {
            if (crafted_count.ContainsKey(craft_id))
                return crafted_count[craft_id];
            return 0;
        }

        public void ResetCraftCount(string craft_id)
        {
            if (crafted_count.ContainsKey(craft_id))
            {
                crafted_count.Remove(craft_id);
                need_full_refresh = true;
            }
        }

        public void ResetCraftCount()
        {
            crafted_count.Clear();
            need_full_refresh = true;
        }

        // --- Killed things
        public void AddKillCount(string craft_id, int value = 1)
        {
            if (!string.IsNullOrEmpty(craft_id))
            {
                if (kill_count.ContainsKey(craft_id))
                    kill_count[craft_id] += value;
                else
                    kill_count[craft_id] = value;

                if (kill_count[craft_id] <= 0)
                    kill_count.Remove(craft_id);
                need_full_refresh = true;
            }
        }

        public int GetKillCount(string craft_id)
        {
            if (kill_count.ContainsKey(craft_id))
                return kill_count[craft_id];
            return 0;
        }

        public void ResetKillCount(string craft_id)
        {
            if (kill_count.ContainsKey(craft_id))
            {
                kill_count.Remove(craft_id);
                need_full_refresh = true;
            }
        }

        public void ResetKillCount()
        {
            kill_count.Clear();
            need_full_refresh = true;
        }

        public void AddPet(string uid, string pet_id)
        {
            PlayerPetData pet = new PlayerPetData();
            pet.pet_id = pet_id;
            pet.uid = uid;
            pets[uid] = pet;
            need_full_refresh = true;
        }

        public void RemovePet(string uid)
        {
            if (pets.ContainsKey(uid))
            {
                pets.Remove(uid);
                need_full_refresh = true;
            }
        }

        public void OverrideAttributes(RefreshPlayerAttributes attr)
        {
            gold = attr.gold;
            attributes = attr.attributes;
            timed_bonus_effects = attr.timed_bonus_effects;
        }

        public void OverrideLevels(RefreshPlayerLevels lvls)
        {
            levels = lvls.levels;
        }

#if DIALOGUE_QUESTS
        public void OverrideNarrativeData(DialogueQuests.NarrativeData ndata)
        {
            narrative_data = ndata;
        }
#endif

        public string GetPlayerUID()
        {
            return "inventory";
        }

        public string GetPlayerEquipUID()
        {
            return "equip";
        }

        public bool IsFullRefresh()
        {
            return need_full_refresh;
        }

        public bool IsLevelRefresh()
        {
            return need_level_refresh;
        }

        public void ClearRefresh()
        {
            need_full_refresh = false;
            need_level_refresh = false;
        }

        public bool IsValidScene()
        {
            WorldData wdata = WorldData.Get();
            return wdata != null && wdata.scene == scene && wdata.world_id == world;
        }

        //--- Save / load -----

        public void Save()
        {
            Save(filename, this);
        }

        public void Save(string filename)
        {
            Save(filename, this);
        }

        public static void Save(string filename, PlayerData data)
        {
            if (!string.IsNullOrEmpty(filename) && data != null)
            {
                filename = filename.ToLower();
                data.filename = filename;
                loaded_data = data;
                loaded_file = filename;

                SaveTool.SaveFile<PlayerData>(filename + extension, data);
                SetLastSave(filename);
            }
        }

        public static void NewGame()
        {
            NewGame(GetLastSave()); //default name
        }

        //You should reload the scene right after NewGame
        public static PlayerData NewGame(string filename)
        {
            filename = filename.ToLower();
            loaded_file = filename;
            loaded_data = new PlayerData(filename);
            loaded_data.FixData();
            return loaded_data;
        }

        public static PlayerData Load(string filename)
        {
            filename = filename.ToLower();
            if (loaded_data == null || loaded_file != filename)
            {
                loaded_file = "";
                loaded_data = SaveTool.LoadFile<PlayerData>(filename + extension);
                if (loaded_data != null)
                {
                    loaded_file = filename;
                    loaded_data.filename = filename;
                    loaded_data.FixData();
                }
            }
            return loaded_data;
        }

        public static PlayerData LoadLast()
        {
            return AutoLoad(GetLastSave());
        }

        //Use when loading a new scene, it will keep the current save file, unless there is none, then it will load
        public static PlayerData AutoLoad(string filename)
        {
            if (!SaveTool.IsValidFilename(filename))
                return null;
            if (loaded_data == null)
                loaded_data = Load(filename);
            if (loaded_data == null)
                loaded_data = NewGame(filename);
            return loaded_data;
        }

        //Clear current load, and then try to either load or create a new game
        public static PlayerData NewOrLoad(string filename)
        {
            if (!SaveTool.IsValidFilename(filename))
                return null;
            Unload();
            loaded_data = Load(filename);
            if (loaded_data == null)
                loaded_data = NewGame(filename);
            return loaded_data;
        }

        public static PlayerData NewOrLoad(string user_id, string username)
        {
            PlayerData pdata = NewOrLoad(username);
            if (pdata != null)
                pdata.user_id = user_id;
            return pdata;
        }

        public static void Override(PlayerData data)
        {
            loaded_data = data;
            loaded_file = data.filename;
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
            return SaveTool.DoesFileExist(filename.ToLower() + extension);
        }

        public static void Unload()
        {
            loaded_data = null;
            loaded_file = "";
        }

        public static void Delete(string filename)
        {
            SaveTool.DeleteFile(filename + extension);
        }

        public static bool IsLoaded()
        {
            return loaded_data != null && !string.IsNullOrEmpty(loaded_file);
        }

        //-------

        //Copy the loaded Player save into the World save
        public static void CopyToWorld(int player_id)
        {
            if(IsLoaded() && player_id >= 0)
                WorldData.Get().OverridePlayer(player_id, GetLoaded());
        }

        //Copy World save's player into your local player save
        public static void CopyFromWorld(int player_id)
        {
            if(WorldData.Get().HasPlayer(player_id))
                Override(WorldData.Get().GetPlayer(player_id));
        }

        //-------

        public static PlayerData Get(int player_id)
        {
            WorldData wdata = WorldData.Get();
            if (wdata != null && player_id >= 0)
                return wdata.GetPlayer(player_id);
            return null;
        }

        public static PlayerData Get(string user_id)
        {
            WorldData wdata = WorldData.Get();
            if (wdata != null && !string.IsNullOrEmpty(user_id))
                return wdata.GetPlayer(user_id);
            return null;
        }

        public static PlayerData GetSelf()
        {
            return Get(TheNetwork.Get().PlayerID);
        }

        public static PlayerData GetLoaded()
        {
            return loaded_data;
        }

        public static List<PlayerData> GetAll()
        {
            List<PlayerData> list = new List<PlayerData>() ;
            list.AddRange(WorldData.Get().players.Values);
            return list;
        }
    }

    [System.Serializable]
    public class TimedBonusData
    {
        public BonusType bonus;
        public float time;
        public float value;
    }

    [System.Serializable]
    public class PlayerLevelData
    {
        public string id;
        public int level;
        public int xp;
    }

    [System.Serializable]
    public class PlayerPetData
    {
        public string pet_id;
        public string uid;
    }
}
