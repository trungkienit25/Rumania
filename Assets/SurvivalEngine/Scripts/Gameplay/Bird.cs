using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NetcodePlus;

namespace SurvivalEngine
{

    /// <summary>
    /// Birds are alternate version of the animal script but with flying!
    /// </summary>

    public enum BirdState
    {
        Sit = 0,
        Fly = 2,
        FlyDown = 4,
        Alerted = 5,
        Hide = 7,
        Dead = 10,
    }

    [RequireComponent(typeof(Character))]
    public class Bird : SNetworkBehaviour
    {
        [Header("Fly")]
        public float wander_radius = 10f;
        public float fly_duration = 20f;
        public float sit_duration = 20f;

        [Header("Vision")]
        public float detect_range = 5f;
        public float detect_angle = 360f;
        public float detect_360_range = 1f;
        public float reaction_time = 0.2f;

        [Header("Models")]
        public Animator sit_model;
        public Animator fly_model;

        private Character character;
        private Selectable selectable;
        private Destructible destruct;
        private Collider[] colliders;
        private SNetworkActions actions;

        private BirdState state = BirdState.Sit;
        private float state_timer = 0f;
        private Transform transf;
        private Vector3 start_pos;
        private Vector3 target_pos;
        private float update_timer = 0f;

        protected override void Awake()
        {
            base.Awake();
            character = GetComponent<Character>();
            selectable = GetComponent<Selectable>();
            destruct = GetComponent<Destructible>();
            colliders = GetComponentsInChildren<Collider>();
            transf = transform;
            start_pos = transform.position;
            target_pos = transform.position;
            destruct.onDeath += OnDeath;
            state_timer = 99f; //Fly right away
            update_timer = Random.Range(-1f, 1f);

            transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();
            actions = new SNetworkActions(this);
            actions.RegisterInt(ActionType.SyncObject, DoChangeState);

            if (!IsServer)
                DoHide();
        }

        protected override void OnDespawn()
        {
            base.OnDespawn();
            actions.Clear();
        }

        protected override void OnReady()
        {
            base.OnReady();
            DoLand();
        }

        void Update()
        {
            if (TheGame.Get().IsPaused())
                return;

            if (!IsSpawned || !IsServer)
                return;

            state_timer += Time.deltaTime;

            if (state == BirdState.Sit)
            {
                if (state_timer > sit_duration)
                {
                    FlyAway();
                }
            }

            if (state == BirdState.Alerted)
            {
                if (state_timer > reaction_time)
                {
                    FlyAway();
                }
            }

            if (state == BirdState.Fly)
            {
                if (state_timer > 2f && fly_model.gameObject.activeSelf && character.HasReachedMoveTarget())
                    Hide();
                if (state_timer > 5f)
                    Hide();
            }

            if (state == BirdState.Hide)
            {
                if (state_timer > fly_duration)
                {
                    StopFly();
                }
            }

            if (state == BirdState.FlyDown)
            {
                if (character.HasReachedMoveTarget())
                {
                    Land();
                }
            }

            update_timer += Time.deltaTime;
            if (update_timer > 0.5f)
            {
                update_timer = Random.Range(-0.1f, 0.1f);
                SlowUpdate(); //Optimization
            }
        }

        private void SlowUpdate()
        {
            if (state == BirdState.Sit)
            {
                DetectThreat();
            }
        }

        public void FlyAway()
        {
            if (state == BirdState.Sit || state == BirdState.Alerted)
            {
                ChangeState(BirdState.Fly); //DoFlyAway()
            }
        }

        public void StopFly()
        {
            if (state == BirdState.Fly || state == BirdState.Hide)
            {
                ChangeState(BirdState.FlyDown); //DoStopFly()
            }
        }

        public void Land()
        {
            if (state == BirdState.FlyDown)
            {
                ChangeState(BirdState.Sit); //DoLand()
            }
        }

        public void Alert()
        {
            if (state == BirdState.Sit)
            {
                ChangeState(BirdState.Alerted); //DoAlert()
            }
        }

        public void Hide()
        {
            ChangeState(BirdState.Hide); //DoHide()
        }

        private void ChangeState(BirdState state)
        {
            state_timer = 0f;
            actions?.Trigger(ActionType.SyncObject, (int)state); //DoChangeState()
        }

        private void DoFlyAway()
        {
            if (state == BirdState.Sit || state == BirdState.Alerted)
            {
                state_timer = 0f;
                FindFlyPosition(transf.position, wander_radius, out target_pos);
                state = BirdState.Fly;
                sit_model.gameObject.SetActive(false);
                fly_model.gameObject.SetActive(true);

                foreach (Collider collide in colliders)
                    collide.enabled = false;

                if (IsServer)
                    character.MoveTo(target_pos);
            }
        }

        private void DoStopFly()
        {
            if (state == BirdState.Fly || state == BirdState.Hide)
            {
                state_timer = 0f;
                Vector3 npos;
                bool succes = FindGroundPosition(start_pos, wander_radius, out npos);
                if (succes)
                {
                    state = BirdState.FlyDown;
                    target_pos = npos;
                    fly_model.gameObject.SetActive(true);
                    sit_model.gameObject.SetActive(false);

                    foreach (Collider collide in colliders)
                        collide.enabled = false;

                    if (IsServer)
                        character.MoveTo(target_pos);
                }
            }
        }

        private void DoLand()
        {
            if (state == BirdState.FlyDown)
            {
                state_timer = Random.Range(-1f, 1f);
                state = BirdState.Sit;
                sit_model.gameObject.SetActive(true);
                fly_model.gameObject.SetActive(false);

                foreach (Collider collide in colliders)
                    collide.enabled = true;
            }
        }

        private void DoAlert()
        {
            if (state == BirdState.Sit)
            {
                state_timer = 0f;
                state = BirdState.Alerted;
                StopMoving();
            }
        }

        private void DoHide()
        {
            state_timer = Random.Range(-1f, 1f);
            state = BirdState.Hide;
            sit_model.gameObject.SetActive(false);
            fly_model.gameObject.SetActive(false);
        }

        private void DoChangeState(int istate)
        {
            BirdState astate = (BirdState) istate;
            if (astate == BirdState.Fly)
                DoFlyAway();
            else if (astate == BirdState.FlyDown)
                DoStopFly();
            else if (astate == BirdState.Sit)
                DoLand();
            else if (astate == BirdState.Alerted)
                DoAlert();
            else if (astate == BirdState.Hide)
                DoHide();
        }


        private void OnDeath()
        {
            StopMoving();
            state = BirdState.Dead;
            state_timer = 0f;
            sit_model.gameObject.SetActive(true);
            fly_model.gameObject.SetActive(false);
            sit_model.SetTrigger("Death");
        }

        private bool FindFlyPosition(Vector3 pos, float radius, out Vector3 fly_pos)
        {
            Vector3 offest = new Vector3(Random.Range(-radius, radius), 0f, Random.Range(radius, radius));
            fly_pos = pos + offest;
            fly_pos.y = start_pos.y + 20f;
            return true;
        }

        //Find landing position to make sure it wont land on an obstacle
        private bool FindGroundPosition(Vector3 pos, float radius, out Vector3 ground_pos)
        {
            Vector3 offest = new Vector3(Random.Range(-radius, radius), 0f, Random.Range(radius, radius));
            Vector3 center = pos + offest;
            bool found = PhysicsTool.FindGroundPosition(center, 50f, character.ground_layer.value, out ground_pos);
            return found;
        }

        //Detect if the player is in vision
        private void DetectThreat()
        {
            Vector3 pos = transf.position;

            //React to player
            foreach (PlayerCharacter player in PlayerCharacter.GetAll())
            {
                Vector3 char_dir = (player.transform.position - pos);
                if (char_dir.magnitude < detect_range)
                {
                    float dangle = detect_angle / 2f; // /2 for each side
                    float angle = Vector3.Angle(transf.forward, char_dir.normalized);
                    if (angle < dangle || char_dir.magnitude < detect_360_range)
                    {
                        Alert();
                        return;
                    }
                }
            }

            //React to other characters
            foreach (Destructible destruct in Destructible.GetAllActive())
            {
                if (destruct != null && destruct.Selectable != selectable)
                {
                    if (destruct.target_group != this.destruct.target_group)
                    {
                        Character character = destruct.Selectable.Character;
                        if (character != null && character.attack_enabled) //Only afraid if the character can attack
                        {
                            Vector3 dir = (destruct.Selectable.GetPosition() - pos);
                            if (dir.magnitude < detect_range)
                            {
                                Alert();
                                return;
                            }
                        }
                    }
                }
            }
        }

        public void StopMoving()
        {
            target_pos = transf.position;
            state_timer = 0f;
            character.Stop();
        }
    }

}