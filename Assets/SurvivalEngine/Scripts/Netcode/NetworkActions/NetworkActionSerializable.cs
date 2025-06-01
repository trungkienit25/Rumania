using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;
using Unity.Collections;

namespace NetcodePlus
{

    public class NetworkActionSerializable : NetworkAction
    {
        public UnityAction<SerializedData> callback;

        private static Pool<NetworkActionSerializable> pool = new Pool<NetworkActionSerializable>();

        public void TriggerAction<T>(SNetworkActions handler, T data) where T : INetworkSerializable
        {
            if (handler == null || data == null)
                return;

            SendToTarget(handler, data);

            if (ShouldRun())
                RunAction(data);
        }

        public void RunAction(SerializedData data)
        {
            callback?.Invoke(data);
        }

        public void RunAction<T>(T data) where T : INetworkSerializable
        {
            SerializedData sdata = new SerializedData(data);
            RunAction(sdata);
        }

        public override void RunAction(FastBufferReader reader)
        {
            SerializedData sdata = new SerializedData(reader);
            RunAction(sdata);
        }

        public override void Dispose()
        {
            pool.Dispose(this);
        }

        public static void Clear()
        {
            pool.Clear();
        }

        public static NetworkActionSerializable Create()
        {
            return pool.Create();
        }

    }

    //Object passed as parameter when using RegisterSerializable() or RegisterRefresh(), use Get<>() to read the data
    public struct SerializedData
    {
        private FastBufferReader reader;
        private INetworkSerializable data;

        public SerializedData(FastBufferReader r) { reader = r; data = null; }
        public SerializedData(INetworkSerializable d) { data = d; }

        public T Get<T>() where T : INetworkSerializable, new()
        {
            if (data != null)
            {
                return (T)data;
            }
            else
            {
                reader.ReadNetworkSerializable(out T val);
                data = val;
                return val;
            }
        }
    }
}
