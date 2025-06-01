using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalEngine
{
    /// <summary>
    /// Hoe the ground, to allow planting plants
    /// </summary>

    [CreateAssetMenu(fileName = "Action", menuName = "SurvivalEngine/Actions/Hoe", order = 50)]
    public class ActionHoe : SAction
    {
        public float hoe_range = 1f;

        public override void DoAction(PlayerCharacter character, InventorySlot slot)
        {
            Vector3 pos = character.transform.position + character.GetFacing() * hoe_range;

            PlayerCharacterHoe hoe = character.GetComponent<PlayerCharacterHoe>();
            hoe?.HoeGround(pos);

            InventoryItemData ivdata = character.EquipData.GetInventoryItem(slot.slot);
            if (ivdata != null)
                ivdata.durability -= 1;
        }

        public override bool CanDoAction(PlayerCharacter character, InventorySlot slot)
        {
            return slot.inventory != null && slot.inventory.type == InventoryType.Equipment;
        }
    }

}