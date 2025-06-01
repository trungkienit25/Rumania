using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.Events;

namespace NetcodePlus
{
    /// <summary>
    /// Improved version of NetworkBehaviour
    /// </summary>

    public abstract class SNetworkBehaviour : MonoBehaviour
    {
        private SNetworkObject nobj;        //If your script inherits from SNetworkBehaviour, it should have a SNetworkObject component on it
        private ushort behaviour_id;        //ID of the behaviour
        private byte[] extra = new byte[0];     //Extra data transfered from server to client when spawned

        protected virtual void Awake()
        {
            nobj = GetComponentInParent<SNetworkObject>();
            if (nobj != null)
            {
                nobj.onReady += OnReady;
                nobj.onBeforeSpawn += OnBeforeSpawn;
                nobj.onSpawn += OnSpawn;
                nobj.onDespawn += OnDespawn;
            }
            else
            {
                Debug.LogError(gameObject.name + " should have a SNetworkObject component if a script inherits from SNetworkBehaviour");
            }
        }

        protected virtual void OnReady()
        {
            //Function will run after connection was fully established (and all data loaded), unlike Spawn() this function will run only once
        }

        protected virtual void OnBeforeSpawn()
        {
            //Function will run before spawning (server only)
        }

        protected virtual void OnSpawn()
        {
            //Function will run after spawned
        }

        protected virtual void OnDespawn()
        {
            //Function will run before despawned
        }

        public void SetBehaviourId(ushort id)
        {
            behaviour_id = id;
        }

        public void SetSpawnData(byte[] data)
        {
            extra = data;
        }

        public byte[] GetSpawnData()
        {
            return extra;
        }

        public void SetSpawnData(int data)
        {
            extra = NetworkTool.SerializeInt32(data);
        }

        public int GetSpawnDataInt32()
        {
            return NetworkTool.DeserializeInt32(extra);
        }

        public void SetSpawnData(ulong data)
        {
            extra = NetworkTool.SerializeUInt64(data);
        }

        public ulong GetSpawnDataUInt64()
        {
            return NetworkTool.DeserializeUInt64(extra);
        }

        public void SetSpawnData(string data)
        {
            extra = NetworkTool.SerializeString(data);
        }

        public string GetSpawnDataString()
        {
            return NetworkTool.DeserializeString(extra);
        }

        //Extra data transfered from server to client when spawned
        public void SetSpawnData<T>(T data) where T : INetworkSerializable, new()
        {
            extra = NetworkTool.NetSerialize(data);
        }

        public T GetSpawnData<T>() where T : INetworkSerializable, new()
        {
            return NetworkTool.NetDeserialize<T>(extra);
        }

        public T Get<T>() where T : SNetworkBehaviour
        {
            if (this is T)
                return (T)this;
            return null;
        }

        public SNetworkObject NetObject { get { return nobj; } }

        public ulong NetworkId { get { return nobj != null ? nobj.NetworkId : 0; } }
        public ulong OwnerId { get { return nobj != null ? nobj.OwnerId : 0; } }
        public ushort BehaviourId { get { return behaviour_id; } }

        public bool IsServer { get { return nobj != null ? nobj.IsServer : false; } }
        public bool IsClient { get { return nobj != null ? nobj.IsClient : false; } }
        public bool IsOwner { get { return nobj != null ? nobj.IsOwner : false; } }

        public bool IsSpawned { get { return nobj != null ? nobj.IsSpawned : false; } }
        public bool IsReady { get { return nobj != null ? nobj.IsReady : false; } }
    }

    [System.Serializable]
    public struct SNetworkBehaviourRef : INetworkSerializable
    {
        public ulong net_id;
        public ushort behaviour_id;

        public SNetworkBehaviourRef(SNetworkBehaviour behaviour)
        {
            if (behaviour != null)
            {
                net_id = behaviour.NetworkId;
                behaviour_id = behaviour.BehaviourId;
            }
            else
            {
                net_id = 0;
                behaviour_id = 0;
            }
        }

        public SNetworkBehaviour Get()
        {
            if (net_id == 0)
                return null;
            return NetworkSpawner.Get().GetSpawnedBehaviour(net_id, behaviour_id);
        }

        public T Get<T>() where T : SNetworkBehaviour
        {
            if (net_id == 0)
                return null;
            SNetworkBehaviour sb = NetworkSpawner.Get().GetSpawnedBehaviour(net_id, behaviour_id);
            if (sb is T)
                return (T)sb;
            return null;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref net_id);
            serializer.SerializeValue(ref behaviour_id);
        }
    }
}
