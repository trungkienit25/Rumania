using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalEngine
{
    /// <summary>
    /// Action to attack a destructible (if the destructible cant be attack automatically)
    /// </summary>

    [CreateAssetMenu(fileName = "Action", menuName = "SurvivalEngine/Actions/Attack", order = 50)]
    public class ActionAttack : AAction
    {
        public override void DoAction(PlayerCharacter character, Selectable select)
        {
            AttackTarget target = select.GetComponent<AttackTarget>();
            if (target != null)
            {
                character.Attack(target);
            }
        }

        public override bool CanDoAction(PlayerCharacter character, Selectable select)
        {
            AttackTarget target = select.GetComponent<AttackTarget>();
            return target != null && target.CanBeAttacked(character);
        }

    }

}