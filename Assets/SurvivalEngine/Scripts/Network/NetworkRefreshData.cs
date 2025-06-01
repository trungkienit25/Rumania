using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using NetcodePlus;

namespace SurvivalEngine
{

    public class RefreshType
    {
        public const ushort None = 0;
        public const ushort All = 100; //IDs start at 100 to avoid ushort conflict with NetworkActionType
        public const ushort RefreshObject = 105;

        public const ushort GameTime = 110;
        public const ushort Player = 112;
        public const ushort Inventory = 114;
        public const ushort Attributes = 116;
        public const ushort Levels = 118;

        public const ushort CustomInt = 130;
        public const ushort CustomFloat = 131;
        public const ushort CustomString = 132;
        public const ushort ObjectRemoved = 133;

    }

    /// <summary>
    /// List of serializable data
    /// </summary>
    /// 

    [System.Serializable]
    public struct RefreshPlayerAttributes : INetworkSerializable
    {
        public int player_id;
        public int gold;
        public Dictionary<AttributeType, float> attributes;
        public Dictionary<BonusType, TimedBonusData> timed_bonus_effects;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref player_id);
            serializer.SerializeValue(ref gold);
            NetworkTool.SerializeDictionaryEnum(serializer, ref attributes);
            NetworkTool.SerializeDictionaryEnumObject(serializer, ref timed_bonus_effects);
        }

        public static RefreshPlayerAttributes Get(PlayerData pdata)
        {
            RefreshPlayerAttributes attr = new RefreshPlayerAttributes();
            attr.player_id = pdata.player_id;
            attr.gold = pdata.gold;
            attr.attributes = pdata.attributes;
            attr.timed_bonus_effects = pdata.timed_bonus_effects;
            return attr;
        }
    }

    [System.Serializable]
    public struct RefreshPlayerLevels : INetworkSerializable
    {
        public int player_id;
        public Dictionary<string, PlayerLevelData> levels;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref player_id);
            NetworkTool.SerializeDictionaryObject(serializer, ref levels);
        }

        public static RefreshPlayerLevels Get(PlayerData pdata)
        {
            RefreshPlayerLevels attr = new RefreshPlayerLevels();
            attr.player_id = pdata.player_id;
            attr.levels = pdata.levels;
            return attr;
        }
    }

    [System.Serializable]
    public struct RefreshGameTime : INetworkSerializable
    {
        public int day;
        public float day_time;

        public RefreshGameTime(int d, float dt) { day = d; day_time = dt; }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref day);
            serializer.SerializeValue(ref day_time);
        }
    }

    [System.Serializable]
    public struct RefreshCustomInt : INetworkSerializable
    {
        public string uid;
        public int value;

        public RefreshCustomInt(string id, int val) { uid = id; value = val; }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref uid);
            serializer.SerializeValue(ref value);
        }
    }

    [System.Serializable]
    public struct RefreshCustomFloat : INetworkSerializable
    {
        public string uid;
        public float value;

        public RefreshCustomFloat(string id, float val) { uid = id; value = val; }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref uid);
            serializer.SerializeValue(ref value);
        }
    }

    [System.Serializable]
    public struct RefreshCustomString : INetworkSerializable
    {
        public string uid;
        public string value;

        public RefreshCustomString(string id, string val) { uid = id; value = val; }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref uid);
            serializer.SerializeValue(ref value);
        }
    }
}
