using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;

namespace NetcodePlus
{
    public class NetworkActionVector : NetworkAction
    {
        public UnityAction<Vector3> callback;

        private static Pool<NetworkActionVector> pool = new Pool<NetworkActionVector>();

        public NetworkActionVectorData GetData(Vector3 pos)
        {
            NetworkActionVectorData data = new NetworkActionVectorData();
            data.pos = pos;
            return data;
        }

        public void TriggerAction(SNetworkActions handler, Vector3 pos)
        {
            if (handler == null)
                return;

            NetworkActionVectorData data = GetData(pos);

            SendToTarget(handler, data);

            if (ShouldRun())
                RunAction(pos);
        }

        public void RunAction(Vector3 pos)
        {
            callback?.Invoke(pos);
        }

        public override void RunAction(FastBufferReader reader)
        {
            NetworkActionVectorData data;
            reader.ReadNetworkSerializable(out data);
            RunAction(data.pos);
        }

        public override void Dispose()
        {
            pool.Dispose(this);
        }

        public static void Clear()
        {
            pool.Clear();
        }

        public static NetworkActionVector Create()
        {
            return pool.Create();
        }

        public struct NetworkActionVectorData : INetworkSerializable
        {
            public Vector3 pos;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref pos);
            }
        }
    }
}
