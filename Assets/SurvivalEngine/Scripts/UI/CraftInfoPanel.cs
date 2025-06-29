﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivalEngine
{

    /// <summary>
    /// Panel that contain the detailed information of a single crafting item
    /// </summary>

    public class CraftInfoPanel : UIPanel
    {
        public ItemSlot slot;
        public Text title;
        public Text desc;
        public Button craft_btn;

        public ItemSlot[] craft_slots;

        private CraftData data;

        private float update_timer = 0f;

        private static CraftInfoPanel instance;

        protected override void Awake()
        {
            base.Awake();
            instance = this;
        }

        protected override void Update()
        {
            base.Update();

            update_timer += Time.deltaTime;
            if (update_timer > 0.5f)
            {
                update_timer = 0f;
                SlowUpdate();
            }
        }

        private void SlowUpdate()
        {
            if (data != null && IsVisible())
            {
                RefreshPanel();
            }
        }

        private void RefreshPanel()
        {
            slot.SetSlot(data, data.craft_quantity, true);
            title.text = data.title;
            desc.text = data.desc;

            foreach (ItemSlot slot in craft_slots)
                slot.Hide();

            PlayerCharacter player = PlayerCharacter.GetSelf();

            CraftCostData cost = data.GetCraftCost();
            int index = 0;
            foreach (KeyValuePair<ItemData, int> pair in cost.craft_items)
            {
                if (index < craft_slots.Length)
                {
                    ItemSlot slot = craft_slots[index];
                    slot.SetSlot(pair.Key, pair.Value, false);
                    slot.SetFilter(player.Inventory.HasItem(pair.Key, pair.Value) ? 0 : 2);
                    slot.ShowTitle();
                }
                index++;
            }

            foreach (KeyValuePair<GroupData, int> pair in cost.craft_fillers)
            {
                if (index < craft_slots.Length)
                {
                    ItemSlot slot = craft_slots[index];
                    slot.SetSlotCustom(pair.Key.icon, pair.Key.title, pair.Value, false);
                    slot.SetFilter(player.Inventory.HasItemInGroup(pair.Key, pair.Value) ? 0 : 2);
                    slot.ShowTitle();
                }
                index++;
            }

            foreach (KeyValuePair<CraftData, int> pair in cost.craft_requirements)
            {
                if (index < craft_slots.Length)
                {
                    ItemSlot slot = craft_slots[index];
                    slot.SetSlot(pair.Key, pair.Value, false);
                    slot.SetFilter(player.Crafting.CountRequirements(pair.Key) >= pair.Value ? 0 : 2);
                    slot.ShowTitle();
                }
                index++;
            }

            if (index < craft_slots.Length)
            {
                ItemSlot slot = craft_slots[index];
                if (cost.craft_near != null)
                {
                    slot.SetSlotCustom(cost.craft_near.icon, cost.craft_near.title, 1, false);
                    bool isnear = player.IsNearGroup(cost.craft_near) || player.EquipData.HasItemInGroup(cost.craft_near);
                    slot.SetFilter(isnear ? 0 : 2);
                    slot.ShowTitle();
                }
            }

            craft_btn.interactable = player.Crafting.CanCraft(data);
        }

        public void ShowData(CraftData item)
        {
            this.data = item;
            RefreshPanel();
            slot.AnimateGain();
            Show();
        }

        public void OnClickCraft()
        {
            PlayerCharacter player = PlayerCharacter.GetSelf();

            if (player.Crafting.CanCraft(data))
            {
                player.Crafting.StartCraftingOrBuilding(data);

                craft_btn.interactable = false;
                Hide();
            }
        }

        public override void Hide(bool instant = false)
        {
            base.Hide(instant);
            data = null;
        }

        public CraftData GetData()
        {
            return data;
        }

        public static CraftInfoPanel Get()
        {
            return instance;
        }
    }

}