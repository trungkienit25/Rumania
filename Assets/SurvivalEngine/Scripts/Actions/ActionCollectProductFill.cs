using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalEngine
{
    /// <summary>
    /// Collect an animal product, using a container (Like milk for cow)
    /// </summary>

    [CreateAssetMenu(fileName = "Action", menuName = "SurvivalEngine/Actions/CollectProductFill", order = 50)]
    public class ActionCollectProductFill : MAction
    {
        //Merge action
        public override void DoAction(PlayerCharacter character, InventorySlot slot, Selectable select)
        {
            AnimalLivestock animal = select.GetComponent<AnimalLivestock>();
            if (select.HasGroup(merge_target) && animal != null)
            {
                character.TriggerAnim("Take", animal.transform.position);
                character.TriggerBusy(0.5f, () =>
                {
                    InventoryData inventory = slot.inventory;
                    inventory.RemoveItemAt(slot.slot, 1);
                    if(animal != null)
                        animal.CollectProduct(character);
                });
            }
        }

        public override bool CanDoAction(PlayerCharacter character, InventorySlot slot, Selectable select)
        {
            AnimalLivestock animal = select.GetComponent<AnimalLivestock>();
            return select.HasGroup(merge_target) && animal != null && animal.HasProduct();
        }
    }

}