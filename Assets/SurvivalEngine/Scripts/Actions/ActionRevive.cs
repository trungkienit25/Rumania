using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalEngine
{
    /// <summary>
    /// Sleeeep!
    /// </summary>

    [CreateAssetMenu(fileName = "Action", menuName = "SurvivalEngine/Actions/Revive", order = 50)]
    public class ActionRevive : SAction
    {
        public float revive_percent = 0.5f;

        public override void DoAction(PlayerCharacter character, Selectable select)
        {
            PlayerSelect pselect = select.GetComponent<PlayerSelect>();
            character.TriggerAnim("Build", select.transform.position);
            character.TriggerBusy(1f, () =>
            {
                if(pselect != null)
                    pselect.GetPlayer().Revive(pselect.transform.position, revive_percent);
            });

        }

        public override bool CanDoAction(PlayerCharacter character, Selectable select)
        {
            PlayerSelect other = select?.GetComponent<PlayerSelect>();
            return other != null && other.GetPlayer().IsDead();
        }
    }

}
