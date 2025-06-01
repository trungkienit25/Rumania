using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NetcodePlus;

namespace SurvivalEngine
{
    /// <summary>
    /// Allow you to ride the animal
    /// </summary>
    
    [RequireComponent(typeof(Character))]
    public class AnimalRide : SNetworkBehaviour
    {
        public float ride_speed = 5f;
        public Transform ride_root;
        public bool use_navmesh = true;

        private Character character;
        private Selectable select;
        private Animator animator;
        private AnimalWild wild;
        private AnimalLivestock livestock;
        private float regular_speed;
        private bool default_avoid;
        private bool default_navmesh;

        private PlayerCharacter rider = null;

        private static List<AnimalRide> animal_list = new List<AnimalRide>();

        protected override void Awake()
        {
            base.Awake();
            animal_list.Add(this);
            character = GetComponent<Character>();
            select = GetComponent<Selectable>();
            wild = GetComponent<AnimalWild>();
            livestock = GetComponent<AnimalLivestock>();
            animator = GetComponentInChildren<Animator>();
            regular_speed = character.move_speed;
            default_avoid = character.avoid_obstacles;
            default_navmesh = character.use_navmesh;
            character.onDeath += OnDeath;
        }

        protected void OnDestroy()
        {
            animal_list.Remove(this);
        }

        void Update()
        {
            if (TheGame.Get().IsPaused())
                return;

            if (IsDead())
                return;

            UpdateServer();

            //Animations
            if (animator.enabled)
            {
                animator.SetBool("Move", IsMoving());
                animator.SetBool("Run", IsMoving());
            }
        }

        void UpdateServer()
        {
            if (!IsServer)
                return;

            //Change owner
            if (rider != null)
            {
                ClientData client = TheNetwork.Get().GetClientByPlayerID(rider.player_id);
                if (client != null && OwnerId != client.client_id)
                    NetObject.ChangeOwner(client.client_id);
            }
            else
            {
                if(OwnerId != TheNetwork.Get().ServerID)
                    NetObject.ChangeOwner(TheNetwork.Get().ServerID);
            }
        }

        public void SetRider(PlayerCharacter player)
        {
            if (rider == null) {
                rider = player;
                character.move_speed = ride_speed;
                character.avoid_obstacles = false;
                character.use_navmesh = use_navmesh;
                character.Stop();
                if (wild != null)
                    wild.enabled = false;
                if (livestock != null)
                    livestock.enabled = false;
            }
        }

        public void StopRide()
        {
            if (rider != null)
            {
                rider = null;
                character.move_speed = regular_speed;
                character.avoid_obstacles = default_avoid;
                character.use_navmesh = default_navmesh;
                StopMove();
                if (wild != null)
                    wild.enabled = true;
                if (livestock != null)
                    livestock.enabled = true;
            }
        }

        public void StopMove()
        {
            character.Stop();
            animator.SetBool("Move", false);
            animator.SetBool("Run", false);
        }

        public void RemoveRider()
        {
            if (rider != null)
            {
                rider.Riding.StopRide();
            }
        }

        void OnDeath()
        {
            animator.SetTrigger("Death");
        }

        public bool IsDead()
        {
            return character.IsDead();
        }

        public bool IsMoving()
        {
            return character.IsMoving();
        }

        public bool HasRider()
        {
            return rider != null;
        }

        public Vector3 GetMove()
        {
            return character.GetMove();
        }

        public Vector3 GetFacing()
        {
            return character.GetFacing();
        }

        public Vector3 GetRideRoot()
        {
            return ride_root != null ? ride_root.position : transform.position;
        }

        public Character Character { get { return character; } }

        public static AnimalRide GetNearest(Vector3 pos, float range = 999f)
        {
            float min_dist = range;
            AnimalRide nearest = null;
            foreach (AnimalRide animal in animal_list)
            {
                float dist = (animal.transform.position - pos).magnitude;
                if (dist < min_dist)
                {
                    min_dist = dist;
                    nearest = animal;
                }
            }
            return nearest;
        }

        public static List<AnimalRide> GetAll()
        {
            return animal_list;
        }
    }

}
