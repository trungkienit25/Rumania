using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NetcodePlus;

namespace SurvivalEngine
{
    /// <summary>
    /// Allows other player to target this player for Actions, will be hidden on yourself
    /// </summary>

    [RequireComponent(typeof(Selectable))]
    public class PlayerSelect : AttackTarget
    {
        private PlayerCharacter character;

        protected override void Awake()
        {
            base.Awake();
            character = GetComponentInParent<PlayerCharacter>();
        }

        protected override void OnReady()
        {
            if (character.IsSelf())
                gameObject.SetActive(false); //Hide selectable if self
        }

        public override void TakeDamage(int damage)
        {
            character.Combat.TakeDamage(damage);
        }

        public override void Kill()
        {
            character.Combat.Kill();
        }

        public override bool CanBeAttacked()
        {
            return !IsDead();
        }

        public override bool IsDead()
        {
            return character.Combat.IsDead(); 
        }

        public override Vector3 GetCenter()
        {
            return character.GetColliderCenter();
        }

        public PlayerCharacter GetPlayer()
        {
            return character;
        }

        public int GetPlayerID()
        {
            return character.PlayerID;
        }
     }
}
