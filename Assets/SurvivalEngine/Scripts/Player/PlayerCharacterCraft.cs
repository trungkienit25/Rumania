using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using NetcodePlus;
using Unity.Netcode;

namespace SurvivalEngine
{
    /// <summary>
    /// Class that manages the player character crafting and building
    /// </summary>

    [RequireComponent(typeof(PlayerCharacter))]
    public class PlayerCharacterCraft : SNetworkBehaviour
    {
        public float build_range = 0f;
        public GroupData[] craft_groups;

        public UnityAction<CraftData> onCraft;
        public UnityAction<Buildable> onBuild;

        private PlayerCharacter character;
        private SNetworkActions actions;

        private CraftData craft_data = null;
        private GameObject craft_progress = null;

        private bool is_building = false;
        private Vector3 build_position;
        private Vector3 build_rotation;
        private CraftData build_mode_data = null;
        private Buildable build_buildable = null;
        private InventorySlot build_pay_slot;

        private float build_timer = 0f;
        private float craft_timer = 0f;
        private bool clicked_build = false;

        protected override void Awake()
        {
            base.Awake();
            character = GetComponent<PlayerCharacter>();
        }

        protected void Start()
        {
            PlayerControlsMouse controls = PlayerControlsMouse.Get();
            controls.onRightClick += (Vector3 pos, Selectable select) => CancelBuilding();
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();
            actions = new SNetworkActions(this);
            actions.RegisterSerializable(ActionType.Craft, DoCraft);
            actions.RegisterSerializable(ActionType.Build, DoBuild);
            actions.RegisterSerializable(ActionType.BuildItem, DoBuildItem);
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

            if (character.IsDead())
                return;

            build_timer += Time.deltaTime;
            craft_timer += Time.deltaTime;

            PlayerControls controls = PlayerControls.Get();

            //Cancel building
            if (controls.IsPressUICancel() || controls.IsPressPause())
                CancelBuilding();

            //Cancel crafting
            if (craft_data != null && character.IsMovingControls())
                CancelCrafting();

            //Complete crafting after timer
            if (craft_data != null)
            {
                if (craft_timer > craft_data.craft_duration)
                    CompleteCrafting();
            }
        }

        //---- Crafting cost and requirements ----

        public bool CanCraft(CraftData item, bool skip_cost = false, bool skip_near = false)
        {
            if (item == null || character.IsDead())
                return false;

            bool has_craft_cost = skip_cost || HasCraftCost(item);
            bool has_near = skip_near || HasCraftNear(item);
            return has_near && has_craft_cost;
        }

        public bool CanCraft(CraftData item, InventorySlot pay_slot, bool skip_near = false)
        {
            if (item == null || character.IsDead())
                return false;

            bool has_craft_cost = HasCraftCost(item);
            bool has_pay_slot = HasCraftPaySlot(item, pay_slot);
            bool has_near = skip_near || HasCraftNear(item);
            bool has_cost = has_craft_cost || has_pay_slot;
            return has_near && has_cost;
        }

        public bool HasCraftPaySlot(CraftData item, InventorySlot pay_slot)
        {
            if (pay_slot != null)
            {
                ItemData idata = pay_slot.GetItem();
                return idata != null && idata.build_data == item;
            }
            return false;
        }

        public bool HasCraftCost(CraftData item)
        {
            bool can_craft = true;
            CraftCostData cost = item.GetCraftCost();
            Dictionary<GroupData, int> item_groups = new Dictionary<GroupData, int>(); //Add to groups so that fillers are not same than items

            foreach (KeyValuePair<ItemData, int> pair in cost.craft_items)
            {
                AddCraftCostItemsGroups(item_groups, pair.Key, pair.Value);
                if (!character.Inventory.HasItem(pair.Key, pair.Value))
                    can_craft = false; //Dont have required items
            }

            foreach (KeyValuePair<GroupData, int> pair in cost.craft_fillers)
            {
                int value = pair.Value + CountCraftCostGroup(item_groups, pair.Key);
                if (!character.Inventory.HasItemInGroup(pair.Key, value))
                    can_craft = false; //Dont have required items
            }

            foreach (KeyValuePair<CraftData, int> pair in cost.craft_requirements)
            {
                if (CountRequirements(pair.Key) < pair.Value)
                    can_craft = false; //Dont have required constructions
            }
            return can_craft;
        }

        public bool HasCraftNear(CraftData item)
        {
            bool can_craft = true;
            CraftCostData cost = item.GetCraftCost();
            if (cost.craft_near != null && !character.IsNearGroup(cost.craft_near) && !character.EquipData.HasItemInGroup(cost.craft_near))
                can_craft = false; //Not near required construction
            return can_craft;
        }

        private void AddCraftCostItemsGroups(Dictionary<GroupData, int> item_groups, ItemData item, int quantity)
        {
            foreach (GroupData group in item.groups)
            {
                if (item_groups.ContainsKey(group))
                    item_groups[group] += quantity;
                else
                    item_groups[group] = quantity;
            }
        }

        private int CountCraftCostGroup(Dictionary<GroupData, int> item_groups, GroupData group)
        {
            if (item_groups.ContainsKey(group))
                return item_groups[group];
            return 0;
        }

        public void PayCraftingCost(CraftData item)
        {
            CraftCostData cost = item.GetCraftCost();
            foreach (KeyValuePair<ItemData, int> pair in cost.craft_items)
            {
                character.Inventory.UseItem(pair.Key, pair.Value);
            }
            foreach (KeyValuePair<GroupData, int> pair in cost.craft_fillers)
            {
                character.Inventory.UseItemInGroup(pair.Key, pair.Value);
            }
        }

        public void PayCraftingCost(CraftData item, InventorySlot pay_slot)
        {
            if (pay_slot != null && pay_slot.inventory != null)
            {
                InventoryData inventory = pay_slot.inventory;
                inventory.RemoveItemAt(pay_slot.slot, 1);
                character.Inventory.Refresh(inventory);
            }
            else
            {
                PayCraftingCost(item);
            }
        }

        public int CountRequirements(CraftData requirement)
        {
            if (requirement is ItemData)
                return character.Inventory.CountItem((ItemData)requirement);
            else
                return SObject.CountSceneObjects(requirement);
        }

        //----Networked Craft Actions ----

        public void StartCraftingOrBuilding(CraftData data)
        {
            if (CanCraft(data))
            {
                if (data is ItemData)
                    StartCrafting(data);
                else
                    CraftBuildMode(data);

                if(character.IsSelf())
                    TheAudio.Get().PlaySFX("craft", data.craft_sound);
            }
        }

        //Start crafting with timer
        public void StartCrafting(CraftData data)
        {
            if (data != null && craft_data == null && IsOwner && !character.IsDead())
            {
                NetworkActionCraftData ndata = new NetworkActionCraftData(data);
                actions.Trigger(ActionType.Craft, ndata); // DoCraft(ndata)
            }
        }

        //After reached build position, start crafting duration / anim
        public void StartCraftBuilding()
        {
            if (build_buildable != null)
            {
                //If in range, use current position, otherwise build directly in front of the character
                bool in_range = build_buildable.IsInRange(character);
                Vector3 bpos = in_range ? build_buildable.transform.position : build_buildable.GetBuildFrontPos(character);
                StartCraftBuilding(bpos);
            }
        }

        public void StartCraftBuilding(Vector3 pos)
        {
            if (IsOwner && build_mode_data != null && build_buildable != null && craft_data == null)
            {
                if (CanCraft(build_mode_data, build_pay_slot, true))
                {
                    Vector3 angles = build_buildable.transform.rotation.eulerAngles;
                    NetworkActionBuildData ndata = new NetworkActionBuildData(build_mode_data, build_pay_slot, pos, angles);
                    actions.Trigger(ActionType.Build, ndata); // DoBuild(ndata)
                }
            }
        }

        private void DoCraft(SerializedData sdata)
        {
            NetworkActionCraftData ndata = sdata.Get<NetworkActionCraftData>();
            CraftData data = ndata.GetData();
            DoCraft(data);
        }

        private void DoCraft(CraftData data, bool is_build = false)
        {
            if (data != null && craft_data == null)
            {
                craft_data = data;
                craft_timer = 0f;
                is_building = is_build;
                character.StopMove();

                if (AssetData.Get().action_progress != null && data.craft_duration > 0.1f)
                {
                    craft_progress = Instantiate(AssetData.Get().action_progress, transform);
                    craft_progress.GetComponent<ActionProgress>().duration = data.craft_duration;
                }

                if (data.craft_duration < 0.01f)
                   CompleteCrafting();
            }
        }

        private void DoBuild(SerializedData sdata)
        {
            NetworkActionBuildData ndata = sdata.Get<NetworkActionBuildData>();
            CraftData data = ndata.GetData();
            Vector3 pos = ndata.pos;
            Vector3 rot = ndata.rot;

            if (craft_data == null)
            {
                InventorySlot pay_slot = ndata.GetInventorySlot();
                if (CanCraft(data, pay_slot, true))
                {
                    build_position = pos;
                    build_rotation = rot;
                    build_mode_data = null;
                    build_pay_slot = pay_slot;
                    SetBuildPos(pos, rot);
                    character.FaceTorward(pos);
                    DoCraft(data, true);
                }
            }
        }

        //------------ Build Items ----------

        public void BuildItemBuildMode(InventorySlot slot)
        {
            ItemData idata = slot.GetItem();
            if (idata != null && idata.build_data != null)
            {
                CraftBuildMode(idata.build_data);
                build_pay_slot = slot;
            }
        }

        public void BuildItem(InventorySlot islot)
        {
            ItemData idata = islot.GetItem();
            if (idata != null)
                BuildItem(idata.build_data, islot);
        }

        public void BuildItem(CraftData cdata, InventorySlot islot)
        {
            if (IsOwner && !character.IsDead())
            {
                ItemData idata = islot.GetItem();
                if (idata != null && idata.build_data == cdata)
                {
                    NetworkActionBuildData bidata = new NetworkActionBuildData(cdata, islot);
                    actions.Trigger(ActionType.BuildItem, bidata); // DoBuildItem(idata)
                }
            }
        }

        private void DoBuildItem(SerializedData sdata)
        {
            NetworkActionBuildData ndata = sdata.Get<NetworkActionBuildData>();
            InventorySlot islot = ndata.GetInventorySlot();
            InventoryData inventory = islot.inventory;
            InventoryItemData invdata = inventory?.GetInventoryItem(islot.slot);
            ItemData idata = ItemData.Get(invdata?.item_id);
            bool server = TheNetwork.Get().IsServer;

            if (server && invdata != null && idata != null && idata.build_data != null)
            {
                if (idata.build_data.id == ndata.craft_id && CanCraft(idata.build_data, true))
                {
                    inventory.RemoveItemAt(islot.slot, 1);
                    character.Inventory.Refresh(inventory);

                    Craftable craftable = CompleteCraftCraftable(idata.build_data, true);

                    if (craftable != null && craftable is Construction)
                    {
                        Construction construction = (Construction)craftable;
                        BuiltConstructionData constru = WorldData.Get().GetConstructed(construction.GetUID());
                        if (idata.HasDurability())
                            constru.durability = invdata.durability; //Save durability
                    }

                    if (craftable != null && craftable.Buildable != null)
                        TheAudio.Get().PlaySFX3D("craft", craftable.Buildable.build_audio, craftable.transform.position);
                }
            }
        }


        //----- Craft in Build mode -----

        public void CraftBuildMode(CraftData data)
        {
            if (data is PlantData)
                CraftPlantBuildMode((PlantData)data, 0);
            if (data is ConstructionData)
                CraftConstructionBuildMode((ConstructionData)data);
            if (data is CharacterData)
                CraftCharacterBuildMode((CharacterData)data);
        }

        public void CraftPlantBuildMode(PlantData plant, int stage)
        {
            CancelCrafting();

            Plant aplant = Plant.CreateBuildMode(plant, transform.position, stage);
            build_buildable = aplant.Buildable;
            build_buildable.StartBuild(character);
            build_mode_data = plant;
            build_pay_slot = null;
            clicked_build = false;
            build_timer = 0f;
        }

        public void CraftConstructionBuildMode(ConstructionData item)
        {
            CancelCrafting();

            Construction construction = Construction.CreateBuildMode(item, transform.position + transform.forward * 1f);
            build_buildable = construction.GetBuildable();
            build_buildable.StartBuild(character);
            build_mode_data = item;
            build_pay_slot = null;
            clicked_build = false;
            build_timer = 0f;
        }

        public void CraftCharacterBuildMode(CharacterData item)
        {
            CancelCrafting();

            Character acharacter = Character.CreateBuildMode(item, transform.position + transform.forward * 1f);
            build_buildable = acharacter.Buildable;
            if (build_buildable != null)
                build_buildable.StartBuild(character);
            build_mode_data = item;
            build_pay_slot = null;
            clicked_build = false;
            build_timer = 0f;
        }

        //----- Cancel and confirm -----

        public void CancelCrafting()
        {
            craft_data = null;
            is_building = false;
            if (craft_progress != null)
                Destroy(craft_progress);
            CancelBuilding();
        }

        public void CancelBuilding()
        {
            if (build_buildable != null)
            {
                Destroy(build_buildable.gameObject);
                build_buildable = null;
                build_mode_data = null;
                is_building = false;
                build_pay_slot = null;
                clicked_build = false;
            }
        }

        //Order to move to and build there
        public void BuildMoveAt(Vector3 pos)
        {
            bool in_range = character.interact_type == PlayerInteractBehavior.MoveAndInteract || IsInBuildRange();
            if (!in_range)
                return;

            if (!clicked_build && build_buildable != null)
            {
                build_buildable.SetBuildPositionTemporary(pos); //Set build position before checkifcanbuild

                bool can_build = build_buildable.CheckIfCanBuild();
                if (can_build)
                {
                    build_buildable.SetBuildPosition(pos);
                    clicked_build = true; //Give command to build
                    character.MoveTo(pos);
                }
            }
        }

        private void SetBuildPos(Vector3 pos, Vector3 rot)
        {
            if (build_buildable != null)
            {
                build_buildable.SetBuildPositionTemporary(pos, rot); //Set to position to test the condition, before applying it
                if (build_buildable.CheckIfCanBuild())
                {
                    build_buildable.SetBuildPosition(pos);
                }
            }
        }

        //----- Crafting Completion -----

        //End of the craft timer
        private void CompleteCrafting()
        {
            if (craft_data != null)
            {
                if (is_building)
                    CompleteBuilding(craft_data, build_pay_slot);
                else
                    CompleteCraftCraftable(craft_data);
                craft_data = null;
            }
        }

        //Craft immediately
        private Craftable CompleteCraftCraftable(CraftData data, bool skip_cost = false)
        {
            ItemData item = data.GetItem();
            ConstructionData construct = data.GetConstruction();
            PlantData plant = data.GetPlant();
            CharacterData character = data.GetCharacter();

            if (item != null)
                return CompleteCraftItem(item, skip_cost);
            else if (construct != null)
                return CompleteCraftConstruction(construct, skip_cost);
            else if (plant != null)
                return CompleteCraftPlant(plant, 0, skip_cost);
            else if (character != null)
                return CompleteCraftCharacter(character, skip_cost);
            return null;
        }

        public Item CompleteCraftItem(ItemData item, bool skip_cost = false)
        {
            if (!IsServer)
                return null;

            if (CanCraft(item, skip_cost))
            {
                if (!skip_cost)
                    PayCraftingCost(item);

                Item ritem = null;
                if (character.Inventory.CanTakeItem(item, item.craft_quantity))
                    character.Inventory.GainItem(item, item.craft_quantity);
                else
                    ritem = Item.Create(item, transform.position, item.craft_quantity);

                character.SaveData.AddCraftCount(item.id);
                character.Attributes.GainXP(item.craft_xp_type, item.craft_xp);

                if (onCraft != null)
                    onCraft.Invoke(item);

                return ritem;
            }
            return null;
        }

        public Character CompleteCraftCharacter(CharacterData character, bool skip_cost = false)
        {
            if (CanCraft(character, skip_cost))
            {
                if (!skip_cost)
                    PayCraftingCost(character);

                Vector3 pos = transform.position + transform.forward * 0.8f;
                Character acharacter = Character.Create(character, pos);

                this.character.SaveData.AddCraftCount(character.id);
                this.character.Attributes.GainXP(character.craft_xp_type, character.craft_xp);

                if (onCraft != null)
                    onCraft.Invoke(character);

                return acharacter;
            }
            return null;
        }

        public Plant CompleteCraftPlant(PlantData plant, int stage, bool skip_cost = false)
        {
            if (CanCraft(plant, skip_cost))
            {
                if (!skip_cost)
                    PayCraftingCost(plant);

                Vector3 pos = transform.position + transform.forward * 0.4f;
                Plant aplant = Plant.Create(plant, pos, stage);

                character.SaveData.AddCraftCount(plant.id);
                character.Attributes.GainXP(plant.craft_xp_type, plant.craft_xp);

                if (onCraft != null)
                    onCraft.Invoke(plant);

                return aplant;
            }
            return null;
        }

        public Construction CompleteCraftConstruction(ConstructionData construct, bool skip_cost = false)
        {
            if (CanCraft(construct, skip_cost))
            {
                if(!skip_cost)
                    PayCraftingCost(construct);

                Vector3 pos = transform.position + transform.forward * 1f;
                Construction aconstruct = Construction.Create(construct, pos);

                character.SaveData.AddCraftCount(construct.id);
                character.Attributes.GainXP(construct.craft_xp_type, construct.craft_xp);

                if (onCraft != null)
                    onCraft.Invoke(construct);

                return aconstruct;
            }
            return null;
        }

        private void CompleteBuilding(CraftData data, InventorySlot pay_slot = null)
        {
            bool can_pay = CanCraft(data, pay_slot, true);
            if (data != null && can_pay)
            {
                if (build_buildable == null)
                {
                    CraftBuildMode(data); //Action was sent from a client, create the construction
                    SetBuildPos(build_position, build_rotation);
                }

                if (build_buildable != null)
                {
                    Vector3 pos = build_buildable.transform.position;
                    if (build_buildable.CheckIfCanBuild())
                    {
                        character.FaceTorward(pos);

                        PayCraftingCost(data, pay_slot);

                        Buildable buildable = build_buildable;
                        buildable.FinishBuild();

                        character.SaveData.AddCraftCount(data.id);
                        character.Attributes.GainXP(data.craft_xp_type, data.craft_xp);

                        build_buildable = null;
                        craft_data = null;
                        clicked_build = false;
                        is_building = false;
                        character.StopAutoMove();

                        PlayerUI.Get(character.PlayerID)?.CancelSelection();
                        TheAudio.Get().PlaySFX3D("craft", buildable.build_audio, buildable.transform.position);

                        if (onBuild != null)
                            onBuild.Invoke(buildable);

                        character.TriggerBusy(1f);

                        if (!IsServer)
                            Destroy(buildable.gameObject); //Destroy temporary building
                    }
                }
            }
        }

        //---- Values and getters

        public void LearnCraft(string craft_id)
        {
            character.SaveData.UnlockID(craft_id);
        }

        public bool HasLearnt(string craft_id)
        {
            return character.SaveData.IsIDUnlocked(craft_id);
        }

        public int CountTotalCrafted(CraftData craftable)
        {
            if (craftable != null)
                return character.SaveData.GetCraftCount(craftable.id);
            return 0;
        }

        public void ResetCraftCount(CraftData craftable)
        {
            if (craftable != null)
                character.SaveData.ResetCraftCount(craftable.id);
        }

        public void ResetCraftCount()
        {
            character.SaveData.ResetCraftCount();
        }

        //Did it click to order to build
        public bool ClickedBuild()
        {
            return clicked_build;
        }

        public bool CanBuild()
        {
            return build_buildable != null && build_buildable.IsBuilding() && build_timer > 0.5f;
        }

        public bool IsInBuildRange()
        {
            if (build_buildable == null)
                return false;
            Vector3 dist = (character.GetInteractCenter() - build_buildable.transform.position);
            return dist.magnitude < build_buildable.GetBuildRange(character);
        }

        public bool IsBuildMode()
        {
            return build_buildable != null && build_buildable.IsBuilding();
        }

        public bool IsCrafting()
        {
            return craft_data != null;
        }

        public float GetCraftProgress()
        {
            if (craft_data != null && craft_data.craft_duration > 0.01f)
                return craft_timer / craft_data.craft_duration;
            return 0f;
        }

        public Buildable GetCurrentBuildable()
        {
            return build_buildable; //Can be null if not in build mode
        }

        public CraftData GetCurrentCrafting()
        {
            return craft_data;
        }

        public PlayerCharacter GetCharacter()
        {
            return character;
        }

        public CraftStation GetCraftStation()
        {
            CraftStation station = CraftStation.GetNearestInRange(transform.position);
            return station;
        }

        public List<GroupData> GetCraftGroups()
        {
            CraftStation station = CraftStation.GetNearestInRange(transform.position);
            if (station != null)
                return new List<GroupData>(station.craft_groups);
            else
                return new List<GroupData>(craft_groups);
        }
    }
}
