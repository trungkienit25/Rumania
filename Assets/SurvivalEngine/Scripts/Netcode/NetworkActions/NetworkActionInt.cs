using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;

namespace NetcodePlus
{
    public class NetworkActionInt : NetworkAction
    {
        public UnityAction<int> callback;

        private static Pool<NetworkActionInt> pool = new Pool<NetworkActionInt>();

        public NetworkActionIntData GetData(int value)
        {
            NetworkActionIntData data = new NetworkActionIntData();
            data.value = value;
            return data;
        }

        public void TriggerAction(SNetworkActions handler, int value)
        {
            if (handler == null)
                return;

            NetworkActionIntData data = GetData(value);

            SendToTarget(handler, data);

            if (ShouldRun())
                RunAction(value);
        }

        public void RunAction(int value)
        {
            callback?.Invoke(value);
        }

        public override void RunAction(FastBufferReader reader)
        {
            NetworkActionIntData data;
            reader.ReadNetworkSerializable(out data);
            RunAction(data.value);
        }

        public override void Dispose()
        {
            pool.Dispose(this);
        }

        public static void Clear()
        {
            pool.Clear();
        }

        public static NetworkActionInt Create()
        {
            return pool.Create();
        }

        public struct NetworkActionIntData : INetworkSerializable
        {
            public int value;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref value);
            }
        }
    }
}
