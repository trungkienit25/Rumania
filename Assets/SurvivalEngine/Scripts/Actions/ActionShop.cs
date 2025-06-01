using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalEngine
{
    [CreateAssetMenu(fileName = "Action", menuName = "SurvivalEngine/Actions/Shop", order = 50)]
    public class ActionShop : AAction
    {
        public override void DoAction(PlayerCharacter character, Selectable select)
        {
            ShopNPC shop = select.GetComponent<ShopNPC>();
            if (shop != null && character.IsSelf())
                shop.OpenShop(character);
        }

        public override bool CanDoAction(PlayerCharacter character, Selectable select)
        {
            ShopNPC shop = select.GetComponent<ShopNPC>();
            return shop != null;
        }
    }
}
