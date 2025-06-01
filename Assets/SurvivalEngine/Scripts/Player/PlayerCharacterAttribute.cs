using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using NetcodePlus;
using Unity.Netcode;

namespace SurvivalEngine
{
    [RequireComponent(typeof(PlayerCharacter))]
    public class PlayerCharacterAttribute : SNetworkBehaviour
    {
        [Header("Attributes")]
        public AttributeData[] attributes;

        public UnityAction onGainLevel;

        private PlayerCharacter character;

        private float move_speed_mult = 1f;
        private float attack_mult = 1f;
        private bool depleting = false;

        [Header("Refresh rate")]
        public float sync_refresh_rate = 0.5f;

        private SNetworkActions actions;
        private float refresh_timer = 0f;

        protected override void Awake()
        {
            base.Awake();

            character = GetComponent<PlayerCharacter>();

        }

        protected override void OnReady()
        {
            //Init attributes
            foreach (AttributeData attr in attributes)
            {
                if (!CharacterData.HasAttribute(attr.type))
                    CharacterData.SetAttributeValue(attr.type, attr.start_value, GetAttributeMax(attr.type));
            }
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();
            actions = new SNetworkActions(this);
            actions.RegisterRefresh(RefreshType.Player, OnRefreshPlayer, NetworkDelivery.ReliableFragmentedSequenced);
            actions.RegisterRefresh(RefreshType.Attributes, OnRefreshAttributes, NetworkDelivery.Unreliable);
            actions.RegisterRefresh(RefreshType.Levels, OnRefreshLevels, NetworkDelivery.Reliable);
        }

        protected override void OnDespawn()
        {
            base.OnDespawn();
            actions.Clear();
        }

        protected void Update()
        {
            if (TheGame.Get().IsPaused())
                return;

            if (!IsSpawned)
                return;

            if (character.IsDead())
                return;

            //Update attributes
            float game_speed = TheGame.Get().GetGameTimeSpeedPerSec();

            //Update Attributes
            foreach (AttributeData attr in attributes)
            {
                float update_value = attr.value_per_hour + GetBonusEffectTotal(BonusEffectData.GetAttributeBonusType(attr.type));
                update_value = update_value * game_speed * Time.deltaTime;
                CharacterData.AddAttributeValue(attr.type, update_value, GetAttributeMax(attr.type));
            }

            //Penalty for depleted attributes
            move_speed_mult = 1f;
            attack_mult = 1f;
            depleting = false;

            foreach (AttributeData attr in attributes)
            {
                if (GetAttributeValue(attr.type) < 0.01f)
                {
                    move_speed_mult = move_speed_mult * attr.deplete_move_mult;
                    attack_mult = attack_mult * attr.deplete_attack_mult;
                    float update_value = attr.deplete_hp_loss * game_speed * Time.deltaTime;
                    AddAttribute(AttributeType.Health, update_value);
                    if (attr.deplete_hp_loss < 0f)
                        depleting = true;
                }
            }

            //Dying
            float health = GetAttributeValue(AttributeType.Health);
            if (health < 0.01f)
                character.Kill();

            //Sleeps add attributes
            if (character.IsSleeping())
            {
                ActionSleep sleep_target = character.GetSleepTarget();
                AddAttribute(AttributeType.Health, sleep_target.sleep_hp_hour * game_speed * Time.deltaTime);
                AddAttribute(AttributeType.Hunger, sleep_target.sleep_hunger_hour * game_speed * Time.deltaTime);
                AddAttribute(AttributeType.Thirst, sleep_target.sleep_thirst_hour * game_speed * Time.deltaTime);
                AddAttribute(AttributeType.Happiness, sleep_target.sleep_hapiness_hour * game_speed * Time.deltaTime);
            }

            UpdateRefresh();
            //SyncOwner();
        }

        private void UpdateRefresh()
        {
            refresh_timer += Time.deltaTime;
            if (refresh_timer > sync_refresh_rate)
            {
                refresh_timer = 0f;
                CheckRefresh();
            }
        }

        //For optimization, only refresh the data that is changed
        private void CheckRefresh()
        {
            if (CharacterData.IsFullRefresh())
            {
                CharacterData.ClearRefresh();
                RefreshPlayer(); //Refresh the whole player data
            }
            else if (CharacterData.IsLevelRefresh())
            {
                CharacterData.ClearRefresh();
                RefreshLevels(); //Refresh the levels (xp...)
                RefreshAttributes(); //Also refresh attributes
            }
            else
            {
                RefreshAttributes(); //Only refresh attributes (fastest)
            }
        }

        //Call to refresh ALL PlayerData
        public void RefreshPlayer()
        {
            //Its better to serialize the save file data with NetworkTool,
            //otherwise with INetworkSerializable it may cause combatibility issues when loading a save from older version
            byte[] bytes = NetworkTool.Serialize(CharacterData); 
            actions?.Refresh(RefreshType.Player, bytes); // OnRefreshData(SData)
        }

        private void OnRefreshPlayer(byte[] bytes)
        {
            PlayerData pdata = NetworkTool.Deserialize<PlayerData>(bytes);
            if (pdata != null)
                WorldData.Get().OverridePlayer(pdata.player_id, pdata);
        }

        //Refresh only attributes, bonus effect, and gold
        public void RefreshAttributes()
        {
            if (actions != null)
            {
                RefreshPlayerAttributes attr = RefreshPlayerAttributes.Get(CharacterData);
                actions.Refresh(RefreshType.Attributes, attr); // OnRefreshAttributes(attr)
            }
        }

        private void OnRefreshAttributes(SerializedData sdata)
        {
            RefreshPlayerAttributes attr = sdata.Get<RefreshPlayerAttributes>();
            PlayerData pdata = PlayerData.Get(attr.player_id);
            if(pdata != null)
                pdata.OverrideAttributes(attr);
        }

        //Refresh only levels and xp
        public void RefreshLevels()
        {
            if (actions != null)
            {
                RefreshPlayerLevels lvls = RefreshPlayerLevels.Get(CharacterData);
                actions.Refresh(RefreshType.Levels, lvls); // OnRefreshLevels(attr)
            }
        }

        private void OnRefreshLevels(SerializedData sdata)
        {
            RefreshPlayerLevels lvls = sdata.Get<RefreshPlayerLevels>();
            PlayerData pdata = PlayerData.Get(lvls.player_id);
            if (pdata != null)
                pdata?.OverrideLevels(lvls);
        }

        public void AddAttribute(AttributeType type, float value)
        {
            if(HasAttribute(type))
                CharacterData.AddAttributeValue(type, value, GetAttributeMax(type));
        }

        public void SetAttribute(AttributeType type, float value)
        {
            if (HasAttribute(type))
                CharacterData.SetAttributeValue(type, value, GetAttributeMax(type));
        }

        public void ResetAttribute(AttributeType type)
        {
            AttributeData adata = GetAttribute(type);
            if (adata != null)
                CharacterData.SetAttributeValue(type, adata.start_value, GetAttributeMax(type));
        }

        public void KillAttributes()
        {
            foreach (AttributeData attr in attributes)
                CharacterData.SetAttributeValue(attr.type, 0f, 0f);
        }

        public float GetAttributeValue(AttributeType type)
        {
            return CharacterData.GetAttributeValue(type);
        }

        public float GetAttributeMax(AttributeType type)
        {
            AttributeData adata = GetAttribute(type);
            if (adata != null)
                return adata.max_value + GetBonusEffectTotal(BonusEffectData.GetAttributeMaxBonusType(type));
            return 100f;
        }

        public AttributeData GetAttribute(AttributeType type)
        {
            foreach (AttributeData attr in attributes)
            {
                if (attr.type == type)
                    return attr;
            }
            return null;
        }

        public bool HasAttribute(AttributeType type)
        {
            return GetAttribute(type) != null;
        }

        //-- Xp / Levels --

        public void GainXP(string id, int xp)
        {
            if (xp > 0)
            {
                CharacterData.GainXP(id, xp);
                CheckLevel(id);
            }
        }

        private void CheckLevel(string id)
        {
            PlayerLevelData ldata = CharacterData.GetLevelData(id);
            LevelData current = LevelData.GetLevel(id, ldata.level);
            LevelData next = LevelData.GetLevel(id, ldata.level + 1);
            if (current != null && next != null && current != next && ldata.xp >= next.xp_required)
            {
                GainLevel(id);
                CheckLevel(id); //Check again if it increased by 2+ levels
            }
        }

        public void GainLevel(string id)
        {
            CharacterData.GainLevel(id);

            int alevel = CharacterData.GetLevel(id);
            LevelData level = LevelData.GetLevel(id, alevel);
            if (level != null)
            {
                foreach (CraftData unlock in level.unlock_craft)
                    character.Crafting.LearnCraft(unlock.id);
            }

            onGainLevel?.Invoke();
        }

        public int GetLevel(string id)
        {
            return CharacterData.GetLevel(id);
        }

        public int GetXP(string id)
        {
            return CharacterData.GetXP(id);
        }

        public float GetBonusEffectTotal(BonusType type, GroupData[] targets = null)
        {
            float value = GetBonusEffectTotalSingle(type, null);
            if (targets != null)
            {
                foreach (GroupData target in targets)
                    value += GetBonusEffectTotalSingle(type, target);
            }
            return value;
        }

        public float GetBonusEffectTotalSingle(BonusType type, GroupData target)
        {
            float value = 0f;

            //Level bonus
            value += CharacterData.GetLevelBonusValue(type, target);

            //Equip bonus
            foreach (KeyValuePair<int, InventoryItemData> pair in character.EquipData.items)
            {
                ItemData idata = ItemData.Get(pair.Value?.item_id);
                if (idata != null)
                {
                    foreach (BonusEffectData bonus in idata.equip_bonus)
                    {
                        if (bonus.type == type && bonus.target == target)
                            value += bonus.value;
                    }
                }
            }

            //Aura bonus
            foreach (BonusAura aura in BonusAura.GetAll())
            {
                float dist = (aura.transform.position - transform.position).magnitude;
                if (aura.effect.type == type && aura.effect.target == target && dist < aura.range)
                    value += aura.effect.value;
            }

            //Timed bonus
            if (target == null)
                value += CharacterData.GetTotalTimedBonus(type);

            return value;
        }

        public float GetSpeedMult()
        {
            return Mathf.Max(move_speed_mult, 0.01f);
        }

        public float GetAttackMult()
        {
            return Mathf.Max(attack_mult, 0.01f);
        }

        public bool IsDepletingHP()
        {
            return depleting;
        }

        public PlayerData CharacterData
        {
            get { return character.SaveData; }
        }

        public PlayerCharacter GetCharacter()
        {
            return character;
        }
    }
}
