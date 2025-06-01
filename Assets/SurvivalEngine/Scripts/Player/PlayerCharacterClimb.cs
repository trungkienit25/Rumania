using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NetcodePlus;
using Unity.Netcode;

namespace SurvivalEngine
{
    /// <summary>
    /// Add this script to the player to be able to climb ladders
    /// </summary>

    [RequireComponent(typeof(PlayerCharacter))]
    public class PlayerCharacterClimb : SNetworkBehaviour
    {
        public float climb_speed = 2f;
        public float climb_offset = 0.5f;

        private PlayerCharacter character;
        private Rigidbody rigid;
        private SNetworkActions actions;

        private Ladder climb_ladder = null;
        private bool climbing = false;
        private Vector3 move_vect;
        private Vector3 current_offset;

        private bool auto_move = false;
        private float auto_move_height = 0f;
        private float climb_timer = 0f;

        protected override void Awake()
        {
            base.Awake();
            character = GetComponent<PlayerCharacter>();
            rigid = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            if (IsOwner)
            {
                PlayerControlsMouse controls = PlayerControlsMouse.Get();
                controls.onClick += OnClick;
            }
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();
            actions = new SNetworkActions(this);
            actions.RegisterVector(ActionType.Click, DoClick);
            actions.RegisterBehaviour(ActionType.Climb, DoClimb);
            actions.RegisterVector(ActionType.OrderStop, DoStopClimb);
        }

        protected override void OnDespawn()
        {
            base.OnDespawn();
            actions.Clear();
        }

        private void OnClick(Vector3 pos, Selectable select) { actions.Trigger(ActionType.Click, pos); } // DoClickFloor(pos)

        void Update()
        {
            climb_timer += Time.deltaTime;

            if (IsClimbing())
            {
                UpdateControls();
            }
        }

        private void FixedUpdate()
        {
            if(IsClimbing())
            {
                UpdateClimbing();
            }
        }

        private void UpdateClimbing()
        {
            //Climb up and down
            PlayerCharacterState state = character.GetSyncState();
            float side = Vector3.Dot(state.controls_move, state.cam_dir);
            side = Mathf.Sign(side) * state.controls_move.magnitude;

            move_vect = Vector3.zero;
            if (Mathf.Abs(side) > 0.1f)
            {
                move_vect = Vector3.up * side * climb_speed;
                auto_move = false;
            }

            //Auto move
            if (auto_move)
            {
                Vector3 dir = Vector3.up * (auto_move_height - transform.position.y);
                if (dir.magnitude > 0.1f)
                {
                    move_vect = dir.normalized * climb_speed;
                }
            }

            //Face ladder
            character.FaceTorward(transform.position - current_offset);

            //Snap to ladder
            if (climb_ladder != null)
            {
                Vector3 tpos = new Vector3(climb_ladder.transform.position.x, rigid.position.y, climb_ladder.transform.position.z) + current_offset;
                rigid.position = Vector3.Lerp(rigid.position, tpos, 10f * Time.fixedDeltaTime);
            }

            //Reach bottom and top
            if (character.IsGrounded() && move_vect.y < -0.1f)
                StopClimb();
            if (climb_ladder != null && character.transform.position.y < climb_ladder.GetBounds().min.y && move_vect.y < -0.1f)
                StopClimb();
            if (climb_ladder != null && character.transform.position.y > climb_ladder.GetBounds().max.y && move_vect.y > 0.1f)
                StopClimbTop();
            if (climb_ladder == null)
                StopClimb();

        }

        private void UpdateControls()
        {
            //Stop climb with button
            PlayerControls controls = PlayerControls.Get();
            if ((controls.IsPressAction() || controls.IsPressJump()) && climb_timer > 0.5f)
                StopClimb();
        }

        private void DoClick(Vector3 pos) {
            if (IsClimbing())
            {
                auto_move = true;
                auto_move_height = pos.y;
                if (auto_move_height > transform.position.y)
                    auto_move_height += 1f;
                if (auto_move_height < transform.position.y)
                    auto_move_height -= 1f;
            }
        }

        public void Climb(Ladder ladder)
        {
            if (ladder != null && !IsClimbing() && character.IsMovementEnabled() && climb_timer > 0.5f)
            {
                actions?.Trigger(ActionType.Climb, ladder);
            }
        }

        private void DoClimb(SNetworkBehaviour sdata)
        {
            Ladder ladder = sdata.Get<Ladder>();
            if (ladder != null && !IsClimbing() && climb_timer > 0.5f)
            {
                climb_ladder = ladder;
                character.StopMove();
                character.DisableMovement();
                Vector3 dir = ladder.GetOffsetDir(rigid.position);
                if (ladder.IsSideBlocked(dir) && !ladder.IsSideBlocked(-dir))
                    dir = -dir; //Reverse offset if direction blocked
                current_offset = dir.normalized * climb_offset;
                rigid.position = new Vector3(ladder.transform.position.x, rigid.position.y, ladder.transform.position.z) + current_offset;
                rigid.rotation = Quaternion.LookRotation(-current_offset, Vector3.up);
                auto_move = false;
                climbing = true;
                climb_timer = 0f;
            }
        }

        public void StopClimb()
        {
            StopClimb(transform.position);
        }

        public void StopClimbTop()
        {
            Vector3 dir = climb_ladder.GetOffsetDir(transform.position);
            Vector3 jump_offset = Quaternion.LookRotation(-dir, Vector3.up) * climb_ladder.top_jump_offset;
            Vector3 pos = transform.position + jump_offset;
            StopClimb(pos);
        }

        private void StopClimb(Vector3 pos)
        {
            if (IsClimbing())
            {
                actions?.Trigger(ActionType.OrderStop, pos); // DoStopClimb(pos)
            }
        }

        private void DoStopClimb(Vector3 pos)
        {
            if (IsClimbing())
            {
                climb_ladder = null;
                character.EnableMovement();
                character.StopMove();
                character.FaceTorward(transform.position - current_offset);
                rigid.position = pos;
                auto_move = false;
                climbing = false;
                climb_timer = 0f;
            }
        }

        public bool IsMoving()
        {
            return move_vect.magnitude > 0.1f;
        }

        public Vector3 GetMove()
        {
            return move_vect;
        }

        public bool IsClimbing()
        {
            return climbing;
        }
    }
}
