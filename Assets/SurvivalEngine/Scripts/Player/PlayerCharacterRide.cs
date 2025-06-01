using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NetcodePlus;
using Unity.Netcode;

namespace SurvivalEngine
{
    /// <summary>
    /// Script to allow player swimming
    /// Make sure the player character has a unique layer set to it (like Player layer)
    /// </summary>

    [RequireComponent(typeof(PlayerCharacter))]
    public class PlayerCharacterRide : SNetworkBehaviour
    {
        private PlayerCharacter character;
        private bool is_riding = false;
        private AnimalRide riding_animal = null;

        //private PlayerCharacterState sync_state = new PlayerCharacterState();
        private Vector3 controls_move = Vector3.zero;

        private SNetworkActions actions;

        protected override void Awake()
        {
            base.Awake();
            character = GetComponent<PlayerCharacter>();
        }

        protected void Start()
        {
            if (character.IsSelf())
            {
                PlayerControlsMouse mouse = PlayerControlsMouse.Get();
                mouse.onClick += OnClick;
                mouse.onHold += OnMouseHold;
                mouse.onRightClick += OnRightClick;
            }
        }

        protected void OnDestroy()
        {
            if (character.IsSelf())
            {
                PlayerControlsMouse mouse = PlayerControlsMouse.Get();
                mouse.onClick -= OnClick;
                mouse.onHold -= OnMouseHold;
                mouse.onRightClick -= OnRightClick;
            }
        }

        protected override void OnBeforeSpawn()
        {
            base.OnBeforeSpawn();
            SetSpawnData(new SNetworkBehaviourRef(riding_animal));
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();

            actions = new SNetworkActions(this);
            actions.RegisterVector(ActionType.Click, DoClick);
            actions.RegisterVector(ActionType.ClickRight, DoRightClick);
            actions.RegisterVector(ActionType.ClickHold, DoMouseHold);
            actions.RegisterBehaviour(ActionType.OrderStart, DoStartRide);
            actions.Register(ActionType.OrderStop, DoStopRide);

            SNetworkBehaviourRef animal = GetSpawnData<SNetworkBehaviourRef>();
            SetRiderDelayed(animal); //Cannot set animal now because the animal may not have been spawned yet since we are still in OnSpawn
        }

        protected override void OnDespawn()
        {
            base.OnDespawn();
            actions.Clear();
        }

        private void OnClick(Vector3 pos, Selectable select) { actions.Trigger(ActionType.Click, pos); } // DoRightClick(pos)
        private void OnRightClick(Vector3 pos, Selectable select) { actions.Trigger(ActionType.ClickRight, pos); } // DoRightClick(pos)
        private void OnMouseHold(Vector3 pos) { actions.Trigger(ActionType.ClickHold, pos); } // DoMouseHold(pos)

        void Update()
        {
            if (TheGame.Get().IsPaused())
                return;

            if (!is_riding || character.IsDead())
                return;

            if (riding_animal == null || riding_animal.IsDead())
            {
                StopRide();
                return;
            }

            UpdateRiding();
            UpdateControls();
        }

        private void UpdateRiding()
        {
            transform.position = riding_animal.GetRideRoot();
            transform.rotation = Quaternion.LookRotation(riding_animal.transform.forward, Vector3.up);

            PlayerCharacterState state = character.GetSyncState();
            controls_move = state.controls_move;

            Vector3 tmove = controls_move * riding_animal.ride_speed;
            if (tmove.magnitude > 0.1f)
                riding_animal.Character.DirectMoveToward(tmove);

            //Character stuck
            if (tmove.magnitude < 0.1f && riding_animal.Character.IsStuck())
                riding_animal.Character.Stop();
        }

        private void UpdateControls()
        {
            if (!is_riding || riding_animal == null)
                return;

            if (!IsOwner)
                return;

            PlayerControls controls = PlayerControls.Get();

            //Stop riding
            if (character.IsControlsEnabled())
            {
                if (controls.IsPressJump() || controls.IsPressAction() || controls.IsPressUICancel())
                    StopRide();
            }
        }
        
        public void RideNearest()
        {
            AnimalRide animal = AnimalRide.GetNearest(transform.position, 2f);
            RideAnimal(animal);
        }

        public void RideAnimal(AnimalRide animal)
        {
            if (!is_riding)
            {
                actions.Trigger(ActionType.OrderStart, animal);
            }
        }

        public void StopRide()
        {
            if (is_riding)
            {
                actions.Trigger(ActionType.OrderStop);
            }
        }

        private void DoStartRide(SNetworkBehaviour sobj)
        {
            AnimalRide animal = sobj?.Get<AnimalRide>();

            if (!is_riding && character.IsMovementEnabled() && animal != null)
            {
                is_riding = true;
                character.SetBusy(true);
                character.DisableMovement();
                character.DisableCollider();
                riding_animal = animal;
                transform.position = animal.GetRideRoot();
                animal.SetRider(character);
            }
        }

        private void DoStopRide()
        {
            if (is_riding)
            {
                if (riding_animal != null)
                    riding_animal.StopRide();
                is_riding = false;
                controls_move = Vector3.zero;
                character.SetBusy(false);
                character.EnableMovement();
                character.EnableCollider();
                character.Teleport(riding_animal.GetRideRoot());
                character.FaceDir(transform.forward);
                riding_animal = null;
            }
        }

        private void SetRiderDelayed(SNetworkBehaviourRef beha_ref)
        {
            if (IsServer || beha_ref.net_id == 0)
                return;

            //Wait in case the other object wasnt spawned yet
            TimeTool.WaitFor(1f, () =>
            {
                AnimalRide animal = beha_ref.Get<AnimalRide>();
                if (animal != null)
                    RideAnimal(animal);
            });
        }

        //--- on Click

        private void DoClick(Vector3 pos)
        {
            if (riding_animal != null)
            {
                if (character.interact_type == PlayerInteractBehavior.MoveAndInteract)
                    riding_animal.Character.MoveTo(pos);
            }
        }

        private void DoMouseHold(Vector3 pos)
        {
            if (TheGame.IsMobile())
                return; //On mobile, use joystick instead, no mouse hold

            if (riding_animal != null)
            {
                if (character.interact_type == PlayerInteractBehavior.MoveAndInteract)
                    riding_animal.Character.DirectMoveTo(pos);
            }
        }

        private void DoRightClick(Vector3 pos)
        {
            if (riding_animal != null)
            {
                riding_animal.RemoveRider();
            }
        }

        //-----

        public bool IsRiding()
        {
            return is_riding;
        }

        public AnimalRide GetAnimal()
        {
            return riding_animal;
        }

        public PlayerCharacter GetCharacter()
        {
            return character;
        }
    }

}
