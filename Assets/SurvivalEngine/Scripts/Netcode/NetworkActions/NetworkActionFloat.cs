using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;

namespace NetcodePlus
{
    public class NetworkActionFloat : NetworkAction
    {
        public UnityAction<float> callback;

        private static Pool<NetworkActionFloat> pool = new Pool<NetworkActionFloat>();

        public NetworkActionFloatData GetData(float value)
        {
            NetworkActionFloatData data = new NetworkActionFloatData();
            data.value = value;
            return data;
        }

        public void TriggerAction(SNetworkActions handler, float value)
        {
            if (handler == null)
                return;

            NetworkActionFloatData data = GetData(value);

            SendToTarget(handler, data);

            if (ShouldRun())
                RunAction(value);
        }

        public void RunAction(float value)
        {
            callback?.Invoke(value);
        }

        public override void RunAction(FastBufferReader reader)
        {
            NetworkActionFloatData data;
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

        public static NetworkActionFloat Create()
        {
            return pool.Create();
        }

        public struct NetworkActionFloatData : INetworkSerializable
        {
            public float value;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref value);
            }
        }
    }
}
