using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalEngine
{
    /// <summary>
    /// Use to equip/unequip equipment items
    /// </summary>
    
    [CreateAssetMenu(fileName = "Action", menuName = "SurvivalEngine/Actions/Equip", order = 50)]
    public class ActionEquip : SAction
    {

        public override void DoAction(PlayerCharacter character, InventorySlot slot)
        {
            ItemData item = slot.GetItem();
            InventoryData inventory = slot.inventory;

            if (item != null && item.type == ItemType.Equipment)
            {
                if (inventory.type == InventoryType.Equipment)
                {
                    EquipSlot eslot = (EquipSlot)slot.slot;
                    character.Inventory.UnequipItem(eslot);
                }
                else
                {
                    character.Inventory.EquipItem(inventory, slot.slot);
                }
            }
        }

        public override bool CanDoAction(PlayerCharacter character, InventorySlot slot)
        {
            ItemData item = slot.GetItem();
            return item != null && item.type == ItemType.Equipment;
        }
    }

}