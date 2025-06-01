using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

namespace NetcodePlus
{

    public class NetworkChat
    {
        public UnityAction<string, string> onChat;

        public void Init()
        {
            TheNetwork network = TheNetwork.Get();
            network.Messaging.ListenMsg("chat", ReceiveChat);
        }

        public void Clear()
        {
            TheNetwork network = TheNetwork.GetIfExist();
            if (network != null)
            {
                network.Messaging.UnListenMsg("chat");
            }
        }

        public void SendChat(string msg)
        {
            string user = TheNetwork.Get().Username;
            NetChatMsg chat = new NetChatMsg(user, msg);

            if (IsServer)
                Messaging.SendObjectAll("chat", chat, NetworkDelivery.Reliable);
            else
                Messaging.SendObject("chat", ServerID, chat, NetworkDelivery.Reliable);

            onChat?.Invoke(user, msg); //Display chat
        }

        private void ReceiveChat(ulong client_id, FastBufferReader reader)
        {
            if (client_id != TheNetwork.Get().ClientID)
            {
                reader.ReadValueSafe(out NetChatMsg chat);
                if (chat != null)
                {
                    if (IsServer)
                    {
                        //Forward chat to all other clients
                        Messaging.ForwardAll("chat", client_id, reader, NetworkDelivery.Reliable);
                    }

                    //Display chat
                    onChat?.Invoke(chat.username, chat.text);
                }
            }
        }

        public bool IsOnline { get { return TheNetwork.Get().IsOnline; } }
        public bool IsServer { get { return TheNetwork.Get().IsServer; } }
        public ulong ServerID { get { return TheNetwork.Get().ServerID; } }
        public NetworkMessaging Messaging { get { return TheNetwork.Get().Messaging; } }

        public static NetworkChat Get()
        {
            return NetworkGame.Get().Chat;
        }
    }

    [System.Serializable]
    public class NetChatMsg : INetworkSerializable
    {
        public string username;
        public string text;

        public NetChatMsg() { }
        public NetChatMsg(string u, string t) { username = u; text = t; }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref username);
            serializer.SerializeValue(ref text);
        }
    }
}
