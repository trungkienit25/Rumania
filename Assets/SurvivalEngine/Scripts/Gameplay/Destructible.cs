using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using NetcodePlus;

namespace SurvivalEngine
{
    public enum AttackTeam
    {
        Neutral=0, //Can be attacked by anyone, but won't be attacked automatically, use for resources
        Ally=10, //Will be attacked automatically by wild animals, cant be attacked by the player unless it has the required_item.
        Enemy=20, //Will be attacked automatically by allied pets and wild animals (unless in same team group), can be attacked by anyone.
        CantAttack =50, //Cannot be attacked
    }

    /// <summary>
    /// Destructibles are objects that can be destroyed. They have HP and can be damaged by the player or by animals. 
    /// They often spawn loot items when destroyed (or killed)
    /// </summary>

    [RequireComponent(typeof(Selectable))]
    [RequireComponent(typeof(UniqueID))]
    public class Destructible : AttackTarget
    {
        [Header("Stats")]
        public int hp = 100;
        public int armor = 0; //Reduces each attack's damage by the armor value
        public float hp_regen = 0f; //HP regen per game-hour

        [Header("Targeting")]
        public AttackTeam target_team; //Check above for description of each group
        public GroupData target_group; //Enemies of the same group won't attack each other.
        public GroupData required_item; //Required item to attack it (Only the player is affected by this)
        public bool attack_melee_only = true; //If set to true, this object cannot be attacked with a ranged weapon
        public float hit_range = 1f; //Range from which it can be attacked

        [Header("Loot")]
        public int xp = 0;
        public string xp_type;
        public SData[] loots;

        [Header("FX")]
        public bool shake_on_hit = true; //Shake animation when hit
        public float destroy_delay = 0f; //In seconds, use this if you want a death animation before the object disappears
        public GameObject attack_center; //For projectiles mostly, since most objects have their pivot directly on the floor, we sometimes dont want projectiles to aim at the pivot but at this position instead

        public GameObject hp_bar;
        public GameObject hit_fx; //Prefab spawned then hit
        public GameObject death_fx; //Prefab spawned then dying
        public AudioClip hit_sound;
        public AudioClip death_sound;

        //Events
        public UnityAction<Destructible> onDamagedBy;
        public UnityAction<PlayerCharacter> onDamagedByPlayer;
        public UnityAction onDamaged;
        public UnityAction onDeath;

        private bool hit_by_player = false;
        private bool dead = false;

        private Collider[] colliders;
        private UniqueID unique_id;
        private SNetworkActions actions;

        private Vector3 shake_center;
        private Vector3 shake_vector = Vector3.zero;
        private bool is_shaking = false;
        private float shake_timer = 0f;
        private float shake_intensity = 1f;
        private int max_hp;
        private float hp_regen_val;
        private HPBar hbar = null;

        private static HashSet<Destructible> active_list = new HashSet<Destructible>();

        protected override void Awake()
        {
            base.Awake();

            shake_center = transform.position;
            unique_id = GetComponent<UniqueID>();
            colliders = GetComponentsInChildren<Collider>();
            max_hp = hp;
        }

        protected void OnDestroy()
        {
            active_list.Remove(this);
        }

        protected override void OnSpawn()
        {
            actions = new SNetworkActions(this);
            actions.RegisterInt(ActionType.Death, DoDeath);
            active_list.Add(this);
        }

        protected override void OnDespawn()
        {
            base.OnDespawn();
            actions.Clear();
            active_list.Remove(this);
        }

        protected override void OnReady()
        {
            base.OnReady();

            if (!unique_id.WasCreated && WorldData.Get().IsObjectRemoved(GetUID()))
            {
                NetObject.Destroy();
                return;
            }

            if (HasUID() && WorldData.Get().HasCustomInt(GetHpUID()))
            {
                hp = WorldData.Get().GetCustomInt(GetHpUID());
            }
        }

        protected void Update()
        {
            //Shake FX
            if (is_shaking)
            {
                shake_timer -= Time.deltaTime;

                if (shake_timer > 0f)
                {
                    shake_vector = new Vector3(Mathf.Cos(shake_timer * Mathf.PI * 16f) * 0.02f, 0f, Mathf.Sin(shake_timer * Mathf.PI * 8f) * 0.01f);
                    transform.position += shake_vector * shake_intensity;
                }
                else if (shake_timer > -0.5f)
                {
                    transform.position = Vector3.Lerp(transform.position, shake_center, 4f * Time.deltaTime);
                }
                else
                {
                    is_shaking = false;
                }
            }

            //Spawn HP bar
            if (hp > 0 && hp < max_hp && hbar == null && hp_bar != null)
            {
                GameObject hp_obj = Instantiate(hp_bar, transform);
                hbar = hp_obj.GetComponent<HPBar>();
                hbar.target = this;
            }

            //Regen HP
            if (!dead && hp_regen > 0.01f && hp < max_hp)
            {
                float game_speed = TheGame.Get().GetGameTimeSpeedPerSec();
                hp_regen_val += game_speed * hp_regen * Time.deltaTime;
                if (hp_regen_val >= 1f)
                {
                    hp_regen_val -= 1f;
                    hp += 1;
                }
            }
        }

        //Deal damage from character
        public override void TakeDamage(Destructible attacker, int damage)
        {
            if (!dead)
            {
                ApplyDamage(damage);

                onDamagedBy?.Invoke(attacker);

                if (hp <= 0)
                    Kill();
            }
        }

        //Deal damage from player
        public override void TakeDamage(PlayerCharacter attacker, int damage)
        {
            if (!dead)
            {
                ApplyDamage(damage);

                hit_by_player = true;
                onDamagedByPlayer?.Invoke(attacker);

                if (hp <= 0)
                    Kill();
            }
        }

        //Take damage from no sources (like trap)
        public override void TakeDamage(int damage)
        {
            if (!dead)
            {
                ApplyDamage(damage);

                if (hp <= 0)
                    Kill();
            }
        }

        //Deal damages to the destructible, if it reaches 0 HP it will be killed
        private void ApplyDamage(int damage)
        {
            if (!dead)
            {
                int adamage = Mathf.Max(damage - armor, 1);
                hp -= adamage;

                WorldData.Get().SetCustomInt(GetHpUID(), hp);

                if (shake_on_hit)
                    ShakeFX();

                if (Selectable.IsActive() && Selectable.IsNearCamera(20f))
                {
                    if (hit_fx != null)
                        Instantiate(hit_fx, transform.position, Quaternion.identity);

                    TheAudio.Get().PlaySFX3D("destruct", hit_sound, transform.position);
                }

                onDamaged?.Invoke();
            }
        }

        public void Heal(int value)
        {
            if (!dead)
            {
                hp += value;
                hp = Mathf.Min(hp, max_hp);

                WorldData.Get().SetCustomInt(GetHpUID(), hp);
            }
        }

        //Kill the destructible
        public override void Kill()
        {
            if(IsServer && !dead)
                actions.Trigger(ActionType.Death, 1);  // DoDeath(1)
        }

        public void KillNoLoot()
        {
            if (IsServer && !dead)
                actions.Trigger(ActionType.Death, 0); // DoDeath(0)
        }

        private void DoDeath(int loot)
        {
            if (loot > 0)
                DoKill();
            else
                DoKillNoLoot();
        }

        private void DoKill()
        {
            if (!dead)
            {
                SpawnLoots();
                GiveXPLoot();
                DropStorage();
                KillFX();
                DoKillNoLoot();
            }
        }

        private void DoKillNoLoot()
        {
            if (!dead)
            {
                dead = true;
                hp = 0;

                foreach (Collider collide in colliders)
                    collide.enabled = false;

                if (!unique_id.WasCreated)
                    WorldData.Get().RemoveObject(GetUID()); //Remove object if it was in initial scene

                WorldData.Get().RemoveSpawnedObject(GetUID()); //Remove object if it was spawned
                WorldData.Get().RemoveCustomInt(GetHpUID()); //Remove HP custom value

                if (onDeath != null)
                    onDeath.Invoke();

                unique_id.RemoveAllSubUIDs();
                Selectable.Destroy(destroy_delay);
            }
        }

        private void KillFX()
        {
            //FX
            if (Selectable.IsActive() && Selectable.IsNearCamera(20f))
            {
                if (death_fx != null)
                    Instantiate(death_fx, transform.position, Quaternion.identity);

                TheAudio.Get().PlaySFX3D("destruct", death_sound, transform.position);
            }
        }

        private void GiveXPLoot()
        {
            if (!hit_by_player)
                return; //Don't give any XP if didn't receive at least 1 hit from player

            foreach (PlayerCharacter player in PlayerCharacter.GetAll())
            {
                float range = (player.transform.position - transform.position).magnitude;
                if(range < 20f) //20 could be added as parameter
                    player.Attributes.GainXP(xp_type, xp);
            }
        }

        private void DropStorage()
        {
            //Drop storage items
            InventoryData sdata = InventoryData.Get(InventoryType.Storage, GetUID());
            if (sdata != null)
            {
                foreach (KeyValuePair<int, InventoryItemData> item in sdata.items)
                {
                    ItemData idata = ItemData.Get(item.Value.item_id);
                    if (idata != null && item.Value.quantity > 0)
                    {
                        Item.Create(idata, GetLootRandomPos(), item.Value.quantity, item.Value.durability, item.Value.uid);
                    }
                }
            }
        }

        public void SpawnLoots()
        {
            foreach (SData item in loots)
            {
                SpawnLoot(item);
            }
        }

        private void SpawnLoot(SData item, int quantity=1)
        {
            if (item == null || quantity <= 0)
                return;

            Vector3 pos = GetLootRandomPos();
            if (item is ItemData)
            {
                ItemData aitem = (ItemData)item;
                Item.Create(aitem, pos, quantity);
            }
            if (item is ConstructionData)
            {
                ConstructionData construct_data = (ConstructionData)item;
                Construction.Create(construct_data, pos);
            }
            if (item is PlantData)
            {
                PlantData plant_data = (PlantData)item;
                Plant.Create(plant_data, pos, 0);
            }
            if (item is SpawnData)
            {
                SpawnData spawn_data = (SpawnData)item;
                Spawnable.Create(spawn_data, pos);
            }
            if (item is LootData)
            {
                LootData loot = (LootData)item;
                if (Random.value <= loot.probability)
                {
                    SpawnLoot(loot.item, loot.quantity);
                }
            }
        }

        private Vector3 GetLootRandomPos()
        {
            float radius = Random.Range(0.5f, 1f);
            float angle = Random.Range(0f, 360f) * Mathf.Rad2Deg;
            return transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
        }

        //Delayed kill (useful if the attacking character doing an animation before destroying this)
        public void KillIn(float delay)
        {
            StartCoroutine(KillInRun(delay));
        }

        private IEnumerator KillInRun(float delay)
        {
            yield return new WaitForSeconds(delay);
            Kill();
        }

        public void Reset()
        {
            hit_by_player = false;
            dead = false;
            hp = max_hp;

            foreach (Collider collide in colliders)
                collide.enabled = true;
        }

        public void ShakeFX(float intensity = 1f, float duration = 0.2f)
        {
            is_shaking = true;
            shake_center = transform.position;
            shake_intensity = intensity;
            shake_timer = duration;
        }

        public bool HasUID()
        {
            return !string.IsNullOrEmpty(unique_id.unique_id);
        }

        public string GetUID()
        {
            return unique_id.unique_id;
        }

        public string GetHpUID()
        {
            if (HasUID())
                return unique_id.unique_id + "_hp";
            return "";
        }

        public override bool IsDead()
        {
            return dead;
        }

        public override Vector3 GetCenter()
        {
            if (attack_center != null)
                return attack_center.transform.position;
            return transform.position + Vector3.up * 0.1f; //Bit higher than floor
        }

        public override bool CanBeAttacked()
        {
            return target_team != AttackTeam.CantAttack && !dead;
        }

        public override bool CanBeAttackedRanged()
        {
            return CanBeAttacked() && !attack_melee_only;
        }

        public override bool CanBeAttacked(PlayerCharacter character)
        {
            return CanBeAttacked()
                && (required_item != null || target_team != AttackTeam.Ally) //Cant attack allied unless has required item
                && (required_item == null || character.EquipData.HasItemInGroup(required_item)); //Cannot attack unless has equipped item
        }

        public override bool CanBeAutoAttacked(PlayerCharacter character)
        {
            bool has_required_item =  required_item != null && character.EquipData.HasItemInGroup(required_item);
            return CanBeAttacked(character) && (has_required_item || target_team == AttackTeam.Enemy || !Selectable.CanAutoInteract());
        }

        public override float GetHitRange()
        {
            return hit_range;
        }

        public int GetMaxHP()
        {
            return max_hp;
        }

        //Get nearest auto attack for player
        public static Destructible GetNearestAutoAttack(PlayerCharacter character, Vector3 pos, float range = 999f)
        {
            Destructible nearest = null;
            float min_dist = range;
            foreach (Destructible destruct in active_list) //Loop on active selectables only for optimization
            {
                if (destruct != null && character.Combat.CanAutoAttack(destruct))
                {
                    float dist = (destruct.transform.position - pos).magnitude;
                    if (dist < min_dist)
                    {
                        min_dist = dist;
                        nearest = destruct;
                    }
                }
            }
            return nearest;
        }

        //Get nearest destructible of attack team
        public static Destructible GetNearestAttack(AttackTeam team, Vector3 pos, float range = 999f)
        {
            Destructible nearest = null;
            float min_dist = range;
            foreach (Selectable selectable in Selectable.GetAllActive()) //Loop on active selectables only for optimization
            {
                Destructible destruct = selectable.Destructible;
                if (destruct != null && selectable.IsActive() && destruct.CanBeAttacked() && destruct.target_team == team)
                {
                    float dist = (destruct.transform.position - pos).magnitude;
                    if (dist < min_dist)
                    {
                        min_dist = dist;
                        nearest = destruct;
                    }
                }
            }
            return nearest;
        }

        //Get nearest active destructible selectable
        public static Destructible GetNearest(Vector3 pos, float range = 999f)
        {
            Destructible nearest = null;
            float min_dist = range;
            foreach (Destructible destruct in active_list) //Loop on active selectables only for optimization
            {
                if (destruct != null)
                {
                    float dist = (destruct.transform.position - pos).magnitude;
                    if (dist < min_dist)
                    {
                        min_dist = dist;
                        nearest = destruct;
                    }
                }
            }
            return nearest;
        }

        public static HashSet<Destructible> GetAllActive()
        {
            return active_list;
        }
    }

}