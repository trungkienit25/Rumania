using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalEngine
{
    /// <summary>
    /// Sow a seed in the ground.
    /// </summary>
    
    [CreateAssetMenu(fileName = "Action", menuName = "SurvivalEngine/Actions/Plant", order = 50)]
    public class ActionPlant : SAction
    {
        public override void DoAction(PlayerCharacter character, InventorySlot slot)
        {
            ItemData item = slot.GetItem();
            if (item != null && item.build_data is PlantData)
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