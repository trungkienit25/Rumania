using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;

namespace NetcodePlus
{
   
    public class NetworkActionObject : NetworkAction
    {
        public UnityAction<SNetworkObject> callback;

        private static Pool<NetworkActionObject> pool = new Pool<NetworkActionObject>();

        public NetworkActionObjectData GetData(SNetworkObject nobj)
        {
            NetworkActionObjectData data = new NetworkActionObjectData();
            data.nobj = new SNetworkObjectRef(nobj);
            return data;
        }

        public void TriggerAction(SNetworkActions handler, SNetworkObject nobj)
        {
            if (handler == null)
                return;

            if(nobj != null && !nobj.IsSpawned)
                return;

            NetworkActionObjectData data = GetData(nobj);

            SendToTarget(handler, data);

            if (ShouldRun())
                RunAction(nobj);
        }

        public void RunAction(SNetworkObject nobj)
        {
            callback?.Invoke(nobj);
        }

        public override void RunAction(FastBufferReader reader)
        {
            NetworkActionObjectData data;
            reader.ReadNetworkSerializable(out data);
            SNetworkObject obj = data.nobj.Get();
            RunAction(obj);
        }

        public override void Dispose()
        {
            pool.Dispose(this);
        }

        public static void Clear()
        {
            pool.Clear();
        }

        public static NetworkActionObject Create()
        {
            return pool.Create();
        }
        
        public struct NetworkActionObjectData : INetworkSerializable
        {
            public SNetworkObjectRef nobj;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref nobj);
            }
        }
    }

}
