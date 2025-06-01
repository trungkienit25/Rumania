using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using NetcodePlus;

namespace SurvivalEngine
{
    public enum AnimalState
    {
        Wander = 0,
        Alerted = 2,
        Escape = 4,
        Attack = 6,
        MoveTo = 10,
        Dead = 20,
    }

    public enum AnimalBehavior
    {
        None = 0,   //Custom behavior from another script
        Escape = 5,  //Escape on sight
        PassiveEscape = 10,  //Escape if attacked 
        PassiveDefense = 15, //Attack if attacked
        Aggressive = 20, //Attack on sight, goes back after a while
        VeryAggressive = 25, //Attack on sight, will not stop following
    }

    public enum WanderBehavior
    {
        None = 0, //Dont wander
        WanderNear = 10, //Will wander near starting position
        WanderFar = 20, //Will wander beyond starting position
    }

    /// <summary>
    /// Animal behavior script for wandering, escaping, or chasing the player
    /// </summary>

    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Selectable))]
    [RequireComponent(typeof(Destructible))]
    [RequireComponent(typeof(Character))]
    public class AnimalWild : SNetworkBehaviour
    {
        [Header("Behavior")]
        public WanderBehavior wander = WanderBehavior.WanderNear;
        public AnimalBehavior behavior = AnimalBehavior.PassiveEscape;

        [Header("Move")]
        public float wander_speed = 2f;
        public float run_speed = 5f;
        public float wander_range = 10f;
        public float wander_interval = 10f;

        [Header("Vision")]
        public float detect_range = 5f;
        public float detect_angle = 360f;
        public float detect_360_range = 1f;
        public float reaction_time = 0.5f; //How fast it detects threats

        [Header("Actions")]
        public float action_duration = 10f; //How long will it attack/escape targets

        public UnityAction onAttack;
        public UnityAction onDamaged;
        public UnityAction onDeath;

        private AnimalState state;
        private Character character;
        private Selectable selectable;
        private Destructible destruct;
        private Animator animator;

        private Transform transf;
        private Vector3 start_pos;

        private AttackTarget attack_target = null;
        private Vector3 wander_target;

        private SNetworkActions actions;

        private bool is_running = false;
        private float state_timer = 0f;

        private float lure_interest = 8f;
        private bool force_action = false;
        private float update_timer = 0f;

        protected override void Awake()
        {
            base.Awake();
            character = GetComponent<Character>();
            destruct = GetComponent<Destructible>();
            selectable = GetComponent<Selectable>();
            animator = GetComponentInChildren<Animator>();
            transf = transform;
            start_pos = transform.position;
            state_timer = 99f; //Find wander right away
            update_timer = Random.Range(-1f, 1f);

            if (wander != WanderBehavior.None)
                transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }

        protected void Start()
        {
            character.onAttack += OnAttack;
            destruct.onDamaged += OnDamaged;
            destruct.onDamagedBy += OnDamagedBy;
            destruct.onDamagedByPlayer += OnDamagedPlayer;
            destruct.onDeath += OnDeath;
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();
            actions = new SNetworkActions(this);
            actions.RegisterSerializable(ActionType.OrderMove, DoMoveTo);
            actions.Register(ActionType.OrderStop, DoStop);
            actions.RegisterInt(ActionType.SyncObject, DoChangeState);
        }

        protected override void OnDespawn()
        {
            base.OnDespawn();
            actions.Clear();
        }

        private void Update()
        {
            //Animations
            bool paused = TheGame.Get().IsPaused();
            if (animator != null)
                animator.enabled = !paused;

            if (TheGame.Get().IsPaused())
                return;

            if (state == AnimalState.Dead || behavior == AnimalBehavior.None)
                return;

            if (!IsSpawned)
                return;

            state_timer += Time.deltaTime;
            character.move_speed = is_running ? run_speed : wander_speed;

            //States transitions
            UpdateStates();

            update_timer += Time.deltaTime;
            if (update_timer > 0.5f)
            {
                update_timer = Random.Range(-0.1f, 0.1f);
                SlowUpdate(); //Optimization
            }

            //Animations
            if (animator != null && animator.enabled)
            {
                animator.SetBool("Move", IsMoving());
                animator.SetBool("Run", IsRunning());
            }
        }

        private void UpdateStates()
        {
            is_running = (state == AnimalState.Escape || state == AnimalState.Attack);

            if (state == AnimalState.Alerted && attack_target != null)
                character.FaceTorward(attack_target.transform.position);

            if (!IsServer)
                return; //Server only code

            if (state == AnimalState.Wander)
            {
                if (state_timer > wander_interval && wander != WanderBehavior.None)
                {
                    state_timer = Random.Range(-1f, 1f);
                    FindWanderTarget();
                    character.MoveTo(wander_target);
                }

                //Character stuck
                if (character.IsStuck())
                    character.Stop();
            }

            if (state == AnimalState.Alerted)
            {
                if (attack_target == null)
                {
                    character.Stop();
                    ChangeState(AnimalState.Wander);
                    return;
                }

                if (state_timer > reaction_time)
                {
                    ReactToThreat();
                }
            }

            if (state == AnimalState.Escape)
            {
                if (attack_target == null || attack_target.IsDead())
                {
                    StopAction();
                    return;
                }

                if (!force_action && state_timer > action_duration)
                {
                    Vector3 targ_dir = (attack_target.transform.position - transf.position);
                    targ_dir.y = 0f;

                    if (targ_dir.magnitude > detect_range)
                    {
                        StopAction();
                    }
                }
            }

            if (state == AnimalState.Attack)
            {
                if (attack_target == null || attack_target.IsDead())
                {
                    StopAction();
                    return;
                }

                //Very aggressive wont stop following 
                if (!force_action && behavior != AnimalBehavior.VeryAggressive && state_timer > action_duration)
                {
                    Vector3 targ_dir = attack_target.transform.position - transf.position;
                    Vector3 start_dir = start_pos - transf.position;

                    float follow_range = detect_range * 2f;
                    bool cant_see = targ_dir.magnitude > follow_range;
                    bool too_far = wander == WanderBehavior.WanderNear && start_dir.magnitude > Mathf.Max(wander_range, follow_range);
                    if (cant_see || too_far)
                    {
                        StopAction();
                        MoveToTarget(wander_target, false);
                    }
                }
            }

            if (state == AnimalState.MoveTo)
            {
                if (character.HasReachedMoveTarget())
                    StopAction();
            }
        }

        private void SlowUpdate()
        {
            if (!IsServer)
                return; //Server only code

            if (state == AnimalState.Wander)
            {
                //These behavior trigger a reaction on sight, while the "Defense" behavior only trigger a reaction when attacked
                if (behavior == AnimalBehavior.Aggressive || behavior == AnimalBehavior.VeryAggressive || behavior == AnimalBehavior.Escape)
                {
                    DetectThreat(detect_range);

                    if (attack_target != null)
                    {
                        character.Stop();
                        ChangeState(AnimalState.Alerted);
                    }
                }
            }

            if (state == AnimalState.Attack)
            {
                if (character.IsStuck() && !character.IsAttacking() && state_timer > 1f)
                {
                    DetectThreat(detect_range);
                    ReactToThreat();
                }
            }
        }

        //Detect if the player is in vision
        private void DetectThreat(float range)
        {
            Vector3 pos = transf.position;

            //React to player
            float min_dist = range;
            foreach (PlayerCharacter player in PlayerCharacter.GetAll())
            {
                Vector3 char_dir = (player.transform.position - pos);
                float dist = char_dir.magnitude;
                if (dist < min_dist && !player.IsDead())
                {
                    float dangle = detect_angle / 2f; // /2 for each side
                    float angle = Vector3.Angle(transf.forward, char_dir.normalized);
                    if (angle < dangle || char_dir.magnitude < detect_360_range)
                    {
                        attack_target = player.Combat;
                        min_dist = dist;
                    }
                }
            }

            //React to other characters/destructibles
            foreach (Destructible destruct in Destructible.GetAllActive())
            {
                if (destruct != null && destruct != selectable && !destruct.IsDead())
                {
                    bool valid_target = false;

                    //Find destructible to attack
                    if (HasAttackBehavior())
                    {
                        if (destruct.target_team == AttackTeam.Ally || destruct.target_team == AttackTeam.Enemy) //Attack by default (not neutral)
                        {
                            if (destruct.target_team != this.destruct.target_team || destruct.target_group != this.destruct.target_group) //Is not in same team
                            {
                                valid_target = true; //For optimization, check if target is valid before doing distance check
                            }
                        }
                    }

                    //Find character to escape
                    if (HasEscapeBehavior())
                    {
                        Character character = destruct.Selectable.Character;
                        if (character != null && character.attack_enabled) //Only afraid if the character can attack
                        {
                            if (character.Destructible.target_group != this.destruct.target_group) //Not afraid if in same team
                            {
                                valid_target = true; //For optimization, check if target is valid before doing distance checks
                            }
                        }
                    }

                    if (valid_target)
                    {
                        Vector3 dir = (destruct.Selectable.GetPosition() - pos);
                        if (dir.magnitude < min_dist)
                        {
                            float dangle = detect_angle / 2f; // /2 for each side
                            float angle = Vector3.Angle(transf.forward, dir.normalized);
                            if (angle < dangle || dir.magnitude < detect_360_range)
                            {
                                attack_target = destruct;
                                min_dist = dir.magnitude;
                            }
                        }
                    }
                }
            }
        }

        //React to player if seen by animal
        private void ReactToThreat()
        {
            if (attack_target == null || IsDead())
                return;

            if (HasEscapeBehavior())
            {
                ChangeState(AnimalState.Escape);
                character.Escape(attack_target.NetObject);
            }
            else if (HasAttackBehavior())
            {
                ChangeState(AnimalState.Attack);
                character.Attack(attack_target);
            }
        }

        private void FindWanderTarget()
        {
            float range = Random.Range(0f, wander_range);
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            Vector3 spos = wander == WanderBehavior.WanderFar ? transf.position : start_pos;
            Vector3 pos = spos + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * range;
            wander_target = pos;

            Lure lure = Lure.GetNearestInRange(transf.position);
            if (lure != null)
            {
                Vector3 dir = lure.transform.position - transf.position;
                dir.y = 0f;

                Vector3 center = transf.position + dir.normalized * dir.magnitude * 0.5f;
                if (lure_interest < 4f)
                    center = lure.transform.position;

                float range2 = Mathf.Clamp(lure_interest, 1f, wander_range);
                Vector3 pos2 = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * range2;
                wander_target = pos2;

                lure_interest = lure_interest * 0.5f;
                if (lure_interest <= 0.2f)
                    lure_interest = 8f;
            }
        }

        public void AttackTarget(AttackTarget target)
        {
            if (target != null)
            {
                ChangeState(AnimalState.Attack);
                this.attack_target = target;
                force_action = true;
                character.Attack(target);
            }
        }

        public void EscapeTarget(AttackTarget target)
        {
            if (target != null)
            {
                ChangeState(AnimalState.Escape);
                this.attack_target = target;
                force_action = true;
                character.Escape(target.NetObject);
            }
        }

        public void MoveToTarget(Vector3 pos, bool run)
        {
            actions?.Trigger(ActionType.OrderMove, new AnimalMoveSync(pos, run)); //DoMoveTo
        }

        public void StopAction()
        {
            actions?.Trigger(ActionType.OrderStop); //DoStop
        }

        public void ChangeState(AnimalState state)
        {
            actions?.Trigger(ActionType.SyncObject, (int)state); //DoChangeState
        }

        private void DoMoveTo(SerializedData sdata)
        {
            AnimalMoveSync sync = sdata.Get<AnimalMoveSync>();
            is_running = sync.run;
            force_action = true;
            DoChangeState((int)AnimalState.MoveTo);
            character.MoveTo(sync.pos);
        }

        private void DoStop()
        {
            character.Stop();
            is_running = false;
            force_action = false;
            attack_target = null;
            DoChangeState((int)AnimalState.Wander);
        }

        private void DoChangeState(int istate)
        {
            this.state = (AnimalState) istate;
            state_timer = 0f;
            lure_interest = 8f;
        }

        public void Reset()
        {
            StopAction();
            character.Reset();
            animator.Rebind();
        }

        private void OnDamaged()
        {
            if (IsDead())
                return;

            if (onDamaged != null)
                onDamaged.Invoke();

            if (animator != null)
                animator.SetTrigger("Damaged");
        }

        private void OnDamagedPlayer(PlayerCharacter player)
        {
            if (IsDead() || state_timer < 2f)
                return;

            attack_target = player.Combat;
            ReactToThreat();
        }

        private void OnDamagedBy(Destructible attacker)
        {
            if (IsDead() || state_timer < 2f)
                return;

            attack_target = attacker;
            ReactToThreat();
        }

        private void OnDeath()
        {
            state = AnimalState.Dead;

            if (onDeath != null)
                onDeath.Invoke();

            if (animator != null)
                animator.SetTrigger("Death");
        }

        void OnAttack()
        {
            if (animator != null)
                animator.SetTrigger("Attack");
        }

        public bool HasAttackBehavior()
        {
            return behavior == AnimalBehavior.Aggressive || behavior == AnimalBehavior.VeryAggressive || behavior == AnimalBehavior.PassiveDefense;
        }

        public bool HasEscapeBehavior()
        {
            return behavior == AnimalBehavior.Escape || behavior == AnimalBehavior.PassiveEscape;
        }

        public bool IsDead()
        {
            return character.IsDead();
        }

        public bool IsMoving()
        {
            return character.IsMoving();
        }

        public bool IsRunning()
        {
            return character.IsMoving() && is_running;
        }

        public string GetUID()
        {
            return character.GetUID();
        }
    }

}
