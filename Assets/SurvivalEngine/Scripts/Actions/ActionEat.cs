using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalEngine
{
    /// <summary>
    /// Eat an item
    /// </summary>
    

    [CreateAssetMenu(fileName = "Action", menuName = "SurvivalEngine/Actions/Eat", order = 50)]
    public class ActionEat : SAction
    {

        public override void DoAction(PlayerCharacter character, InventorySlot slot)
        {
            InventoryData inventory = slot.inventory;
            character.Inventory.EatItem(inventory, slot.slot);
        }

        public override bool CanDoAction(PlayerCharacter character, InventorySlot slot)
        {
            ItemData item = slot.GetItem();
            return item != null && item.type == ItemType.Consumable;
        }
    }

}