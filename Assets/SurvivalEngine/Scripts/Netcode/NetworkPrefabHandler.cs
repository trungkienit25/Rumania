using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace NetcodePlus
{

    public class NetworkPrefabHandler : INetworkPrefabInstanceHandler
    {
        private GameObject prefab;

        public NetworkPrefabHandler(GameObject prefab)
        {
            this.prefab = prefab;
        }

        public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
        {
            GameObject obj = GameObject.Instantiate(prefab, position, rotation);
            return obj.GetComponent<NetworkObject>();
        }

        public void Destroy(NetworkObject networkObject)
        {
            GameObject.Destroy(networkObject.gameObject);
        }
    }
}