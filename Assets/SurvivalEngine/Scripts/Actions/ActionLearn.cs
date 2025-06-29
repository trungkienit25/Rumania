﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalEngine
{
    /// <summary>
    /// Learn a crafting recipe
    /// </summary>
    

    [CreateAssetMenu(fileName = "Action", menuName = "SurvivalEngine/Actions/Learn", order = 50)]
    public class ActionLearn : SAction
    {
        public AudioClip learn_audio;
        public bool destroy_on_learn = true;
        public CraftData[] learn_list;

        public override void DoAction(PlayerCharacter character, InventorySlot slot)
        {
            foreach (CraftData data in learn_list)
            {
                character.Crafting.LearnCraft(data.id);
            }

            InventoryData inventory = slot.inventory;
            if (destroy_on_learn)
                inventory.RemoveItemAt(slot.slot, 1);

            if (character.IsSelf())
            {
                TheAudio.Get().PlaySFX("learn", learn_audio);
                CraftSubPanel.Get()?.RefreshCraftPanel();
            }
        }

        public override bool CanDoAction(PlayerCharacter character, InventorySlot slot)
        {
            foreach (CraftData data in learn_list)
            {
                if (!character.Crafting.HasLearnt(data.id))
                    return true;
            }
            return false;
        }
    }

}