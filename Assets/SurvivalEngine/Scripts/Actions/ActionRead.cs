using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalEngine
{
    /// <summary>
    /// Read a note on an item
    /// </summary>
    

    [CreateAssetMenu(fileName = "Action", menuName = "SurvivalEngine/Actions/Read", order = 50)]
    public class ActionRead : SAction
    {

        public override void DoAction(PlayerCharacter character, InventorySlot slot)
        {
            ItemData item = slot.GetItem();
            if (item != null && character.IsSelf())
            {
                ReadPanel.Get().ShowPanel(item.title, item.desc);
            }

        }

        public override void DoAction(PlayerCharacter character, Selectable select)
        {
            ReadObject read = select.GetComponent<ReadObject>();
            if (read != null)
            {
                ReadPanel.Get().ShowPanel(read.title, read.text);
            }

        }

        public override bool CanDoAction(PlayerCharacter character, InventorySlot slot)
        {
            return true;
        }
    }

}