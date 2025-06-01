using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NetcodePlus;
using Unity.Netcode;

namespace SurvivalEngine
{

    /// <summary>
    /// Plants can be sowed (from a seed) and their fruit can be harvested. They can also have multiple growth stages.
    /// </summary>

    [RequireComponent(typeof(Selectable))]
    [RequireComponent(typeof(Buildable))]
    [RequireComponent(typeof(UniqueID))]
    [RequireComponent(typeof(Destructible))]
    public class Plant : Craftable
    {
        [Header("Plant")]
        public PlantData data;
        public int growth_stage = 0;

        [Header("Time")]
        public TimeType time_type = TimeType.GameHours; //Time type (days or hours) for grow_time and fruit_grow_time
        public float water_duration = 24f;              //How long the water lasts

        [Header("Growth")]
        public float grow_time = 8f;        
        public bool grow_require_water = false;
        public bool regrow_on_death;        //If true, will go back to stage 1 instead of being destroyed
        public float soil_range = 1f;       //How far the watered soil can be from the plant

        [Header("Harvest")]
        public ItemData fruit;              //Item that can be harvested
        public float fruit_grow_time = 0f;  
        public bool fruit_require_water = false;
        public Transform fruit_model;       //3D model of the fruit
        public bool death_on_harvest;

        [Header("FX")]
        public GameObject gather_fx;
        public AudioClip gather_audio;

        private Selectable selectable;
        private Buildable buildable;
        private Destructible destruct;
        private UniqueID unique_id;
        private Soil soil;

        private int nb_stages = 1;
        private float update_timer = 0f;
        private bool need_refresh = false;

        private SNetworkActions actions;

        private static List<Plant> plant_list = new List<Plant>();

        protected override void Awake()
        {
            base.Awake();
            plant_list.Add(this);
            selectable = GetComponent<Selectable>();
            buildable = GetComponent<Buildable>();
            destruct = GetComponent<Destructible>();
            unique_id = GetComponent<UniqueID>();
            selectable.onDestroy += OnDeath;
            buildable.onBuild += OnBuild;

            if(data != null)
                nb_stages = Mathf.Max(data.growth_stage_prefabs.Length, 1);
        }

        void OnDestroy()
        {
            plant_list.Remove(this);
        }

        protected override void OnReady()
        {
            base.OnReady();

            if (!unique_id.WasCreated && WorldData.Get().IsObjectRemoved(GetUID()))
            {
                NetObject.Destroy();
                return;
            }

            //Soil
            if (!buildable.IsBuilding())
                soil = Soil.GetNearest(transform.position, soil_range);

            //Grow time
            if (!IsFullyGrown() && !unique_id.HasCustomFloat("grow_time"))
                ResetGrowTime();
            if (fruit != null && !unique_id.HasCustomFloat("fruit_time"))
                ResetFruitTime();
            if (water_duration > 0.001f && !unique_id.HasCustomFloat("water_time"))
                ResetWaterTime();

            RefreshFruitModel();
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();

            actions = new SNetworkActions(this);
            actions.RegisterRefresh(RefreshType.RefreshObject, DoReceiveRefresh);
            need_refresh = true; //Refresh next update
        }

        protected override void OnDespawn()
        {
            base.OnDespawn();
            actions.Clear();
        }

        protected void Update()
        {
            if (TheGame.Get().IsPaused())
                return;

            if (buildable.IsBuilding())
                return;

            if (!IsReady)
                return;

            update_timer += Time.deltaTime;
            if (update_timer > 0.5f)
            {
                update_timer = Random.Range(-0.1f, 0.1f);
                SlowUpdate();
            }
        }

        private void SlowUpdate()
        {
            if (!IsFullyGrown() && HasUID())
            {
                bool can_grow = !grow_require_water || HasWater();
                if (can_grow && IsGrowTimeFinished())
                {
                    GrowPlant();
                    return;
                }
            }

            if (fruit != null && !HasFruit() && HasUID())
            {
                bool can_grow = !fruit_require_water || HasWater();
                if (can_grow && IsFruitTimeFinished())
                {
                    GrowFruit();
                    return;
                }
            }

            if (water_duration > 0.001f && HasUID())
            {
                if (HasWater() && IsWaterTimeFinished())
                    RemoveWater();
            }

            //Auto water
            if (!HasWater())
            {
                if (TheGame.Get().IsWeather(WeatherEffect.Rain))
                    Water();
                Sprinkler nearest = Sprinkler.GetNearestInRange(transform.position);
                if (nearest != null)
                    Water();
            }

            //Refresh
            if (need_refresh)
                RefreshPlant();
        }

        private void DoReceiveRefresh(SerializedData sdata)
        {
            if (IsServer)
                return;

            PlantSyncState state = sdata.Get<PlantSyncState>();
            if (state != null)
            {
                if (!IsFullyGrown())
                    unique_id.SetCustomFloat("grow_time", state.grow_time);
                if (fruit != null)
                    unique_id.SetCustomFloat("fruit_time", state.fruit_time);
                if (water_duration  > 0.001f)
                    unique_id.SetCustomFloat("water_time", state.water_time);

                if (state.water != HasWater())
                {
                    if (state.water)
                        Water();
                    else
                        RemoveWater();
                }

                if (state.fruit != HasFruit())
                {
                    if (state.water)
                        GrowFruit();
                    else
                        RemoveFruit();
                }
            }
        }

        private void RefreshPlant()
        {
            need_refresh = false;

            PlantSyncState state = new PlantSyncState(HasFruit(), HasWater());
            state.grow_time = GetGrowTime();
            state.fruit_time = GetFruitTime();
            state.water_time = GetWaterTime();
            actions?.Refresh(RefreshType.RefreshObject, state); // DoReceiveState(state)
        }

        public void GrowPlant()
        {
            if (!IsFullyGrown())
            {
                GrowPlant(growth_stage + 1);
            }
        }

        public void GrowPlant(int grow_stage)
        {
            if (data != null && grow_stage >= 0 && grow_stage < nb_stages)
            {
                RemoveWater(); //Remove soil water
                WorldData.Get().RemovePlant(GetUID());
                if (!unique_id.WasCreated)
                    WorldData.Get().RemoveObject(GetUID());
                unique_id.RemoveAllSubUIDs();
                NetObject.Destroy();

                Create(data, transform.position, transform.rotation, grow_stage);
            }
        }

        public void GrowFruit()
        {
            if (fruit != null && !HasFruit())
            {
                unique_id.SetCustomInt("fruit", 1);
                RefreshFruitModel();
                RemoveWater();
                RefreshPlant();
            }
        }

        public void Harvest(PlayerCharacter character)
        {
            if (fruit != null && HasFruit() && character.Inventory.CanTakeItem(fruit, 1))
            {
                GameObject source = fruit_model != null ? fruit_model.gameObject : gameObject;
                character.Inventory.GainItem(fruit, 1, source.transform.position);

                RemoveFruit();

                if (death_on_harvest && destruct != null)
                    destruct.Kill();

                TheAudio.Get().PlaySFX3D("plant", gather_audio, transform.position);

                if (gather_fx != null)
                    Instantiate(gather_fx, transform.position, Quaternion.identity);
            }
        }

        public void RemoveFruit()
        {
            if (fruit != null && HasFruit())
            {
                unique_id.SetCustomInt("fruit", 0);
                RefreshFruitModel();
                ResetFruitTime();
                RefreshPlant();
            }
        }

        public void Water()
        {
            if (!HasWater())
            {
                unique_id.SetCustomInt("water", 1);
                if (soil != null)
                    soil.Water();

                if (grow_require_water)
                    ResetGrowTime();
                if (fruit != null && fruit_require_water)
                    ResetFruitTime();

                ResetWaterTime();
                RefreshPlant();
            }
        }

        public void RemoveWater()
        {
            if (HasWater())
            {
                unique_id.SetCustomInt("water", 0);
                if (soil != null)
                    soil.RemoveWater();

                ResetWaterTime();
                RefreshPlant();
            }
        }

        private void RefreshFruitModel()
        {
            if (fruit_model != null && HasFruit() != fruit_model.gameObject.activeSelf)
                fruit_model.gameObject.SetActive(HasFruit());
        }

        private void ResetTime()
        {
            if(!IsFullyGrown())
                ResetGrowTime();
            if (fruit != null)
                ResetFruitTime();
            if (water_duration > 0.001f)
                ResetWaterTime();
        }

        private void ResetGrowTime()
        {
            ResetTime("grow_time");
        }

        private bool IsGrowTimeFinished()
        {
            return IsTimeFinished("grow_time", grow_time);
        }

        private float GetGrowTime()
        {
            return unique_id.GetCustomFloat("grow_time");
        }

        private void ResetFruitTime()
        {
            ResetTime("fruit_time");
        }

        private bool IsFruitTimeFinished()
        {
            return IsTimeFinished("fruit_time", fruit_grow_time);
        }

        private float GetFruitTime()
        {
            return unique_id.GetCustomFloat("fruit_time");
        }

        private void ResetWaterTime()
        {
            ResetTime("water_time");
        }

        private bool IsWaterTimeFinished()
        {
            return IsTimeFinished("water_time", water_duration);
        }

        private float GetWaterTime()
        {
            return unique_id.GetCustomFloat("water_time");
        }

        private void ResetTime(string id)
        {
            if (time_type == TimeType.GameDays)
                unique_id.SetCustomFloat(id, WorldData.Get().day);
            if (time_type == TimeType.GameHours)
                unique_id.SetCustomFloat(id, WorldData.Get().GetTotalTime());
        }

        private bool IsTimeFinished(string id, float duration)
        {
            float last_grow_time = unique_id.GetCustomFloat(id);
            if (time_type == TimeType.GameDays && HasUID())
                return WorldData.Get().day >= Mathf.RoundToInt(last_grow_time + duration);
            if (time_type == TimeType.GameHours && HasUID())
                return WorldData.Get().GetTotalTime() > last_grow_time + duration;
            return false;
        }

        public void Kill()
        {
            destruct.Kill();
        }

        public void KillNoLoot()
        {
            destruct.KillNoLoot(); //Such as when being eaten, dont spawn loot
        }

        private void OnBuild()
        {
            if (data != null)
            {
                SowedPlantData splant = WorldData.Get().AddPlant(data.id, SceneNav.GetCurrentScene(), transform.position, transform.rotation, growth_stage);
                unique_id.unique_id = splant.uid;
                soil = Soil.GetNearest(transform.position, soil_range); 
                NetObject.SetOptimizerEnabled(true);
                NetObject.Spawn();
                ResetTime();
            }
        }

        private void OnDeath()
        {
            if (data != null)
            {
                foreach (PlayerCharacter character in PlayerCharacter.GetAll())
                    character.SaveData.AddKillCount(data.id); //Add kill count
            }

            WorldData.Get().RemovePlant(GetUID());
            if (!unique_id.WasCreated)
                WorldData.Get().RemoveObject(GetUID());

            if (HasFruit())
                Item.Create(fruit, transform.position, 1);

            if (data != null && regrow_on_death)
            {
                Create(data, transform.position, transform.rotation, 0);
            }
        }

        public bool HasFruit()
        {
            return fruit != null && unique_id.GetCustomInt("fruit") > 0;
        }

        public bool HasWater()
        {
            return unique_id.GetCustomInt("water") > 0;
        }

        public bool IsFullyGrown()
        {
            return (growth_stage + 1) >= nb_stages;
        }

        public bool IsBuilt()
        {
            return !IsDead() && !buildable.IsBuilding();
        }

        public bool IsDead()
        {
            return destruct.IsDead();
        }
        
        public bool HasUID()
        {
            return !string.IsNullOrEmpty(unique_id.unique_id);
        }

        public string GetUID()
        {
            return unique_id.unique_id;
        }

        public string GetSubUID(string tag)
        {
            return unique_id.GetSubUID(tag);
        }

        public bool HasGroup(GroupData group)
        {
            if (data != null)
                return data.HasGroup(group) || selectable.HasGroup(group);
            return selectable.HasGroup(group);
        }

        public Buildable Buildlable { get { return buildable; } }

        public SowedPlantData SaveData
        {
            get { return WorldData.Get().GetSowedPlant(GetUID()); }  //Can be null if not sowed or spawned
        }

        public static Plant GetNearest(Vector3 pos, float range = 999f)
        {
            Plant nearest = null;
            float min_dist = range;
            foreach (Plant plant in plant_list)
            {
                float dist = (plant.transform.position - pos).magnitude;
                if (dist < min_dist && plant.IsBuilt())
                {
                    min_dist = dist;
                    nearest = plant;
                }
            }
            return nearest;
        }

        public static int CountInRange(Vector3 pos, float range)
        {
            int count = 0;
            foreach (Plant plant in GetAll())
            {
                float dist = (plant.transform.position - pos).magnitude;
                if (dist < range && plant.IsBuilt())
                    count++;
            }
            return count;
        }

        public static new int CountInRange(IdData data, Vector3 pos, float range)
        {
            int count = 0;
            foreach (Plant plant in GetAll())
            {
                if (plant.data == data && plant.IsBuilt())
                {
                    float dist = (plant.transform.position - pos).magnitude;
                    if (dist < range)
                        count++;
                }
            }
            return count;
        }

        public static new int CountInRegion(IdData data, Zone zone)
        {
            int count = 0;
            foreach (Plant plant in GetAll())
            {
                if (plant.data == data && plant.IsBuilt() && zone.IsInside(plant.transform.position))
                {
                    count++;
                }
            }
            return count;
        }

        public static Plant GetByUID(string uid)
        {
            if (!string.IsNullOrEmpty(uid))
            {
                foreach (Plant plant in plant_list)
                {
                    if (plant.GetUID() == uid)
                        return plant;
                }
            }
            return null;
        }

        public static new List<SObject> GetAllOf(IdData data)
        {
            List<SObject> valid_list = new List<SObject>();
            foreach (Plant plant in plant_list)
            {
                if (plant.data == data && plant.IsBuilt())
                    valid_list.Add(plant);
            }
            return valid_list;
        }

        public static List<Plant> GetAll()
        {
            return plant_list;
        }

        //Spawn an existing one in the save file (such as after loading)
        public static Plant Spawn(string uid, Transform parent = null)
        {
            SowedPlantData sdata = WorldData.Get().GetSowedPlant(uid);
            if (sdata != null && sdata.scene == SceneNav.GetCurrentScene())
            {
                PlantData pdata = PlantData.Get(sdata.plant_id);
                if (pdata != null)
                {
                    GameObject prefab = pdata.GetStagePrefab(sdata.growth_stage);
                    GameObject build = Instantiate(prefab, sdata.pos, sdata.rot);
                    build.transform.parent = parent;

                    Plant plant = build.GetComponent<Plant>();
                    plant.data = pdata;
                    plant.growth_stage = sdata.growth_stage;
                    plant.unique_id.was_created = true;
                    plant.unique_id.unique_id = uid;
                    plant.NetObject.Spawn();
                    return plant;
                }
            }
            return null;
        }

        //Create a totally new one, in build mode for player to place, will be saved after FinishBuild() is called, -1 = max stage
        public static Plant CreateBuildMode(PlantData data, Vector3 pos, int stage)
        {
            GameObject prefab = data.GetStagePrefab(stage);
            GameObject build = Instantiate(prefab, pos, prefab.transform.rotation);
            Plant plant = build.GetComponent<Plant>();
            plant.data = data;
            plant.unique_id.was_created = true;
            plant.NetObject.SetOptimizerEnabled(false); //Disable optimizer while in build mode

            if (stage >= 0 && stage < data.growth_stage_prefabs.Length)
                plant.growth_stage = stage;
            
            return plant;
        }

        //Create a totally new one that will be added to save file, already placed
        public static Plant Create(PlantData data, Vector3 pos, int stage)
        {
            if (TheNetwork.Get().IsServer)
            {
                Plant plant = CreateBuildMode(data, pos, stage);
                plant.buildable.FinishBuild();
                return plant;
            }
            return null;
        }

        public static Plant Create(PlantData data, Vector3 pos, Quaternion rot, int stage)
        {
            if (TheNetwork.Get().IsServer)
            {
                Plant plant = CreateBuildMode(data, pos, stage);
                plant.transform.rotation = rot;
                plant.buildable.FinishBuild();
                return plant;
            }
            return null;
        }
    }

    public class PlantSyncState : INetworkSerializable
    {
        public bool fruit;
        public bool water;
        public float grow_time;
        public float fruit_time;
        public float water_time;

        public PlantSyncState() { }
        public PlantSyncState(bool f, bool w) { fruit = f; water = w; }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref fruit);
            serializer.SerializeValue(ref water);
            serializer.SerializeValue(ref grow_time);
            serializer.SerializeValue(ref fruit_time);
            serializer.SerializeValue(ref water_time);
        }
    }

}