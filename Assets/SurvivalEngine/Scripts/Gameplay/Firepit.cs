using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NetcodePlus;
using Unity.Netcode;

namespace SurvivalEngine
{

    /// <summary>
    /// Firepits can be fueled with wood or other materials. Will be lit until it run out of fuel
    /// </summary>

    [RequireComponent(typeof(Selectable))]
    [RequireComponent(typeof(Construction))]
    public class Firepit : SNetworkBehaviour
    {
        public GroupData fire_group;
        public GameObject fire_fx;
        public GameObject fuel_model;

        public float start_fuel = 10f;
        public float max_fuel = 50f;
        public float fuel_per_hour = 1f; //In Game hours
        public float wood_add_fuel = 2f;

        [Header("Client Sync")]
        public float sync_refresh_rate = 0.2f;

        private Selectable select;
        private Construction construction;
        private Buildable buildable;
        private UniqueID unique_id;
        private HeatSource heat_source;

        private bool is_on = false;
        private float fuel = 0f;
        private float refresh_timer = 0f;

        private SNetworkActions actions;

        private static List<Firepit> firepit_list = new List<Firepit>();

        protected override void Awake()
        {
            base.Awake();
            firepit_list.Add(this);
            select = GetComponent<Selectable>();
            construction = GetComponent<Construction>();
            buildable = GetComponent<Buildable>();
            unique_id = GetComponent<UniqueID>();
            heat_source = GetComponent<HeatSource>();
            if (fire_fx)
                fire_fx.SetActive(false);
            if (fuel_model)
                fuel_model.SetActive(false);

            select.RemoveGroup(fire_group);
            buildable.onBuild += OnBuild;
        }

        private void OnDestroy()
        {
            firepit_list.Remove(this);
        }

        private void Start()
        {
            if (!unique_id.WasCreated && !buildable.IsBuilding())
                fuel = start_fuel;
            if (WorldData.Get().HasCustomFloat(GetFireUID()))
                fuel = WorldData.Get().GetCustomFloat(GetFireUID());
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();
            actions = new SNetworkActions(this);
            actions.RegisterRefresh(RefreshType.CustomFloat, OnRefresh, NetworkDelivery.Unreliable);
        }

        protected override void OnDespawn()
        {
            base.OnDespawn();
            actions.Clear();
        }

        void Update()
        {
            if (TheGame.Get().IsPaused())
                return;
            
            if (is_on)
            {
                float game_speed = TheGame.Get().GetGameTimeSpeedPerSec();
                fuel -= fuel_per_hour * game_speed * Time.deltaTime;
                UpdateRefresh();
            }

            is_on = fuel > 0f;
            if (fire_fx)
                fire_fx.SetActive(is_on);
            if (fuel_model)
                fuel_model.SetActive(fuel > 0f);

            if (is_on)  
                select.AddGroup(fire_group);
            else
                select.RemoveGroup(fire_group);

            if (heat_source != null)
                heat_source.enabled = is_on;
        }

        public void AddFuel(float value)
        {
            fuel += value;
            is_on = fuel > 0f;

            WorldData.Get().SetCustomFloat(GetFireUID(), fuel);
            Refresh();
        }

        private void OnBuild()
        {
            fuel = start_fuel;
        }

        public string GetFireUID()
        {
            if(!string.IsNullOrEmpty(unique_id.unique_id))
                return unique_id.unique_id + "_fire";
            return "";
        }

        public bool IsOn()
        {
            return is_on;
        }

        private void UpdateRefresh()
        {
            refresh_timer += Time.deltaTime;
            if (refresh_timer < sync_refresh_rate)
                return;
            refresh_timer = 0f;
            WorldData.Get().SetCustomFloat(GetFireUID(), fuel);
            Refresh();
        }

        private void Refresh()
        {
            if (actions != null)
            {
                RefreshCustomFloat value = new RefreshCustomFloat(GetFireUID(), fuel);
                actions.Refresh(RefreshType.CustomFloat, value);
            }
        }

        private void OnRefresh(SerializedData rdata)
        {
            RefreshCustomFloat value = rdata.Get<RefreshCustomFloat>();
            WorldData.Get().OverrideCustomFloat(value.uid, value.value);
            fuel = value.value;
        }

        public static Firepit GetNearest(Vector3 pos, float range=999f)
        {
            float min_dist = range;
            Firepit nearest = null;
            foreach (Firepit fire in firepit_list)
            {
                float dist = (pos - fire.transform.position).magnitude;
                if (dist < min_dist)
                {
                    min_dist = dist;
                    nearest = fire;
                }
            }
            return nearest;
        }

        public static List<Firepit> GetAll()
        {
            return firepit_list;
        }
    }

}
