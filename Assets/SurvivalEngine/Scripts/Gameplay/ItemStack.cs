using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NetcodePlus;

namespace SurvivalEngine
{
    //Can stack many of only 1 type of item, (Not for inventory containers like Chest)

    [RequireComponent(typeof(Selectable))]
    [RequireComponent(typeof(UniqueID))]
    public class ItemStack : SNetworkBehaviour
    {
        public ItemData item;
        public int item_start = 0;
        public int item_max = 20;

        public GameObject item_mesh;

        private Selectable selectable;
        private UniqueID unique_id;

        private SNetworkActions actions;

        private static List<ItemStack> stack_list = new List<ItemStack>();

        protected override void Awake()
        {
            base.Awake();
            stack_list.Add(this);
            selectable = GetComponent<Selectable>();
            unique_id = GetComponent<UniqueID>();

        }

        private void OnDestroy()
        {
            stack_list.Remove(this);
        }

        private void Start()
        {
            if(!WorldData.Get().HasCustomInt(GetCountUID()))
                 WorldData.Get().SetCustomInt(GetCountUID(), item_start);
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();
            actions = new SNetworkActions(this);
            actions.RegisterInt("add", DoAddItem);
            actions.RegisterInt("remove", DoRemoveItem);
        }

        protected override void OnDespawn()
        {
            base.OnDespawn();
            actions.Clear();
        }

        void Update()
        {
            if (item_mesh != null)
            {
                bool active = GetItemCount() > 0;
                if (active != item_mesh.activeSelf)
                    item_mesh.SetActive(active);
            }
        }

        public void AddItem(int value)
        {
            actions?.Trigger("add", value);
        }

        public void RemoveItem(int value)
        {
            actions?.Trigger("remove", value);
        }

        private void DoAddItem(int value)
        {
            int val = GetItemCount();
            WorldData.Get().SetCustomInt(GetCountUID(), val + value);
        }

        private void DoRemoveItem(int value)
        {
            int val = GetItemCount();
            val -= value;
            val = Mathf.Max(val, 0);
            WorldData.Get().SetCustomInt(GetCountUID(), val);
        }

        public int GetItemCount()
        {
            return WorldData.Get().GetCustomInt(GetCountUID());
        }

        public string GetUID()
        {
            return unique_id.unique_id;
        }

        public string GetCountUID()
        {
            return unique_id.unique_id + "_count";
        }

        public static ItemStack GetNearest(Vector3 pos, float range = 999f)
        {
            float min_dist = range;
            ItemStack nearest = null;
            foreach (ItemStack item in stack_list)
            {
                float dist = (item.transform.position - pos).magnitude;
                if (dist < min_dist)
                {
                    min_dist = dist;
                    nearest = item;
                }
            }
            return nearest;
        }

        public static List<ItemStack> GetAll()
        {
            return stack_list;
        }
    }

}
