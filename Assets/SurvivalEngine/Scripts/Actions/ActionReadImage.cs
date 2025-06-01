using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalEngine
{
    /// <summary>
    /// Read a note on an item
    /// </summary>
    

    [CreateAssetMenu(fileName = "Action", menuName = "SurvivalEngine/Actions/ReadImage", order = 50)]
    public class ActionReadImage : SAction
    {
        public Sprite image;

        public override void DoAction(PlayerCharacter character, InventorySlot slot)
        {
            ItemData item = slot.GetItem();
            if (item != null && character.IsSelf())
            {
                ReadPanel.Get(1).ShowPanel(item.title, image);
            }
        }

        public override bool CanDoAction(PlayerCharacter character, InventorySlot slot)
        {
            return true;
        }
    }

}