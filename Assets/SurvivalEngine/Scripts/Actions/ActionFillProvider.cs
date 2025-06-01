using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalEngine
{
    /// <summary>
    /// Fill a jug with item from item provider
    /// </summary>
    
    [CreateAssetMenu(fileName = "Action", menuName = "SurvivalEngine/Actions/FillProvider", order = 50)]
    public class ActionFillProvider : MAction
    {
        public ItemData filled_item;

        public override void DoAction(PlayerCharacter character, InventorySlot slot, Selectable select)
        {
            if (select.HasGroup(merge_target))
            {
                ItemProvider provider = select.GetComponent<ItemProvider>();
                InventoryData inventory = slot.inventory;

                provider.RemoveItem();
                provider.PlayTakeSound();
                inventory.RemoveItemAt(slot.slot, 1);
                character.Inventory.GainItem(inventory, filled_item, 1);
            }
        }

        public override bool CanDoAction(PlayerCharacter character, InventorySlot slot, Selectable select)
        {
            ItemProvider provider = select != null ? select.GetComponent<ItemProvider>() : null;
            return provider != null && provider.HasItem();
        }
    }

}