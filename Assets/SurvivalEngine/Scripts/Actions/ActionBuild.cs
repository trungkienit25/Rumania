using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalEngine
{
    /// <summary>
    /// Build an item into a construction (trap, lure)
    /// </summary>
    
    [CreateAssetMenu(fileName = "Action", menuName = "SurvivalEngine/Actions/Build", order = 50)]
    public class ActionBuild : SAction
    {
        public override void DoAction(PlayerCharacter character, InventorySlot slot)
        {
            ItemData item = slot.GetItem();
            if (item != null && item.build_data != null)
            {
                character.Crafting.BuildItemBuildMode(slot);
            }
        }

        public override bool CanDoAction(PlayerCharacter character, InventorySlot slot)
        {
            ItemData item = slot.GetItem();
            return item != null && item.build_data != null && character.IsSelf();
        }
    }

}
