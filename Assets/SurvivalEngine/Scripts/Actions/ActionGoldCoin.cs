using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalEngine
{
    /// <summary>
    /// Learn a crafting recipe
    /// </summary>
    

    [CreateAssetMenu(fileName = "Action", menuName = "SurvivalEngine/Actions/GoldCoin", order = 50)]
    public class ActionGoldCoin : AAction
    {
        public override void DoAction(PlayerCharacter character, InventorySlot slot)
        {
            InventoryData inventory = slot.inventory;
            InventoryItemData ivdata = slot.GetInventoryItem();
            int amount = ivdata.quantity;
            inventory.RemoveItemAt(slot.slot, amount);
            character.SaveData.gold += amount;
            //ItemTakeFX.DoCoinTakeFX(character.transform.position, slot.GetItem(), character.player_id);
        }

        public override void DoClickAction(PlayerCharacter character, InventorySlot slot)
        {
            InventoryData inventory = slot.inventory;
            InventoryItemData ivdata = slot.GetInventoryItem();
            int amount = ivdata.quantity;
            inventory.RemoveItemAt(slot.slot, amount);
            character.SaveData.gold += amount;
            //ItemTakeFX.DoCoinTakeFX(character.transform.position, slot.GetItem(), character.player_id);
        }

        public override bool CanDoAction(PlayerCharacter character, InventorySlot slot)
        {
            return true;
        }
    }

}