using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalEngine
{
    /// <summary>
    /// Automatic Action parent class: Any action performed automatically when the object is clicked on
    /// If you put more than 1, first AAction in the list that can be performed will be selected
    /// </summary>
    
    public abstract class AAction : SAction
    {
        //When clicking on a Selectable in the scene
        public override void DoAction(PlayerCharacter character, Selectable select)
        {

        }

        //When right-clicking (or pressing Use) a ItemData in inventory
        public override void DoAction(PlayerCharacter character, InventorySlot slot)
        {

        }

        //When left-clicking a ItemData in inventory
        public virtual void DoClickAction(PlayerCharacter character, InventorySlot slot)
        {

        }

        //Condition to check if the selectable action is possible
        public override bool CanDoAction(PlayerCharacter character, Selectable select)
        {
            return true; //No condition
        }

        //Condition to check if the item action is possible
        public override bool CanDoAction(PlayerCharacter character, InventorySlot slot)
        {
            return true; //No condition
        }
    }

}