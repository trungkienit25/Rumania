using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalEngine
{
    /// <summary>
    /// Add item to stack container
    /// </summary>

    [CreateAssetMenu(fileName = "Action", menuName = "SurvivalEngine/Actions/ItemStack", order = 50)]
    public class ActionItemStack : MAction
    {
        //Merge action
        public override void DoAction(PlayerCharacter character, InventorySlot slot, Selectable select)
        {
            InventoryData inventory = slot.inventory;
            InventoryItemData iidata = inventory.GetInventoryItem(slot.slot);
            inventory.RemoveItemAt(slot.slot, iidata.quantity);

            ItemStack stack = select.GetComponent<ItemStack>();
            stack.AddItem(iidata.quantity);
        }

        public override bool CanDoAction(PlayerCharacter character, InventorySlot slot, Selectable select)
        {
            ItemStack stack = select.GetComponent<ItemStack>();
            ItemData slot_item = slot?.GetItem();
            return stack != null && stack.item != null && slot_item != null && stack.item.id == slot_item.id && stack.GetItemCount() < stack.item_max;
        }
    }

}
