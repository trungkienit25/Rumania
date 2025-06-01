using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using NetcodePlus;

namespace SurvivalEngine
{

    /// <summary>
    /// Add to your player character for HOE feature
    /// </summary>

    [RequireComponent(typeof(PlayerCharacter))]
    public class PlayerCharacterHoe : SNetworkBehaviour
    {
        public GroupData hoe_item;
        public ConstructionData hoe_soil;
        public float hoe_range = 1f;
        public float hoe_build_radius = 0.5f;
        public int hoe_energy = 0;

        private PlayerCharacter character;
        private SNetworkActions actions;

        protected override void Awake()
        {
            base.Awake();
            character = GetComponent<PlayerCharacter>();
        }

        private void OnDestroy()
        {
            
        }

        protected override void OnSpawn()
        {
            actions = new SNetworkActions(this);
            actions.RegisterVector(ActionType.Build, DoHoe);
        }

        protected override void OnDespawn()
        {
            actions.Clear();
        }

        void FixedUpdate()
        {
            
        }

        private void Update()
        {
            //Auto hoe
            if (character.IsAutoMove())
            {
                HoeGroundAuto(character.GetAutoMoveTarget());
            }

            PlayerControls control = PlayerControls.Get();
            if (control.IsPressAttack() && character.IsControlsEnabled())
            {
                Vector3 hoe_pos = character.GetInteractCenter() + character.GetFacing() * 1f;
                HoeGround(hoe_pos);
            }
        }

        public void HoeGround(Vector3 pos)
        {
            if (!CanHoe())
                return;

            actions.Trigger(ActionType.Build, pos);
        }

        private void DoHoe(Vector3 pos)
        {
            if (!CanHoe())
                return;

            character.StopMove();
            character.Attributes.AddAttribute(AttributeType.Energy, -hoe_energy);

            character.TriggerAnim(character.Animation ? character.Animation.hoe_anim : "", pos);
            character.TriggerBusy(0.8f, () =>
            {
                InventoryItemData ivdata = character.EquipData.GetEquippedItem(EquipSlot.Hand);
                if (ivdata != null)
                    ivdata.durability -= 1;

                if (!IsServer)
                    return; //Only server destroys/spawn soil

                Construction prev = Construction.GetNearest(pos, hoe_build_radius);
                Plant plant = Plant.GetNearest(pos, hoe_build_radius);
                if (prev != null && plant == null && prev.data == hoe_soil)
                {
                    prev.Kill(); //Destroy previous, if no plant on it
                    return;
                }

                Construction construct = Construction.CreateBuildMode(hoe_soil, pos);
                construct.GetBuildable().StartBuild(character);
                construct.GetBuildable().SetBuildPositionTemporary(pos);
                if (construct.GetBuildable().CheckIfCanBuild())
                {
                    construct.GetBuildable().FinishBuild();
                }
                else
                {
                    Destroy(construct.gameObject);
                }
            });
        }

        public bool CanHoe()
        {
            bool has_energy = character.Attributes.GetAttributeValue(AttributeType.Energy) >= hoe_energy;
            InventoryItemData ivdata = character.EquipData.GetEquippedItem(EquipSlot.Hand);
            ItemData idata = ItemData.Get(ivdata?.item_id);
            return has_energy && idata != null && idata.HasGroup(hoe_item) && !character.IsBusy();
        }

        public void HoeGroundAuto(Vector3 pos)
        {
            Vector3 dir = pos - transform.position;
            if (dir.magnitude > hoe_range)
                return; //Target too far

            if (character.IsBusy() || character.Crafting.ClickedBuild()
                || character.GetAutoSelectTarget() != null || character.GetAutoDropInventory() != null)
                return; //Character busy doing other actions

            PlayerUI ui = PlayerUI.Get(character.player_id);
            if (ui != null && ui.GetSelectedSlot() != null)
                return; //Character trying to drop/merge item

            InventoryItemData ivdata = character.EquipData.GetEquippedItem(EquipSlot.Hand);
            if (ivdata != null && CanHoe())
            {
                HoeGround(pos);
            }
        }
    }

}