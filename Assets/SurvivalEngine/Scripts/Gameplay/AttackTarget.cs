using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NetcodePlus;

namespace SurvivalEngine
{

    /// <summary>
    /// Something that can be attacked, usually either a Destructible or PlayerSelect
    /// </summary>

    public class AttackTarget : SNetworkBehaviour
    {
        protected Selectable select;

        protected override void Awake()
        {
            base.Awake();
            select = GetComponent<Selectable>();
        }

        public virtual void TakeDamage(Destructible attacker, int damage)
        {
            //Need to be overriden
            TakeDamage(damage);
        }

        public virtual void TakeDamage(PlayerCharacter player, int damage)
        {
            //Need to be overriden
            TakeDamage(damage);
        }

        public virtual void TakeDamage(int damage)
        {
            //Need to be overriden
        }

        public virtual void Kill()
        {
            //Need to be overriden
        }

        public virtual bool CanBeAttacked()
        {
            return !IsDead(); //Need to be overriden
        }

        public virtual bool CanBeAttackedRanged()
        {
            return !IsDead(); //Need to be overriden
        }

        //Can it be attacked at all?
        public virtual bool CanBeAttacked(PlayerCharacter player)
        {
            return !IsDead(); //Need to be overriden
        }
        
        //Will it be attacked with a simple click?
        public virtual bool CanBeAutoAttacked(PlayerCharacter player)
        {
            return !IsDead(); //Need to be overriden
        }

        public virtual float GetHitRange()
        {
            return 1f; //Need to be overriden
        }

        public virtual bool IsDead()
        {
            return false;  //Need to be overriden
        }

        public virtual Vector3 GetCenter()
        {
            return transform.position; //Need to be overriden
        }

        public Selectable Selectable { get { return select; } } //May be null

    }
}
