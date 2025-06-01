using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalEngine
{
    /// <summary>
    /// Change scene when using
    /// </summary>

    [CreateAssetMenu(fileName = "Action", menuName = "SurvivalEngine/Actions/ChangeScene", order = 50)]
    public class ActionScene : AAction
    {
        public string scene;
        public string entry;

        public override void DoAction(PlayerCharacter character, Selectable selectable)
        {
            if (string.IsNullOrEmpty(scene) || scene == SceneNav.GetCurrentScene())
            {
                ExitZone zone = ExitZone.Get(entry);
                if(zone != null && zone != this)
                    TheGame.Get().TeleportToZone(character, zone);
            }
            else
            {
                TheGame.Get().TransitionToScene(scene, entry);
            }
        }

        public override bool CanDoAction(PlayerCharacter character, Selectable selectable)
        {
            return true;
        }
    }

}