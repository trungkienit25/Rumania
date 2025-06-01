using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SurvivalEngine
{
    /// <summary>
    /// Main UI panel for storages boxes (chest)
    /// </summary>

    public class MixingPanel : ItemSlotPanel
    {
        public ItemSlot result_slot;
        public Button mix_button;

        private PlayerCharacter player;
        private MixingPot mixing_pot;
        //private ItemData crafed_item = null;

        private static MixingPanel _instance;

        protected override void Awake()
        {
            base.Awake();
            _instance = this;

            result_slot.onClick += OnClickResult;
        }

        protected override void RefreshPanel()
        {
            base.RefreshPanel();

            if (!IsVisible())
                return;

            mix_button.interactable = mixing_pot != null && mixing_pot.CanMix();

            if (mixing_pot != null)
            {
                InventoryData rdata = InventoryData.Get(InventoryType.Storage, mixing_pot.GetResultUID());
                InventoryItemData result = rdata.GetInventoryItem(0);
                if(result != null)
                    result_slot.SetSlot(result.GetItem(), result.quantity);
                else
                    result_slot.SetSlot(null, 0);
            }

            //Hide if too far
            Selectable select = mixing_pot?.GetSelectable();
            if (player != null && select != null)
            {
                float dist = (select.transform.position - player.transform.position).magnitude;
                if (dist > select.GetUseRange(player) * 1.2f)
                {
                    Hide();
                }
            }
        }

        public void ShowMixing(PlayerCharacter player, MixingPot pot, string uid)
        {
            if (!string.IsNullOrEmpty(uid))
            {
                this.player = player;
                this.mixing_pot = pot;
                SetInventory(InventoryType.Storage, pot.GetInventoryUID(), -1, pot.max_items);
                SetPlayer(player);
                RefreshPanel();
                Show();
            }
        }

        public void Refresh()
        {
            RefreshPanel();
        }

        public override void Hide(bool instant = false)
        {
            base.Hide(instant);
            mixing_pot = null;
            player = null;
            SetInventory(InventoryType.Storage, "", -1, 0);
            CancelSelection();
        }

        public void OnClickMix()
        {
            if (mixing_pot != null && mixing_pot.CanMix())
            {
                mixing_pot.Mix();
            }
        }

        public void OnClickResult(UISlot slot)
        {
            if (player != null && mixing_pot != null && result_slot.GetItem() != null)
            {
                mixing_pot.GainResult();
                RefreshPanel();
            }
        }

        public string GetStorageUID()
        {
            return inventory_uid;
        }

        public static MixingPanel Get()
        {
            return _instance;
        }
    }

}