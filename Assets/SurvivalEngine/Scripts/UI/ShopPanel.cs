using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SurvivalEngine
{
    /// <summary>
    /// UI panel for buy/sell items (npc shop)
    /// </summary>

    public class ShopPanel : UISlotPanel
    {
        public Text shop_title;
        public Text gold_value;
        public ShopSlot[] buy_slots;
        public ShopSlot[] sell_slots;
        public AudioClip buy_sell_audio;

        [Header("Description")]
        public Text title;
        public Text desc;
        public Text buy_cost;
        public Button button;
        public Text button_text;
        public GameObject desc_group;

        private ShopNPC shop;
        private PlayerCharacter current_player;
        private ShopSlot selected = null;

        private static ShopPanel instance;

        protected override void Awake()
        {
            base.Awake();
            instance = this;

            for (int i = 0; i < slots.Length; i++)
                ((ShopSlot)slots[i]).Hide();

            onClickSlot += OnClickSlot;
            onRightClickSlot += OnRightClickSlot;
            onPressAccept += OnAccept;
            onPressCancel += OnCancel;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

        }

        protected override void RefreshPanel()
        {
            base.RefreshPanel();

            gold_value.text = "0";

            foreach (ShopSlot slot in buy_slots)
                slot.Hide();

            foreach (ShopSlot slot in sell_slots)
                slot.Hide();

            if (current_player != null)
            {
                gold_value.text = current_player.SaveData.gold.ToString();

                //Buy items
                int index = 0;
                foreach (ItemData item in shop.items)
                {
                    if (index < buy_slots.Length)
                    {
                        ShopSlot slot = buy_slots[index];
                        slot.SetBuySlot(item, item.buy_cost);
                        slot.SetSelected(selected == slot);
                    }
                    index++;
                }

                //Sell items
                index = 0;
                foreach (KeyValuePair<int, InventoryItemData> pair in current_player.InventoryData.items)
                {
                    if (index < sell_slots.Length)
                    {
                        InventoryItemData item = pair.Value;
                        ItemData idata = ItemData.Get(item?.item_id);
                        bool can_sell = shop.CanSell(idata);
                        ShopSlot slot = sell_slots[index];
                        slot.SetSellSlot(idata, idata.sell_cost, item.quantity, can_sell);
                        slot.SetSelected(selected == slot);
                    }
                    index++;
                }

                //Description
                ItemData select_item = selected?.GetItem();
                desc_group.SetActive(select_item != null);
                if (select_item != null)
                {
                    title.text = select_item.title;
                    desc.text = select_item.desc;
                    bool sell = selected.IsSell();
                    int cost = (sell ? select_item.sell_cost : select_item.buy_cost);
                    buy_cost.text = cost.ToString();
                    button_text.text = sell ? "SELL" : "BUY";
                    button.interactable = (sell && cost > 0 && shop.CanSell(select_item)) || (!sell && cost <= current_player.SaveData.gold); 
                }

                //Gamepad auto controls
                PlayerControls controls = PlayerControls.Get();
                UISlotPanel focus_panel = UISlotPanel.GetFocusedPanel();
                if (focus_panel != this && controls.IsGamePad())
                {
                    Focus();
                }
            }
        }

        public void ShowShop(PlayerCharacter player, ShopNPC shop)
        {
            this.shop = shop;
            current_player = player;
            shop_title.text = shop.title;
            selected = null;
            RefreshPanel();
            Show();
        }

        public void AfterTrade()
        {
            if (IsVisible())
            {
                TheAudio.Get().PlaySFX("shop", buy_sell_audio);
                RefreshPanel();
            }
        }
        
        public override void Hide(bool instant = false)
        {
            base.Hide(instant);
            current_player = null;
        }

        private void OnClickSlot(UISlot islot)
        {
            ShopSlot slot = (ShopSlot)islot;
            ItemData item = slot.GetItem();

            if (slot != null && item != null && selected != slot)
                selected = slot;
            else
                selected = null;
           
            RefreshPanel();
        }

        private void OnAccept(UISlot islot)
        {
            if (selected == islot)
                OnClickBuy();
            else
                OnClickSlot(islot);
        }

        private void OnCancel(UISlot islot)
        {
            if (selected != null)
                selected = null;
            else
                Hide();
        }

        public void OnClickBuy()
        {
            ShopSlot slot = selected;
            bool sell = slot.IsSell();
            ItemData item = slot.GetItem();

            if (sell)
            {
                shop.SellItem(current_player, item);
            }
            else
            {
                shop.BuyItem(current_player, item);
            }
            RefreshPanel();
        }

        private void OnRightClickSlot(UISlot islot)
        {
            
        }

        public PlayerCharacter GetPlayer()
        {
            return current_player;
        }

        public static ShopPanel Get()
        {
            return instance;
        }

        public static bool IsAnyVisible()
        {
            if (instance)
                return instance.IsVisible();
            return false;
        }
    }

}