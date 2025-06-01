using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using NetcodePlus;

namespace SurvivalEngine
{
    /// <summary>
    /// Script to allow player jumping
    /// </summary>
    
    [RequireComponent(typeof(PlayerCharacter))]
    public class PlayerCharacterJump : SNetworkBehaviour
    {
        public float jump_power = 10f;
        public float jump_duration = 0.2f;

        public UnityAction onJump;

        private PlayerCharacter character;
        private SNetworkActions actions;

        private float jump_timer = 0f;

        protected override void Awake()
        {
            base.Awake();
            character = GetComponent<PlayerCharacter>();
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();
            actions = new SNetworkActions(this);
            actions.Register(ActionType.Jump, DoJump);
        }

        protected override void OnDespawn()
        {
            base.OnDespawn();
            actions.Clear();
        }

        void Update()
        {
            if (TheGame.Get().IsPaused())
                return;

            if (character.IsDead())
                return;

            jump_timer -= Time.deltaTime;
        }

        public void Jump()
        {
            if (!IsJumping() && character.IsGrounded() && !character.IsBusy() && !character.IsRiding() && !character.IsSwimming())
            {
                actions?.Trigger(ActionType.Jump);
            }
        }

        private void DoJump()
        {
            if (!IsJumping() && character.IsGrounded() && !character.IsBusy() && !character.IsRiding() && !character.IsSwimming())
            {
                character.SetFallVect(Vector3.up * jump_power);
                jump_timer = jump_duration;

                if (onJump != null)
                    onJump.Invoke();
            }
        }

        public float GetJumpTimer()
        {
            return jump_timer;
        }

        public bool IsJumping()
        {
            return jump_timer > 0f;
        }
    }

}
