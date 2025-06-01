using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using NetcodePlus;

namespace SurvivalEngine
{
    public class ActionType
    {
        public const ushort None = 0;
        public const ushort SyncObject = 5;
        public const ushort SyncRequest = 7;

        public const ushort Click = 10;
        public const ushort ClickRight = 11;
        public const ushort ClickHold = 12;
        public const ushort ClickRelease = 13;

        public const ushort Build = 20;
        public const ushort BuildItem = 21;
        public const ushort Craft = 22;
        public const ushort Take = 23;
        public const ushort Drop = 24;
        public const ushort Eat = 25;
        public const ushort Use = 26;
        public const ushort Equip = 27;
        public const ushort UnEquip = 28;
        public const ushort MoveItem = 29;

        public const ushort Attack = 30;
        public const ushort AttackTarget = 31;
        public const ushort AttackTargetPos = 32;
        public const ushort Jump = 32;
        public const ushort Climb = 33;
        public const ushort Death = 35;
        public const ushort Revive = 37;

        public const ushort ActionTarget = 40;
        public const ushort ActionSlot = 41;
        public const ushort ActionMergeTarget = 42;
        public const ushort ActionMergeSlot = 43;
        public const ushort ActionSelect = 45;

        public const ushort OrderMove = 50;
        public const ushort OrderAttack = 51;
        public const ushort OrderEscape = 52;
        public const ushort OrderFollow = 53;
        public const ushort OrderStart = 55;
        public const ushort OrderStop = 56;

        public const ushort Buy = 60;
        public const ushort Sell = 61;

        public const ushort Teleport = 70;
        public const ushort Transition = 72;
        public const ushort TransitionScene = 74;
    }

    /// <summary>
    /// List of serializable data
    /// </summary>

    public struct NetworkActionClickData : INetworkSerializable
    {
        public SNetworkBehaviourRef selectable;
        public Vector3 pos;

        public NetworkActionClickData(Selectable select, Vector3 p)
        {
            selectable = new SNetworkBehaviourRef(select);
            pos = p;
        }

        public Selectable GetSelectable()
        {
            return selectable.Get<Selectable>();
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref selectable);
            serializer.SerializeValue(ref pos);
        }
    }

    public struct NetworkActionInventoryData : INetworkSerializable
    {
        public InventoryType type;
        public string uid;
        public int owner;
        public int slot;
        public int quantity;
        public Vector3 pos;

        public NetworkActionInventoryData(InventoryData inventory, int slt)
        {
            type = inventory.type;
            uid = inventory.uid;
            owner = inventory.owner;
            slot = slt;
            quantity = 1;
            pos = Vector3.zero;
        }

        public NetworkActionInventoryData(InventoryData inventory, int slt, int quant)
        {
            type = inventory.type;
            uid = inventory.uid;
            owner = inventory.owner;
            slot = slt;
            quantity = quant;
            pos = Vector3.zero;
        }

        public NetworkActionInventoryData(InventoryData inventory, int slt, Vector3 p)
        {
            type = inventory.type;
            uid = inventory.uid;
            owner = inventory.owner;
            slot = slt;
            quantity = 1;
            pos = p;
        }

        public InventorySlot GetInventorySlot()
        {
            InventoryData inv = InventoryData.Get(type, uid, owner);
            InventorySlot islot = new InventorySlot(inv, slot);
            return islot;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref type);
            serializer.SerializeValue(ref uid);
            serializer.SerializeValue(ref owner);
            serializer.SerializeValue(ref slot);
            serializer.SerializeValue(ref quantity);
            serializer.SerializeValue(ref pos);
        }
    }

    public struct NetworkActionInventoryMoveData : INetworkSerializable
    {
        public InventoryType type1;
        public string uid1;
        public int owner1;
        public int slot1;
        public InventoryType type2;
        public string uid2;
        public int owner2;
        public int slot2;

        public NetworkActionInventoryMoveData(InventoryData inv1, int slt1, InventoryData inv2, int slt2)
        {
            type1 = inv1.type;
            uid1 = inv1.uid;
            owner1 = inv1.owner;
            slot1 = slt1;
            type2 = inv2.type;
            uid2 = inv2.uid;
            owner2 = inv2.owner;
            slot2 = slt2;
        }

        public NetworkActionInventoryMoveData(InventorySlot islot1, InventorySlot islot2)
        {
            type1 = islot1.inventory.type;
            uid1 = islot1.inventory.uid;
            owner1 = islot1.inventory.owner;
            slot1 = islot1.slot;
            type2 = islot2.inventory.type;
            uid2 = islot2.inventory.uid;
            owner2 = islot2.inventory.owner;
            slot2 = islot2.slot;
        }

        public InventorySlot GetInventorySlot1()
        {
            InventoryData inv = InventoryData.Get(type1, uid1, owner1);
            InventorySlot islot = new InventorySlot(inv, slot1);
            return islot;
        }

        public InventorySlot GetInventorySlot2()
        {
            InventoryData inv = InventoryData.Get(type2, uid2, owner2);
            InventorySlot islot = new InventorySlot(inv, slot2);
            return islot;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref type1);
            serializer.SerializeValue(ref uid1);
            serializer.SerializeValue(ref owner1);
            serializer.SerializeValue(ref slot1);
            serializer.SerializeValue(ref type2);
            serializer.SerializeValue(ref uid2);
            serializer.SerializeValue(ref owner2);
            serializer.SerializeValue(ref slot2);
        }
    }

    public struct NetworkActionCraftData : INetworkSerializable
    {
        public string craft_id;

        public NetworkActionCraftData(CraftData cdata)
        {
            craft_id = cdata.id;
        }

        public CraftData GetData()
        {
            return CraftData.Get(craft_id);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref craft_id);
        }
    }

    public struct NetworkActionBuildData : INetworkSerializable
    {
        public string craft_id;
        public Vector3 pos;
        public Vector3 rot;

        public InventoryType type;
        public string uid;
        public int owner;
        public int slot;

        public NetworkActionBuildData(CraftData cdata)
        {
            craft_id = cdata.id;
            type = InventoryType.None;
            uid = "";
            owner = 0;
            slot = 0;
            pos = Vector3.zero;
            rot = Vector3.zero;
        }

        public NetworkActionBuildData(CraftData cdata, Vector3 ps, Vector3 ang)
        {
            craft_id = cdata.id;
            type = InventoryType.None;
            uid = "";
            owner = 0;
            slot = 0;
            pos = ps;
            rot = ang;
        }


        public NetworkActionBuildData(CraftData cdata, InventorySlot islot)
        {
            craft_id = cdata.id;
            type = islot != null ? islot.inventory.type : InventoryType.None;
            uid = islot != null ? islot.inventory.uid : "";
            owner = islot != null ? islot.inventory.owner : 0;
            slot = islot != null ? islot.slot : 0;
            pos = Vector3.zero;
            rot = Vector3.zero;
        }

        public NetworkActionBuildData(CraftData cdata, InventorySlot islot, Vector3 ps, Vector3 ang)
        {
            craft_id = cdata.id;
            type = islot != null ? islot.inventory.type : InventoryType.None;
            uid = islot != null ? islot.inventory.uid : "";
            owner = islot != null ? islot.inventory.owner : 0;
            slot = islot != null ? islot.slot : 0;
            pos = ps;
            rot = ang;
        }

        public CraftData GetData()
        {
            return CraftData.Get(craft_id);
        }

        public InventorySlot GetInventorySlot()
        {
            if (type != InventoryType.None)
            {
                InventoryData inv = InventoryData.Get(type, uid, owner);
                InventorySlot islot = new InventorySlot(inv, slot);
                return islot;
            }
            return null;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref craft_id);
            serializer.SerializeValue(ref pos);
            serializer.SerializeValue(ref rot);
            serializer.SerializeValue(ref type);
            serializer.SerializeValue(ref uid);
            serializer.SerializeValue(ref owner);
            serializer.SerializeValue(ref slot);
        }
    }

    public struct NetworkActionSActionData : INetworkSerializable
    {
        public int action;
        public SNetworkBehaviourRef selectable;
        public InventoryType inventory_type;
        public string inventory_uid;
        public int inventory_owner;
        public int slot;

        public NetworkActionSActionData(SAction saction, Selectable target)
        {
            selectable = new SNetworkBehaviourRef(target);
            action = target.FindActionIndex(saction);
            inventory_type = InventoryType.None;
            inventory_uid = "";
            inventory_owner = -1;
            slot = 0;
        }

        public NetworkActionSActionData(SAction saction, InventorySlot islot)
        {
            ItemData idata = islot.inventory.GetItem(islot.slot);
            action = idata != null ? idata.FindActionIndex(saction) : 0;
            selectable = new SNetworkBehaviourRef(null);
            inventory_type = islot.inventory.type;
            inventory_uid = islot.inventory.uid;
            inventory_owner = islot.inventory.owner;
            slot = islot.slot;
        }

        public Selectable GetSelectable()
        {
            return selectable.Get<Selectable>();
        }

        public InventorySlot GetInventorySlot()
        {
            InventoryData inv = InventoryData.Get(inventory_type, inventory_uid, inventory_owner);
            InventorySlot islot = new InventorySlot(inv, slot);
            return islot;
        }

        public ItemData GetItem()
        {
            InventoryData inv = InventoryData.Get(inventory_type, inventory_uid, inventory_owner);
            return inv?.GetItem(slot);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref selectable);
            serializer.SerializeValue(ref inventory_type);
            serializer.SerializeValue(ref inventory_uid);
            serializer.SerializeValue(ref inventory_owner);
            serializer.SerializeValue(ref slot);
            serializer.SerializeValue(ref action);
        }
    }

    public struct NetworkActionMActionData : INetworkSerializable
    {
        public int action;
        public SNetworkBehaviourRef selectable;
        public InventoryType inventory1_type;
        public string inventory1_uid;
        public int inventory1_owner;
        public int slot1;
        public InventoryType inventory2_type;
        public string inventory2_uid;
        public int inventory2_owner;
        public int slot2;

        public NetworkActionMActionData(SAction saction, InventorySlot islot, Selectable target)
        {
            ItemData idata = islot.inventory.GetItem(islot.slot);
            action = idata != null ? idata.FindActionIndex(saction) : 0;
            selectable = new SNetworkBehaviourRef(target);
            inventory1_type = islot.inventory.type;
            inventory1_uid = islot.inventory.uid;
            inventory1_owner = islot.inventory.owner;
            slot1 = islot.slot;
            inventory2_type = InventoryType.None;
            inventory2_uid = "";
            inventory2_owner = -1;
            slot2 = 0;
        }

        public NetworkActionMActionData(SAction saction, InventorySlot islot1, InventorySlot islot2)
        {
            ItemData idata = islot1.inventory.GetItem(islot1.slot);
            action = idata != null ? idata.FindActionIndex(saction) : 0;
            selectable = new SNetworkBehaviourRef(null);
            inventory1_type = islot1.inventory.type;
            inventory1_uid = islot1.inventory.uid;
            inventory1_owner = islot1.inventory.owner;
            slot1 = islot1.slot;
            inventory2_type = islot2.inventory.type;
            inventory2_uid = islot2.inventory.uid;
            inventory2_owner = islot2.inventory.owner;
            slot2 = islot2.slot;
        }

        public Selectable GetSelectable()
        {
            return selectable.Get<Selectable>();
        }

        public InventorySlot GetInventorySlot1()
        {
            InventoryData inv = InventoryData.Get(inventory1_type, inventory1_uid, inventory1_owner);
            InventorySlot islot = new InventorySlot(inv, slot1);
            return islot;
        }

        public InventorySlot GetInventorySlot2()
        {
            InventoryData inv = InventoryData.Get(inventory2_type, inventory2_uid, inventory2_owner);
            InventorySlot islot = new InventorySlot(inv, slot2);
            return islot;
        }

        public ItemData GetItem1()
        {
            InventoryData inv = InventoryData.Get(inventory1_type, inventory1_uid, inventory1_owner);
            return inv?.GetItem(slot1);
        }

        public ItemData GetItem2()
        {
            InventoryData inv = InventoryData.Get(inventory2_type, inventory2_uid, inventory2_owner);
            return inv?.GetItem(slot2);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref action);
            serializer.SerializeValue(ref selectable);
            serializer.SerializeValue(ref inventory1_type);
            serializer.SerializeValue(ref inventory1_uid);
            serializer.SerializeValue(ref inventory1_owner);
            serializer.SerializeValue(ref slot1);
            serializer.SerializeValue(ref inventory2_type);
            serializer.SerializeValue(ref inventory2_uid);
            serializer.SerializeValue(ref inventory2_owner);
            serializer.SerializeValue(ref slot2);
        }
    }

    public class NetworkActionReviveData : INetworkSerializable
    {
        public Vector3 pos;
        public float percent;

        public NetworkActionReviveData() { }
        public NetworkActionReviveData(Vector3 p, float perc) { pos = p; percent = perc; }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref pos);
            serializer.SerializeValue(ref percent);
        }
    }
}
