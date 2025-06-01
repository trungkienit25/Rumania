using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;
using Unity.Collections;

namespace NetcodePlus
{
   
    public class NetworkActionBytes : NetworkAction
    {
        public UnityAction<byte[]> callback;

        private static Pool<NetworkActionBytes> pool = new Pool<NetworkActionBytes>();

        public NetworkActionBytesData GetData(byte[] bytes)
        {
            NetworkActionBytesData data = new NetworkActionBytesData();
            data.data = bytes;
            return data;
        }

        public void TriggerAction(SNetworkActions handler, byte[] data)
        {
            if (handler == null || data == null)
                return;

            NetworkActionBytesData bdata = GetData(data);
            SendToTarget(handler, bdata);

            if (ShouldRun())
                RunAction(data);
        }

        public void RunAction(byte[] data)
        {
            callback?.Invoke(data);
        }

        public override void RunAction(FastBufferReader reader)
        {
            NetworkActionBytesData bdata;
            reader.ReadNetworkSerializable(out bdata);
            RunAction(bdata.data);
        }

        public override void Dispose()
        {
            pool.Dispose(this);
        }

        public static void Clear()
        {
            pool.Clear();
        }

        public static NetworkActionBytes Create()
        {
            return pool.Create();
        }
    }

    public struct NetworkActionBytesData : INetworkSerializable
    {
        public byte[] data;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref data);
        }
    }
}
