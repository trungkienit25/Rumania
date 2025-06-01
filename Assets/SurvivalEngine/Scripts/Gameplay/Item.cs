using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using NetcodePlus;

namespace SurvivalEngine
{

    /// <summary>
    /// Items are objects that can be picked, dropped and held into the player's inventory. Some item can also be crafted or used as crafting material.
    /// </summary>

    [RequireComponent(typeof(Selectable))]
    [RequireComponent(typeof(UniqueID))]
    public class Item : Craftable
    {
        [Header("Item")]
        public ItemData data;
        public int quantity = 1;

        [Header("FX")]
        public float auto_collect_range = 0f; //Will automatically be collected when in range
        public bool snap_to_ground = true; //If true, item will be automatically placed on the ground instead of floating if spawns in the air
        public AudioClip take_audio;
        public GameObject take_fx;
        private float update_timer = 0f;

        public UnityAction onTake;
        public UnityAction onDestroy;

        private Selectable selectable;
        private UniqueID unique_id;

        private static List<Item> item_list = new List<Item>();

        protected override void Awake()
        {
            base.Awake();
            item_list.Add(this);
            selectable = GetComponent<Selectable>();
            unique_id = GetComponent<UniqueID>();
            selectable.onUse += OnUse;
        }

        void OnDestroy()
        {
            item_list.Remove(this);
        }

        void Start()
        {
            if (snap_to_ground)
            {
                float dist;
                bool grounded = DetectGrounded(out dist);
                if (!grounded)
                {
                    transform.position += Vector3.down * dist;
                }
            }
        }

        protected override void OnReady()
        {
            base.OnReady();

            if (!unique_id.WasCreated && WorldData.Get().IsObjectRemoved(GetUID()))
            {
                NetObject.Destroy();
                return;
            }
        }

        protected void Update()
        {
            if (TheGame.Get().IsPaused())
                return;

            if (!IsServer)
                return; //Only server delete/auto take 

            //Item auto collect
            if (auto_collect_range > 0.1f)
            {
                PlayerCharacter player = PlayerCharacter.GetNearest(transform.position, auto_collect_range);
                if (player != null)
                    player.Inventory.AutoTakeItem(this);
            }

            //Slow Update
            update_timer += Time.deltaTime;
            if (update_timer > 0.5f)
            {
                update_timer = Random.Range(0f, 0.1f);
                SlowUpdate();
            }
        }

        private void SlowUpdate()
        {
            if (data != null && unique_id.WasCreated && selectable.IsActive())
            {
                WorldData pdata = WorldData.Get();
                DroppedItemData dropped_item = pdata.GetDroppedItem(GetUID());
                if (dropped_item != null)
                {
                    if (data.HasDurability() && dropped_item.durability <= 0f)
                        RemoveItem(); //Destroy item from durability
                }
            }
        }
        
        private void OnUse(PlayerCharacter character)
        {
            //Take
            character.Inventory.TakeItem(this);
        }

        public void TakeItem()
        {
            if (onTake != null)
                onTake.Invoke();

            RemoveItem();

            TheAudio.Get().PlaySFX3D("item", take_audio, transform.position);
            if (take_fx != null)
                Instantiate(take_fx, transform.position, Quaternion.identity);
        }

        //Destroy content but keep container
        public void SpoilItem()
        {
            if (data.container_data)
                Item.Create(data.container_data, transform.position, quantity);
            RemoveItem();
        }

        //Remove quantity
        public void EatItem()
        {
            quantity--;
            DroppedItemData invdata = WorldData.Get().GetDroppedItem(GetUID());
            if (invdata != null)
                invdata.quantity = quantity;

            if (quantity <= 0)
                RemoveItem();
        }

        public void RemoveItem()
        {
            WorldData pdata = WorldData.Get();
            if (unique_id.WasCreated)
                pdata.RemoveDroppedItem(GetUID()); //Removed from dropped items
            else
                pdata.RemoveObject(GetUID()); //Taken from map
            
            item_list.Remove(this);

            if (onDestroy != null)
                onDestroy.Invoke();

            NetObject.Destroy();
        }

        private bool DetectGrounded(out float dist)
        {
            float radius = 20f;
            float radius_up = 5f;
            float offset = 0.5f;
            Vector3 center = transform.position + Vector3.up * offset;
            Vector3 centerup = transform.position + Vector3.up * radius_up;

            RaycastHit hd1, hu1, hf1;
            LayerMask everything = ~0;
            bool f1 = Physics.Raycast(center, Vector3.down, out hf1, offset + 0.1f, everything.value, QueryTriggerInteraction.Ignore);
            bool d1 = Physics.Raycast(center, Vector3.down, out hd1, radius + offset, everything.value, QueryTriggerInteraction.Ignore);
            bool u1 = Physics.Raycast(centerup, Vector3.down, out hu1, radius_up + 0.1f, everything.value, QueryTriggerInteraction.Ignore);
            dist = d1 ? hd1.distance - offset : (u1 ? hu1.distance - radius_up : 0f);
            return f1;
        }

        public bool HasUID()
        {
            return !string.IsNullOrEmpty(unique_id.unique_id);
        }

        public string GetUID()
        {
            return unique_id.unique_id;
        }

        public bool HasGroup(GroupData group)
        {
            return data.HasGroup(group) || selectable.HasGroup(group);
        }

        public Selectable GetSelectable()
        {
            return selectable;
        }

        public DroppedItemData SaveData
        {
            get { return WorldData.Get().GetDroppedItem(GetUID()); }  //Can be null if not dropped or spawned
        }

        public static Item GetNearest(Vector3 pos, float range = 999f)
        {
            Item nearest = null;
            float min_dist = range;
            foreach (Item item in item_list)
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

        public static int CountInRange(Vector3 pos, float range)
        {
            int count = 0;
            foreach (Item item in GetAll())
            {
                float dist = (item.transform.position - pos).magnitude;
                if (dist < range)
                    count++;
            }
            return count;
        }

        public static new int CountInRange(IdData data, Vector3 pos, float range)
        {
            int count = 0;
            foreach (Item item in GetAll())
            {
                if (item.data == data)
                {
                    float dist = (item.transform.position - pos).magnitude;
                    if (dist < range)
                        count++;
                }
            }
            return count;
        }

        public static new int CountInRegion(IdData data, Zone zone)
        {
            int count = 0;
            foreach (Item item in GetAll())
            {
                if (item.data == data && zone.IsInside(item.transform.position))
                {
                    count++;
                }
            }
            return count;
        }

        public static Item GetByUID(string uid)
        {
            if (!string.IsNullOrEmpty(uid))
            {
                foreach (Item item in item_list)
                {
                    if (item.GetUID() == uid)
                        return item;
                }
            }
            return null;
        }

        public static new List<SObject> GetAllOf(IdData data)
        {
            List<SObject> valid_list = new List<SObject>();
            foreach (Item item in item_list)
            {
                if (item.data == data)
                    valid_list.Add(item);
            }
            return valid_list;
        }

        public static List<Item> GetAll()
        {
            return item_list;
        }

        //Spawn an existing one in the save file (such as after loading)
        public static Item Spawn(string uid, Transform parent = null)
        {
            DroppedItemData ddata = WorldData.Get().GetDroppedItem(uid);
            if (ddata != null && ddata.scene == SceneNav.GetCurrentScene())
            {
                ItemData idata = ItemData.Get(ddata.item_id);
                if (idata != null)
                {
                    GameObject build = Instantiate(idata.item_prefab, ddata.pos, idata.item_prefab.transform.rotation);
                    build.transform.parent = parent;

                    Item item = build.GetComponent<Item>();
                    item.data = idata;
                    item.unique_id.was_created = true;
                    item.unique_id.unique_id = uid;
                    item.quantity = ddata.quantity;
                    item.NetObject.Spawn();
                    return item;
                }
            }
            return null;
        }

        //Create a totally new one that will be added to save file
        public static Item Create(ItemData data, Vector3 pos, int quantity)
        {
            if (TheNetwork.Get().IsServer)
            {
                DroppedItemData ditem = WorldData.Get().AddDroppedItem(data.id, SceneNav.GetCurrentScene(), pos, quantity, data.durability);
                GameObject obj = Instantiate(data.item_prefab, pos, data.item_prefab.transform.rotation);
                Item item = obj.GetComponent<Item>();
                item.data = data;
                item.unique_id.was_created = true;
                item.unique_id.unique_id = ditem.uid;
                item.quantity = quantity;
                item.NetObject.Spawn();
                return item;
            }
            return null;
        }

        //Create a new item that existed in inventory (such as when dropping it)
        public static Item Create(ItemData data, Vector3 pos, int quantity, float durability, string uid)
        {
            if (TheNetwork.Get().IsServer)
            {
                DroppedItemData ditem = WorldData.Get().AddDroppedItem(data.id, SceneNav.GetCurrentScene(), pos, quantity, durability, uid);
                GameObject obj = Instantiate(data.item_prefab, pos, data.item_prefab.transform.rotation);
                Item item = obj.GetComponent<Item>();
                item.data = data;
                item.unique_id.was_created = true;
                item.unique_id.unique_id = ditem.uid;
                item.quantity = quantity;
                item.NetObject.Spawn();
                return item;
            }
            return null;
        }
    }

}