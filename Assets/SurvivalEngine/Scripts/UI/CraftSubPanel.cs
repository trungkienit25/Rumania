﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SurvivalEngine
{

    /// <summary>
    /// Second level crafting bar, that contains the items under a category
    /// </summary>

    public class CraftSubPanel : UISlotPanel
    {
        [Header("Craft Sub Panel")]
        public Text title;
        public Animator animator;

        private PlayerUI parent_ui;
        private UISlot prev_slot;

        private GroupData current_category;

        private static CraftSubPanel instance;

        protected override void Awake()
        {
            base.Awake();
            instance = this;
            parent_ui = GetComponentInParent<PlayerUI>();

            if (animator != null)
                animator.SetBool("Visible", IsVisible());
        }

        protected override void Start()
        {
            base.Start();

            onClickSlot += OnClick;
            onPressAccept += OnAccept;
            onPressCancel += OnCancel;
        }

        protected override void Update()
        {
            base.Update();

            
        }

        protected override void RefreshPanel()
        {
            base.RefreshPanel();

            //Gamepad auto controls
            PlayerCharacter player = GetPlayer();
            CraftInfoPanel info_panel = CraftInfoPanel.Get();
            if (UISlotPanel.GetFocusedPanel() == this)
            {
                selection_index = Mathf.Clamp(selection_index, 0, CountActiveSlots() - 1);

                UISlot slot = GetSelectSlot();
                if (player != null && !player.Crafting.IsBuildMode())
                {
                    if (prev_slot != slot || !info_panel.IsVisible())
                    {
                        OnClick(slot);
                        prev_slot = slot;
                    }
                }
            }
        }

        public void RefreshCraftPanel()
        {
            foreach (ItemSlot slot in slots)
                slot.Hide();

            if (current_category == null || !IsVisible())
                return;

            //Show all items of a category
            PlayerCharacter player = GetPlayer();
            if (player != null)
            {
                List<CraftData> items = CraftData.GetAllCraftableInGroup(GetPlayer(), current_category);

                //Sort list
                items.Sort((p1, p2) =>
                {
                    return (p1.craft_sort_order == p2.craft_sort_order)
                        ? p1.title.CompareTo(p2.title) : p1.craft_sort_order.CompareTo(p2.craft_sort_order);
                });

                for (int i = 0; i < items.Count; i++)
                {
                    if (i < slots.Length)
                    {
                        CraftData item = items[i];
                        ItemSlot slot = (ItemSlot)slots[i];
                        slot.SetSlot(item, 1, false);
                        slot.AnimateGain();
                    }
                }
            }
        }

        public void ShowCategory(GroupData group)
        {
            Hide(true); //Instant hide to do show animation

            current_category = group;
            title.text = group.title;
            
            Show();
        }

        public override void Show(bool instant = false)
        {
            base.Show(instant);

            ShowAnim(true);
            RefreshCraftPanel();
        }

        public override void Hide(bool instant = false)
        {
            base.Hide(instant);

            current_category = null;
            CraftInfoPanel.Get()?.Hide();
            ShowAnim(false);

            if(instant && animator != null)
                animator.Rebind();
        }

        private void ShowAnim(bool visible)
        {
            SetVisible(visible);
            if (animator != null)
                animator.SetBool("Visible", IsVisible());
        }

        private void OnClick(UISlot uislot)
        {
            int slot = uislot.index;
            ItemSlot islot = (ItemSlot)uislot;
            CraftData item = islot.GetCraftable();

            foreach (ItemSlot aslot in slots)
                aslot.UnselectSlot();

            CraftInfoPanel info_panel = CraftInfoPanel.Get();
            if (info_panel) {
                if (item == info_panel.GetData())
                {
                    info_panel.Hide();
                }
                else
                {
                    if(parent_ui != null)
                        parent_ui.CancelSelection();
                    slots[slot].SelectSlot();
                    info_panel.ShowData(item);
                }
            }
        }

        private void OnAccept(UISlot slot)
        {
            PlayerCharacter player = GetPlayer();
            CraftInfoPanel.Get()?.OnClickCraft();
            if (player != null && player.Crafting.IsBuildMode())
                UnfocusAll();
        }

        private void OnCancel(UISlot slot)
        {
            CancelSelection();
            CraftInfoPanel.Get()?.Hide();
            CraftPanel.Get()?.Focus();
        }

        public void CancelSelection()
        {
            for (int i = 0; i < slots.Length; i++)
                slots[i].UnselectSlot();
            CraftInfoPanel.Get()?.Hide();
        }

        public GroupData GetCurrentCategory()
        {
            return current_category;
        }

        public PlayerCharacter GetPlayer()
        {
            return parent_ui != null ? parent_ui.GetPlayer() : PlayerCharacter.GetSelf();
        }

        public static CraftSubPanel Get()
        {
            return instance;
        }
    }

}