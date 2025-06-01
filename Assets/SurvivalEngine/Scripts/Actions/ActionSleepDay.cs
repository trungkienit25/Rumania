using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalEngine
{
    /// <summary>
    /// Sleeeep! Until the next day
    /// </summary>

    [CreateAssetMenu(fileName = "Action", menuName = "SurvivalEngine/Actions/SleepDay", order = 50)]
    public class ActionSleepDay : SAction
    {

        public override void DoAction(PlayerCharacter character, Selectable select)
        {
            foreach (PlayerCharacter acharacter in PlayerCharacter.GetAll())
            {
                acharacter.Attributes.ResetAttribute(AttributeType.Health);
                acharacter.Attributes.ResetAttribute(AttributeType.Energy);
            }
            TheGame.Get().TransitionToNextDay();
        }
    }

}
