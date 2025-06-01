using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using NetcodePlus;

namespace SurvivalEngine {

    [RequireComponent(typeof(Selectable))]
    [RequireComponent(typeof(UniqueID))]
    public class MixingPot : SNetworkBehaviour
    {
        public ItemData[] recipes;
        public int max_items = 6;

        private UniqueID unique_id;
        private Selectable select;
        private SNetworkActions actions;

        private PlayerCharacter use_player;

        protected override void Awake()
        {
            base.Awake();
            unique_id = GetComponent<UniqueID>();
            select = GetComponent<Selectable>();
            select.onUse += OnUse;
        }

        protected override void OnReady()
        {
            //Create inventories
            InventoryData idata = InventoryData.Get(InventoryType.Storage, GetInventoryUID());
            InventoryData rdata = InventoryData.Get(InventoryType.Storage, GetResultUID());
            idata.size = max_items;
            rdata.size = 1;
        }

        protected override void OnSpawn()
        {
            actions = new SNetworkActions(this);
            actions.RegisterBehaviour("use", DoUse);
            actions.Register("mix", DoMix);
            actions.Register("gain", DoGain);
        }

        protected override void OnDespawn()
        {
            actions.Clear();
        }

        private void OnUse(PlayerCharacter player)
        {
            if (!string.IsNullOrEmpty(select.GetUID()))
            {
                actions?.Trigger("use", player);
            }
            else
            {
                Debug.LogError("You must generate the UID to use the mixing pot feature.");
            }
        }

        private void DoUse(SNetworkBehaviour beha)
        {
            PlayerCharacter player = beha.Get<PlayerCharacter>();
            use_player = player;

            if (IsClient && player != null)
            {
                MixingPanel.Get().ShowMixing(player, this, select.GetUID());
            }
        }

        public void Mix()
        {
            actions?.Trigger("mix");
        }

        private void DoMix()
        {
            if (use_player == null)
                return;

            foreach (ItemData recipe in recipes)
            {
                if (CanCraft(recipe))
                {
                    CraftRecipe(recipe);
                    return;
                }
            }

            if (IsClient)
            {
                MixingPanel.Get().Refresh();
            }
        }

        public void GainResult()
        {
            actions?.Trigger("gain");
        }

        private void DoGain()
        {
            if (use_player == null)
                return;

            InventoryData rdata = InventoryData.Get(InventoryType.Storage, GetResultUID());
            InventoryItemData item = rdata.GetInventoryItem(0);
            ItemData idata = rdata.GetItem(0);
            if (item != null && idata != null)
            {
                rdata.RemoveItemAt(0, item.quantity);
                use_player.Inventory.GainItem(idata, item.quantity);
            }

            if (IsClient)
            {
                MixingPanel.Get().Refresh();
            }
        }

        public void CraftRecipe(ItemData recipe)
        {
            InventoryData rdata = InventoryData.Get(InventoryType.Storage, GetResultUID());
            int tries = 0;
            while (tries < 100 && CanCraft(recipe))
            {
                PayCraftingCost(recipe);
                rdata.AddItem(recipe.id, recipe.craft_quantity, recipe.durability, NetworkTool.GenerateRandomID());
                tries++;
            }
        }

        public bool CanMix()
        {
            InventoryData rdata = InventoryData.Get(InventoryType.Storage, GetResultUID());
            ItemData result = rdata.GetItem(0);
            return result == null;
        }

        public bool CanCraft(CraftData item)
        {
            if (item == null)
                return false;

            CraftCostData cost = item.GetCraftCost();
            bool can_craft = true;

            Dictionary<GroupData, int> item_groups = new Dictionary<GroupData, int>(); //Add to groups so that fillers are not same than items

            InventoryData idata = InventoryData.Get(InventoryType.Storage, GetInventoryUID());
            foreach (KeyValuePair<ItemData, int> pair in cost.craft_items)
            {
                AddCraftCostItemsGroups(item_groups, pair.Key, pair.Value);
                if (!idata.HasItem(pair.Key.id, pair.Value))
                    can_craft = false; //Dont have required items
            }

            foreach (KeyValuePair<GroupData, int> pair in cost.craft_fillers)
            {
                int value = pair.Value + CountCraftCostGroup(item_groups, pair.Key);
                if (!idata.HasItemInGroup(pair.Key, value))
                    can_craft = false; //Dont have required items
            }

            return can_craft;
        }

        private void AddCraftCostItemsGroups(Dictionary<GroupData, int> item_groups, ItemData item, int quantity)
        {
            foreach (GroupData group in item.groups)
            {
                if (item_groups.ContainsKey(group))
                    item_groups[group] += quantity;
                else
                    item_groups[group] = quantity;
            }
        }

        private int CountCraftCostGroup(Dictionary<GroupData, int> item_groups, GroupData group)
        {
            if (item_groups.ContainsKey(group))
                return item_groups[group];
            return 0;
        }

        public void PayCraftingCost(CraftData item)
        {
            InventoryData idata = InventoryData.Get(InventoryType.Storage, GetInventoryUID());
            CraftCostData cost = item.GetCraftCost();
            foreach (KeyValuePair<ItemData, int> pair in cost.craft_items)
            {
                idata.RemoveItem(pair.Key.id, pair.Value);
            }
            foreach (KeyValuePair<GroupData, int> pair in cost.craft_fillers)
            {
                PayItemInGroup(idata, pair.Key, pair.Value);
            }
        }

        private void PayItemInGroup(InventoryData inventory, GroupData group, int quantity = 1)
        {
            if (group != null)
            {
                //Find which items should be used (by group)
                Dictionary<ItemData, int> remove_list = new Dictionary<ItemData, int>(); //Item, Quantity
                foreach (KeyValuePair<int, InventoryItemData> pair in inventory.items)
                {
                    ItemData idata = ItemData.Get(pair.Value?.item_id);
                    if (idata != null && idata.HasGroup(group) && pair.Value.quantity > 0 && quantity > 0)
                    {
                        int remove = Mathf.Min(quantity, pair.Value.quantity);
                        remove_list.Add(idata, remove);
                        quantity -= remove;
                    }
                }

                //Use those specific items
                InventoryData invdata = InventoryData.Get(InventoryType.Storage, GetInventoryUID());
                foreach (KeyValuePair<ItemData, int> pair in remove_list)
                {
                    invdata.RemoveItem(pair.Key.id, pair.Value);
                }
            }
        }

        public void RemoveAll()
        {
            InventoryData idata = InventoryData.Get(InventoryType.Storage, GetInventoryUID());
            for (int i = 0; i < idata.size; i++)
                idata.RemoveItemAt(i, 999);
        }

        public bool HasResult()
        {
            InventoryData rdata = InventoryData.Get(InventoryType.Storage, GetResultUID());
            ItemData item = rdata.GetItem(0);
            return item != null;
        }

        public string GetInventoryUID()
        {
            return unique_id.unique_id;
        }

        public string GetResultUID()
        {
            return "r_" + unique_id.unique_id;
        }

        public Selectable GetSelectable()
        {
            return select;
        }
    }

    public class MixItem : INetworkSerializable
    {
        public string item_id;
        public int quantity;

        public MixItem() { }
        public MixItem(ItemData item, int q) { item_id = item.id; quantity = q; }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref item_id);
            serializer.SerializeValue(ref quantity);
        }
    }
}
