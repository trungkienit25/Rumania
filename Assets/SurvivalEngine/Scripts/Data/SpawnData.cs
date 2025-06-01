using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NetcodePlus;
using Unity.Netcode;

namespace SurvivalEngine 
{
    /// <summary>
    /// SData is the base scriptable object data for this engine
    /// </summary>
    [System.Serializable]
    public abstract class SData : ScriptableObject { }

    /// <summary>
    /// IdData adds an ID to the class
    /// </summary>
    [System.Serializable]
    public abstract class IdData : SData { public string id; }

    /// <summary>
    /// This is a generic spawn data to spawn any generic prefabs that are not Constructions, Items, Plants or Characters
    /// Spawn() is called automatically during loading to respawn everything that was saved, use Create() to create a new object
    /// </summary>

    [CreateAssetMenu(fileName = "SpawnData", menuName = "SurvivalEngine/SpawnData", order = 5)]
    public class SpawnData : IdData
    {
        public GameObject prefab;

        private static List<SpawnData> spawn_data = new List<SpawnData>();
        private static bool loaded = false;

        public static void Load(string folder = "")
        {
            if (!loaded)
            {
                loaded = true;
                spawn_data.AddRange(Resources.LoadAll<SpawnData>(folder));

                foreach (SpawnData data in spawn_data)
                {
                    TheNetwork.Get().RegisterPrefab(data.prefab);
                }
            }
        }

        public static SpawnData Get(string id)
        {
            foreach (SpawnData data in spawn_data)
            {
                if (data.id == id)
                    return data;
            }
            return null;
        }

        public static List<SpawnData> GetAll()
        {
            return spawn_data;
        }
    }

    [System.Serializable]
    public struct SpawnDataRef : INetworkSerializable
    {
        public string id;

        public SpawnDataRef(SpawnData data)
        {
            if (data != null)
                id = data.id;
            else
                id = "";
        }

        public SpawnData Get()
        {
            return SpawnData.Get(id);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref id);
        }
    }
}


