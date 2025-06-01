using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;

namespace NetcodePlus
{
   
    public class NetworkActionBehaviour : NetworkAction
    {
        public UnityAction<SNetworkBehaviour> callback;

        private static Pool<NetworkActionBehaviour> pool = new Pool<NetworkActionBehaviour>();

        public SNetworkBehaviourData GetData(SNetworkBehaviour nobj)
        {
            SNetworkBehaviourData data = new SNetworkBehaviourData();
            data.nobj = new SNetworkBehaviourRef(nobj);
            return data;
        }

        public void TriggerAction(SNetworkActions handler, SNetworkBehaviour nobj)
        {
            if (handler == null)
                return;

            if(nobj != null && !nobj.IsSpawned)
                return;

            SNetworkBehaviourData data = GetData(nobj);

            SendToTarget(handler, data);

            if (ShouldRun())
                RunAction(nobj);
        }

        public void RunAction(SNetworkBehaviour nobj)
        {
            callback?.Invoke(nobj);
        }

        public override void RunAction(FastBufferReader reader)
        {
            SNetworkBehaviourData data;
            reader.ReadNetworkSerializable(out data);
            SNetworkBehaviour obj = data.nobj.Get();
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

        public static NetworkActionBehaviour Create()
        {
            return pool.Create();
        }

        public struct SNetworkBehaviourData : INetworkSerializable
        {
            public SNetworkBehaviourRef nobj;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref nobj);
            }
        }
    }

}
