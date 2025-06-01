using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NetcodePlus;
using Unity.Netcode;

namespace SurvivalEngine
{
    /// <summary>
    /// Soil that can be watered or not
    /// </summary>

    [RequireComponent(typeof(UniqueID))]
    public class Soil : SNetworkBehaviour
    {
        public MeshRenderer mesh;
        public Material watered_mat;

        private UniqueID unique_id;
        private Material original_mat;
        private bool watered = false;
        private float update_timer = 0f;

        private SNetworkActions actions;

        private static List<Soil> soil_list = new List<Soil>();

        protected override void Awake()
        {
            base.Awake();
            soil_list.Add(this);
            unique_id = GetComponent<UniqueID>();
            if(mesh != null)
                original_mat = mesh.material;
        }

        private void OnDestroy()
        {
            soil_list.Remove(this);
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();
            actions = new SNetworkActions(this);
            actions.RegisterRefresh(RefreshType.RefreshObject, DoReceiveRefresh);
        }

        protected override void OnDespawn()
        {
            base.OnDespawn();
            actions.Clear();
        }

        void Update()
        {
            bool now_watered = IsWatered();
            if (now_watered != watered && mesh != null && watered_mat != null)
            {
                mesh.material = now_watered ? watered_mat : original_mat;
            }
            watered = now_watered;

            update_timer += Time.deltaTime;
            if (update_timer > 0.5f)
            {
                update_timer = 0f;
                SlowUpdate();
            }
        }

        private void SlowUpdate()
        {
            //Auto water
            if (!watered)
            {
                if (TheGame.Get().IsWeather(WeatherEffect.Rain))
                    Water();
                Sprinkler nearest = Sprinkler.GetNearestInRange(transform.position);
                if (nearest != null)
                    Water();
            }
        }

        private void DoReceiveRefresh(SerializedData sdata)
        {
            if (IsServer)
                return;

            SoilSyncState state = sdata.Get<SoilSyncState>();
            if (state != null && state.water != IsWatered())
            {
                if (state.water)
                    Water();
                else
                    RemoveWater();
            }
        }

        //Water the soil
        public void Water()
        {
            if (!IsWatered())
            {
                unique_id.SetCustomInt("water", 1);
                actions?.Refresh(RefreshType.RefreshObject, new SoilSyncState(true));
            }
        }

        public void RemoveWater()
        {
            if (IsWatered())
            {
                unique_id.SetCustomInt("water", 0);
                actions?.Refresh(RefreshType.RefreshObject, new SoilSyncState(false));
            }
        }

        public bool IsWatered()
        {
            return unique_id.GetCustomInt("water") > 0;
        }

        public string GetSubUID(string tag)
        {
            return unique_id.GetSubUID(tag);
        }

        public static Soil GetNearest(Vector3 pos, float range=999f)
        {
            float min_dist = range;
            Soil nearest = null;
            foreach (Soil soil in soil_list)
            {
                float dist = (pos - soil.transform.position).magnitude;
                if (dist < min_dist)
                {
                    min_dist = dist;
                    nearest = soil;
                }
            }
            return nearest;
        }

        public static List<Soil> GetAll(){
            return soil_list;
        }
    }

    public class SoilSyncState : INetworkSerializable
    {
        public bool water;

        public SoilSyncState() { }
        public SoilSyncState(bool w) { water = w; }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref water);
        }
    }

}
