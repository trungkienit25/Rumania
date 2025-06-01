using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using NetcodePlus;

namespace SurvivalEngine {

    /// <summary>
    /// In-Game UI, specific to one player
    /// </summary>

    public class PlayerUI : UIPanel
    {
        [Header("Player")]
        public int player_id = -1; //If set to -1, will use self (TheNetwork player_id)

        [Header("Gameplay UI")]
        public Image save_icon;
        public Image gold_img;
        public Text gold_value;
        public UIPanel damage_fx;
        public UIPanel cold_fx;
        public Text build_mode_text;
        public Image tps_cursor;
        public GameObject revive_button;
        public GameObject unmount_button;

        public UnityAction onCancelSelection;

        private PlayerCharacter player_character = null;
        private ItemSlotPanel[] item_slot_panels;
        private float damage_fx_timer = -5f;

        private static List<PlayerUI> ui_list = new List<PlayerUI>();

        protected override void Awake()
        {
            base.Awake();
            ui_list.Add(this);

            item_slot_panels = GetComponentsInChildren<ItemSlotPanel>();

            if(save_icon != null)
                save_icon.enabled = false;

            if (build_mode_text != null)
                build_mode_text.enabled = false;

            if (gold_img != null)
                gold_img.enabled = false;
            if (gold_value != null)
                gold_value.text = "";

            if (revive_button != null)
                revive_button.SetActive(false);
            if (unmount_button != null)
                unmount_button.SetActive(false);

            Show(true);
        }

        protected virtual void OnDestroy()
        {
            ui_list.Remove(this);
        }

        protected override void Start()
        {
            base.Start();

            PlayerControlsMouse mouse = PlayerControlsMouse.Get();
            mouse.onRightClick += (Vector3 pos, Selectable select) => { CancelSelection(); };
        }

        protected override void Update()
        {
            base.Update();

            PlayerCharacter character = GetPlayer();
            if (character == null)
                return;

            //Add event if player changed
            if(character != player_character)
                character.Combat.onDamaged += DoDamageFX;
            player_character = character;

            int gold = character.SaveData.gold;
            if (gold_img != null)
                gold_img.enabled = gold > 0;
            if (gold_value != null)
                gold_value.text = gold > 0 ? gold.ToString() : "";

            //Init inventories from here because they are disabled
            foreach (ItemSlotPanel panel in item_slot_panels)
                panel.InitPanel();

            //Fx visibility
            damage_fx_timer += Time.deltaTime;

            if(save_icon != null)
                save_icon.enabled = TheGame.Get().IsSaving();

            if (build_mode_text != null)
                build_mode_text.enabled = IsBuildMode();

            if (tps_cursor != null)
                tps_cursor.enabled = TheCamera.Get().IsFreelook();

            //Revive button
            bool dead = character.IsDead();
            if (revive_button != null && dead != revive_button.activeSelf)
                revive_button.SetActive(dead);

            //Unmount button
            if (unmount_button != null && !dead && character.IsRiding() != unmount_button.activeSelf)
                unmount_button.SetActive(character.IsRiding());

            //Damage FX
            bool has_health = character.SaveData.HasAttribute(AttributeType.Health);
            if (!character.IsDead() && has_health && character.Attributes.IsDepletingHP())
            {
                DoDamageFXInterval();
            }

            //Cold FX
            if (!character.IsDead() && has_health)
            {
                PlayerCharacterHeat characterHeat = PlayerCharacterHeat.Get(character.PlayerID);
                if (cold_fx != null && characterHeat != null)
                    cold_fx.SetVisible(characterHeat.IsCold());
                if (damage_fx != null && characterHeat != null && characterHeat.IsColdDamage())
                    DoDamageFXInterval();
            }

            //Controls
            PlayerControls controls = PlayerControls.Get();
            if (controls.IsPressCraft())
            {
                CraftPanel.Get()?.Toggle();
                ActionSelectorUI.Get()?.Hide();
                ActionSelector.Get()?.Hide();
            }

            //Backpack panel
            BagPanel bag_panel = BagPanel.Get(PlayerID);
            if (bag_panel != null)
            {
                InventoryItemData item = character.Inventory.GetBestEquippedBag();
                ItemData idata = ItemData.Get(item?.item_id);
                if (idata != null)
                    bag_panel.ShowBag(character, item.uid, idata.bag_size);
                else
                    bag_panel.HideBag();
            }
        }

        public void DoDamageFX()
        {
            if(damage_fx != null)
                StartCoroutine(DamageFXRun());
        }

        public void DoDamageFXInterval()
        {
            if (damage_fx != null && damage_fx_timer > 0f)
                StartCoroutine(DamageFXRun());
        }

        private IEnumerator DamageFXRun()
        {
            damage_fx_timer = -3f;
            damage_fx.Show();
            yield return new WaitForSeconds(1f);
            damage_fx.Hide();
        }

        public void CancelSelection()
        {
            ItemSlotPanel.CancelSelectionAll();
            CraftPanel.Get()?.CancelSelection();
            CraftSubPanel.Get()?.CancelSelection();
            ActionSelectorUI.Get()?.Hide();
            ActionSelector.Get()?.Hide();

            if (onCancelSelection != null)
                onCancelSelection.Invoke();
        }

        public void OnClickCraft()
        {
            CancelSelection();
            CraftPanel.Get()?.Toggle();
        }

        public void OnClickRevive()
        {
            revive_button.SetActive(false);
            StartCoroutine(ReviveRun());
        }

        public void OnClickUnmount()
        {
            PlayerCharacter character = GetPlayer();
            character.Riding.StopRide();
            unmount_button.SetActive(false);
        }

        private IEnumerator ReviveRun()
        {
            BlackPanel.Get().Show();
            yield return new WaitForSeconds(1f);

            PlayerCharacter character = GetPlayer();
            character.ReviveAtSpawn();

            yield return new WaitForSeconds(1f);
            BlackPanel.Get().Hide();
        }

        public ItemSlot GetSelectedSlot()
        {
            foreach (ItemSlotPanel panel in ItemSlotPanel.GetAll())
            {
                if (panel.GetPlayerID() == PlayerID)
                {
                    ItemSlot slot = panel.GetSelectedSlot();
                    if (slot != null)
                        return slot;
                }
            }
            return null;
        }

        public ItemSlot GetDragSlot()
        {
            foreach (ItemSlotPanel panel in ItemSlotPanel.GetAll())
            {
                if (panel.GetPlayerID() == PlayerID)
                {
                    UISlot slot = panel.GetDragSlot();
                    if (slot != null && slot is ItemSlot)
                        return (ItemSlot) slot;
                }
            }
            return null;
        }

        public int GetSelectedSlotIndex()
        {
            ItemSlot slot = ItemSlotPanel.GetSelectedSlotInAllPanels();
            return slot != null ? slot.index : -1;
        }

        public InventoryData GetSelectedSlotInventory()
        {
            ItemSlot slot = ItemSlotPanel.GetSelectedSlotInAllPanels();
            return slot != null ? slot.GetInventory() : null;
        }

        public bool IsBuildMode()
        {
            PlayerCharacter player = GetPlayer();
            if (player)
                return player.Crafting.IsBuildMode();
            return false;
        }

        public PlayerCharacter GetPlayer()
        {
            return PlayerCharacter.Get(PlayerID);
        }

        public int PlayerID { get { return player_id >= 0 ? player_id : TheNetwork.Get().PlayerID; } }

        public static void ShowUI()
        {
            foreach (PlayerUI ui in ui_list)
                ui.Show();
        }

        public static void HideUI()
        {
            foreach (PlayerUI ui in ui_list)
                ui.Hide();
        }

        public static bool IsUIVisible()
        {
            if (ui_list.Count > 0)
                return ui_list[0].IsVisible();
            return false;
        }

        public static PlayerUI Get(int player_id)
        {
            foreach (PlayerUI ui in ui_list)
            {
                if (ui.PlayerID == player_id)
                    return ui;
            }
            return null;
        }

        public static PlayerUI Get()
        {
            return Get(TheNetwork.Get().PlayerID);
        }

        public static PlayerUI GetSelf() => Get(); //Alternate name

        public static List<PlayerUI> GetAll()
        {
            return ui_list;
        }
    }

}
