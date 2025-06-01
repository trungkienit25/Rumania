using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalEngine
{
    /// <summary>
    /// Add fuel to a fire (wood, grass, etc)
    /// </summary>

    [CreateAssetMenu(fileName = "Action", menuName = "SurvivalEngine/Actions/AddFuel", order = 50)]
    public class ActionAddFuel : MAction
    {
        public float range = 2f;

        //Merge action
        public override void DoAction(PlayerCharacter character, InventorySlot slot, Selectable select)
        {
            Firepit fire = select.GetComponent<Firepit>();
            InventoryData inventory = slot.inventory;
            if (fire != null && slot.GetItem() != null && inventory.HasItem(slot.GetItem().id))
            {
                fire.AddFuel(fire.wood_add_fuel);
                inventory.RemoveItemAt(slot.slot, 1);
            }

        }

    }

}