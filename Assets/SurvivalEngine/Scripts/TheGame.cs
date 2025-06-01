using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using NetcodePlus;
using Unity.Netcode;

namespace SurvivalEngine
{

    /// <summary>
    /// Game Manager Script for Survival Engine
    /// Author: Indie Marc (Marc-Antoine Desbiens)
    /// </summary>

    [DefaultExecutionOrder(-1)]
    public class TheGame : SMonoBehaviour
    {
        //non-static UnityActions only work in a game scene that uses TheGame.cs
        public UnityAction<bool> onPause; //When pausing/unpausing the game
        public UnityAction onStartNewGame; //After creating a new game and after the game scene has been loaded, only first time if its a new game.
        public UnityAction onNewDay; //Right after changing day (if using sleep function)
        public UnityAction<string> beforeSave; //Right after calling Save(), before writing the file on disk
        public UnityAction<float> onSkipTime; //When sleeping (skipping time), before changing scene. <float> is how many in-game hours are skipped

        //static UnityActions work in any scene (including Menu scenes that don't have TheGame.cs)
        public static UnityAction afterLoad; //Right after calling Load(), after loading the PlayerData but before changing scene
        public static UnityAction afterNewGame; //Right after calling NewGame(), after creating the PlayerData but before changing scene
        public static UnityAction<string> beforeChangeScene; //Right before changing scene (for any reason)

        private bool paused_by_player = false;
        private bool paused_by_script = false;
        private float speed_multiplier = 1f;
        private bool scene_transition = false;
        private float game_speed = 1f;
        private float game_speed_per_sec = 0.002f;
        private float update_timer = 0f;
        private bool is_saving = false;

        private SNetworkActions actions;

        private List<string> remove_items_uid = new List<string>();
        private List<BonusType> remove_bonus_list = new List<BonusType>();
        private List<RegrowthData> spawn_growth_list = new List<RegrowthData>();

        private static TheGame instance;

        protected override void Awake()
        {
            base.Awake();
            instance = this;
            Application.targetFrameRate = 120;

            PlayerData.LoadLast();
            WorldData.LoadLast();
            SettingData.LoadLast();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            instance = null;
            Selectable.ClearAll();
        }

        protected override void OnReady()
        {
            //Actions
            actions = new SNetworkActions(1); //Use custom NetworkID 1 for this action since it doesnt have a NetworkObject attached
            actions.RegisterRefresh(RefreshType.GameTime, ReceiveRefresh, NetworkDelivery.Unreliable);
            actions.Register(ActionType.Transition, OnTransition);
            actions.Register(ActionType.TransitionScene, OnTransitionScene);
        }

        protected override void OnAfterReady()
        {
            //Set current scene
            WorldData pdata = WorldData.Get();
            string scene = SceneNav.GetCurrentScene();
            pdata.scene = scene;

            SpawnObjects();

            //New game
            if (pdata.IsNewGame())
            {
                onStartNewGame?.Invoke(); //New Game!
            }

            //New day
            if (pdata.new_day)
            {
                pdata.new_day = false;
                pdata.day_time = GameData.Get().start_day_time; //Sete start of day time
                onNewDay?.Invoke();
            }
        }

        private void SpawnObjects()
        {
            if (!TheNetwork.Get().IsServer)
                return;

            WorldData pdata = WorldData.Get();
            GameObject spawn_parent = new GameObject("SaveFileSpawns");

            //Spawn constructions (do this first because they may be big, have colliders, entry zones that affect the player)
            foreach (KeyValuePair<string, BuiltConstructionData> elem in pdata.built_constructions)
            {
                Construction.Spawn(elem.Key, spawn_parent.transform);
            }

            //Spawn characters
            foreach (KeyValuePair<string, TrainedCharacterData> elem in pdata.trained_characters)
            {
                Character.Spawn(elem.Key, spawn_parent.transform);
            }

            //Spawn plants
            foreach (KeyValuePair<string, SowedPlantData> elem in pdata.sowed_plants)
            {
                Plant.Spawn(elem.Key, spawn_parent.transform);
            }

            //Spawn others
            foreach (KeyValuePair<string, SpawnedData> elem in pdata.spawned_objects)
            {
                Spawnable.Spawn(elem.Key, spawn_parent.transform);
            }

            //Spawn dropped items
            foreach (KeyValuePair<string, DroppedItemData> elem in pdata.dropped_items)
            {
                Item.Spawn(elem.Key, spawn_parent.transform);
            }
        }

        void Update()
        {
            if (!TheNetwork.Get().IsReady())
                return;

            if (IsPaused())
                return;

            //Game speed
            game_speed = speed_multiplier * GameData.Get().game_time_mult;
            game_speed_per_sec = game_speed / 3600f;

            //Game time
            WorldData pdata = WorldData.Get();
            pdata.day_time += game_speed_per_sec * Time.deltaTime;
            if (pdata.day_time >= 24f)
            {
                pdata.day_time = 0f;
                pdata.day++; //New day
            }

            //Play time
            pdata.play_time += Time.deltaTime;

            //Inventory durability
            UpdateDurability(game_speed_per_sec * Time.deltaTime);

            //Slow Update
            update_timer += Time.deltaTime;
            if (update_timer > 1f)
            {
                update_timer = 0f;
                SlowUpdate();
            }

            //Client update
            UpdateClient();
        }

        private void SlowUpdate()
        {
            RefreshGameTime gtime = new RefreshGameTime(WorldData.Get().day, WorldData.Get().day_time);
            actions.Refresh(RefreshType.GameTime, gtime);
        }

        private void UpdateClient()
        {
            if (!TheNetwork.Get().IsClient)
                return;

            //Set music
            AudioClip[] music_playlist = AssetData.Get().music_playlist;
            if (music_playlist != null && music_playlist.Length > 0 && !TheAudio.Get().IsMusicPlaying("music"))
            {
                AudioClip clip = music_playlist[Random.Range(0, music_playlist.Length)];
                TheAudio.Get().PlayMusic("music", clip, 0.4f, false);
            }
        }

        private void ReceiveRefresh(SerializedData rdata)
        {
            RefreshGameTime gtime = rdata.Get<RefreshGameTime>();
            WorldData sdata = WorldData.Get();
            sdata.day = gtime.day;
            sdata.day_time = gtime.day_time;
        }

        private void UpdateDurability(float game_hours)
        {
            WorldData wdata = WorldData.Get();
            remove_items_uid.Clear();

            //Dropped
            foreach (KeyValuePair<string, DroppedItemData> pair in wdata.dropped_items)
            {
                DroppedItemData ddata = pair.Value;
                ItemData idata = ItemData.Get(ddata?.item_id);

                if (idata != null && ddata != null && idata.durability_type == DurabilityType.Spoilage)
                {
                    ddata.durability -= game_hours;
                }

                if (idata != null && ddata != null && idata.HasDurability() && ddata.durability <= 0f)
                    remove_items_uid.Add(pair.Key);
            }

            foreach (string uid in remove_items_uid)
            {
                Item item = Item.GetByUID(uid);
                if (item != null)
                    item.SpoilItem();
            }
            remove_items_uid.Clear();

            //World Inventory
            foreach (KeyValuePair<string, InventoryData> spair in wdata.inventories)
            {
                if (spair.Value != null)
                    spair.Value.UpdateAllDurability(game_hours);
            }

            //Players Inventory
            foreach (KeyValuePair<int, PlayerData> ppair in wdata.players)
            {
                foreach (KeyValuePair<string, InventoryData> spair in ppair.Value.inventories)
                {
                    if (spair.Value != null)
                        spair.Value.UpdateAllDurability(game_hours);
                }
            }

            //Constructions
            foreach (KeyValuePair<string, BuiltConstructionData> pair in wdata.built_constructions)
            {
                BuiltConstructionData bdata = pair.Value;
                ConstructionData cdata = ConstructionData.Get(bdata?.construction_id);

                if (cdata != null && bdata != null && (cdata.durability_type == DurabilityType.Spoilage || cdata.durability_type == DurabilityType.UsageTime))
                {
                    bdata.durability -= game_hours;
                }

                if (cdata != null && bdata != null && cdata.HasDurability() && bdata.durability <= 0f)
                    remove_items_uid.Add(pair.Key);
            }

            foreach (string uid in remove_items_uid)
            {
                Construction item = Construction.GetByUID(uid);
                if (item != null)
                    item.Kill();
            }
            remove_items_uid.Clear();

            //Timed bonus
            foreach (KeyValuePair <int, PlayerData> pcdata in WorldData.Get().players)
            {
                remove_bonus_list.Clear();
                foreach (KeyValuePair<BonusType, TimedBonusData> pair in pcdata.Value.timed_bonus_effects)
                {
                    TimedBonusData bdata = pair.Value;
                    bdata.time -= game_hours;

                    if (bdata.time <= 0f)
                        remove_bonus_list.Add(pair.Key);
                }
                foreach (BonusType bonus in remove_bonus_list)
                    pcdata.Value.RemoveTimedBonus(bonus);
                remove_bonus_list.Clear();
            }

            //World regrowth
            spawn_growth_list.Clear();
            foreach (KeyValuePair<string, RegrowthData> pair in WorldData.Get().world_regrowth)
            {
                RegrowthData bdata = pair.Value;
                bdata.time -= game_hours;

                if (bdata.time <= 0f && bdata.scene == SceneNav.GetCurrentScene())
                    spawn_growth_list.Add(pair.Value);
            }

            foreach (RegrowthData regrowth in spawn_growth_list)
            {
                Regrowth.SpawnRegrowth(regrowth);
                WorldData.Get().RemoveWorldRegrowth(regrowth.uid);
            }
        }

        public float GetTimestamp()
        {
            WorldData sdata = WorldData.Get();
            return sdata.day * 24f + sdata.day_time;
        }

        public bool IsNight()
        {
            WorldData pdata = WorldData.Get();
            GameData gdata = GameData.Get();
            return pdata.day_time >= gdata.end_day_time || pdata.day_time < gdata.start_day_time;
        }

        public bool IsWeather(WeatherEffect effect)
        {
            if (WeatherSystem.Get() != null)
                return WeatherSystem.Get().HasWeatherEffect(effect);
            return false;
        }

        //Set to 1f for default speed
        public void SetGameSpeedMultiplier(float mult)
        {
            speed_multiplier = mult;
        }

        //Game hours per real time hours
        public float GetGameTimeSpeed()
        {
            return game_speed;
        }

        //Game hours per real time seconds
        public float GetGameTimeSpeedPerSec()
        {
            return game_speed_per_sec;
        }

        //---- Pause / Unpause -----

        public void Pause()
        {
            paused_by_player = true;
            onPause?.Invoke(IsPaused());
        }

        public void Unpause()
        {
            paused_by_player = false;
            onPause?.Invoke(IsPaused());
        }

        public void PauseScripts()
        {
            paused_by_script = true;
            onPause?.Invoke(IsPaused());
        }

        public void UnpauseScripts()
        {
            paused_by_script = false;
            onPause?.Invoke(IsPaused());
        }

        public bool IsPaused()
        {
            return paused_by_player || paused_by_script;
        }

        public bool IsPausedByPlayer()
        {
            return paused_by_player;
        }

        public bool IsPausedByScript()
        {
            return paused_by_script;
        }

        //-- Scene transition -----

        public void TransitionToScene(string scene, string entry)
        {
            if (!TheNetwork.Get().IsServer)
                return; //Only server can change scene

            if (!scene_transition && !string.IsNullOrEmpty(scene))
            {
                if (SceneNav.DoSceneExist(scene))
                {
                    scene_transition = true;
                    StartCoroutine(GoToSceneRoutine(scene, entry));
                }
                else
                {
                    Debug.Log("Scene don't exist: " + scene);
                }
            }
        }

        private IEnumerator GoToSceneRoutine(string scene, string entry)
        {
            actions?.SetTarget(ActionType.TransitionScene, NetworkActionTarget.All);
            actions?.Trigger(ActionType.TransitionScene);
            yield return new WaitForSeconds(1f);
            GoToScene(scene, entry);
        }

        public static void GoToScene(string scene, string entry = "")
        {
            if (!TheNetwork.Get().IsServer)
                return; //Only server can change scene

            if (!string.IsNullOrEmpty(scene)) {

                WorldData sdata = WorldData.Get();
                sdata.scene = scene;
                sdata.entry = entry;

                if (beforeChangeScene != null)
                    beforeChangeScene.Invoke(scene);

                TheNetwork.Get().LoadScene(scene);
            }
        }

        // --- Same scene transition

        public void TeleportToZone(PlayerCharacter character, ExitZone zone)
        {
            if (!TheNetwork.Get().IsServer)
                return; //Only server

            if (character != null && zone != null)
            {
                StartCoroutine(TeleportToZoneRun(character, zone));
            }
        }

        private IEnumerator TeleportToZoneRun(PlayerCharacter character, ExitZone zone)
        {
            ClientData client = TheNetwork.Get().GetClientByPlayerID(character.player_id);
            if (client == null)
                yield break;

            actions?.SetTarget(ActionType.Transition, NetworkActionTarget.Single, client.client_id);
            actions?.Trigger(ActionType.Transition);
            zone.ResetTimer(); //Reset timer to avoid teleporting again

            yield return new WaitForSeconds(0.7f);

            Vector3 pos = zone.GetRandomPosition();
            zone.ResetTimer(); //Reset timer to avoid teleporting again
            character.Teleport(pos);
        }

        private void OnTransition()
        {
            StartCoroutine(OnTransitionRun());
        }
        
        private IEnumerator OnTransitionRun()
        {
            BlackPanel.Get().Show();

            yield return new WaitForSeconds(1.5f);

            BlackPanel.Get().Hide();
        }


        private void OnTransitionScene()
        {
            StartCoroutine(OnTransitionSceneRun());
        }

        private IEnumerator OnTransitionSceneRun()
        {
            yield return new WaitForSeconds(0.1f);
            BlackPanel.Get().Show();
        }

        // ----- Next day -----

        public void TransitionToNextDay()
        {
            if (!TheNetwork.Get().IsServer)
                return; //Only server can change day

            if (!scene_transition)
            {
                scene_transition = true;
                StartCoroutine(GoToDayRoutine());
            }
        }

        private IEnumerator GoToDayRoutine()
        {
            BlackPanel.Get().Show();
            yield return new WaitForSeconds(1f);
            GoToNextDay();
        }

        public void GoToNextDay()
        {
            if (!TheNetwork.Get().IsServer)
                return; //Only server can change day

            WorldData pdata = WorldData.Get();
            GameData gdata = GameData.Get();

            float skipped_time;
            if (pdata.day_time > gdata.start_day_time)
            {
                skipped_time = 24f - pdata.day_time + gdata.start_day_time;
            }
            else
            {
                skipped_time = gdata.start_day_time - pdata.day_time;
            }

            SkipTime(skipped_time);
            TheNetwork.Get().RestartScene();
        }

        //Skip X game hours
        public void SkipTime(float skipped_time)
        {
            WorldData sdata = WorldData.Get();

            sdata.day_time += skipped_time;

            while (sdata.day_time >= 24)
            {
                sdata.day++;
                sdata.day_time -= 24f;
                sdata.new_day = true;
            }

            UpdateDurability(skipped_time);

            if (onSkipTime != null)
                onSkipTime.Invoke(skipped_time);
        }

        public void QuitToMenu()
        {
            TheNetwork.Get().Disconnect();
            WorldData.Unload();
            SettingData.Get().Save();
            Menu.GoToLastMenu();
        }

        //---- Load / Save -----

        public bool IsSaving()
        {
            return is_saving;
        }
        
        //Either save the file directly (server) or first ask save file from server and then save (client)
        public void Save()
        {
            if (TheNetwork.Get().IsServer)
            {
                //Save is local, can save now
                is_saving = true;
                Save(WorldData.Get().filename);
                is_saving = false;
            }
            else
            {
                //Download Save from server before saving..
                is_saving = true;
                TheNetwork.Get().RequestWorld(() =>
                {
                    Save(WorldData.Get().filename);
                    is_saving = false;
                });
            }
        }

        public void SaveLocal()
        {
            is_saving = true;
            Save(WorldData.Get().filename);
            is_saving = false;
        }

        public bool Save(string filename)
        {
            if (!SaveTool.IsValidFilename(filename))
                return false; //Failed

            BeforeSave(filename);

            PlayerData.GetLoaded().Save();
            WorldData.Get().Save(filename);
            return true;
        }

        public static void Load()
        {
            Load(WorldData.GetLastSave());
        }

        public static bool Load(string filename)
        {
            if (!TheNetwork.Get().IsServer)
                return false;

            if (!SaveTool.IsValidFilename(filename))
                return false; //Failed

            Debug.Log("Load Game: " + filename);

            WorldData.Load(filename);

            AfterLoad();

            TheNetwork.Get().LoadScene(WorldData.Get().scene);
            return true;
        }

        public static void NewGame()
        {
            NewGame(WorldData.GetLastSave(), SceneNav.GetCurrentScene());
        }

        public static bool NewGame(string filename, string scene)
        {
            if (!TheNetwork.Get().IsServer)
                return false;

            if (!SaveTool.IsValidFilename(filename))
                return false; //Failed

            Debug.Log("New Game: " + filename);

            WorldData.NewGame(filename);

            if(TheNetwork.Get().IsClient && GameData.Get().save_type == GameSaveType.SplitSave)
                PlayerData.CopyToWorld(TheNetwork.Get().PlayerID);

            if (afterNewGame != null)
                afterNewGame.Invoke();

            TheNetwork.Get().LoadScene(scene);
            return true;
        }

        public static bool NewOrLoad(string filename, string scene)
        {
            if (!TheNetwork.Get().IsServer)
                return false;

            if (WorldData.HasSave(filename))
                return Load(filename);
            else
                return NewGame(filename, scene);
        }

        public static void DeleteGame(string filename)
        {
            WorldData.Delete(filename);
        }

        //---------

        private void BeforeSave(string filename)
        {
            WorldData world = WorldData.Get();
            world.scene = SceneNav.GetCurrentScene();

            foreach (PlayerCharacter player in PlayerCharacter.GetAll())
            {
                player.SaveData.position = player.transform.position;
                player.SaveData.scene = SceneNav.GetCurrentScene();
                player.SaveData.world = world.world_id;
            }

            if (beforeSave != null)
                beforeSave.Invoke(filename);

            PlayerData.CopyFromWorld(TheNetwork.Get().PlayerID);
        }

        private static void AfterLoad()
        {
            if (TheNetwork.Get().IsClient && GameData.Get().save_type == GameSaveType.SplitSave)
                PlayerData.CopyToWorld(TheNetwork.Get().PlayerID);

            if (afterLoad != null)
                afterLoad.Invoke();
        }

        //--------

        public static bool IsMobile()
        {
#if UNITY_ANDROID || UNITY_IOS || UNITY_TIZEN
            return true;
#elif UNITY_WEBGL
            return WebGLTool.isMobile();
#else
            return false;
#endif
        }

        public static bool IsValid()
        {
            //Game is valid if SaveData is loaded and game scene is loaded
            return instance != null && WorldData.Get() != null;
        }

        //Use this instead of Get() when calling from Awake function
        public static TheGame Find()
        {
            if (instance == null)
                instance = FindObjectOfType<TheGame>();
            return instance;
        }

        public static TheGame Get()
        {
            return instance;
        }
    }

}