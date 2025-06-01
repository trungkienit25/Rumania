using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NetcodePlus
{
    //Just a monobehaviour that also has the OnReady function
    //Inherit only on scripts WITHOUT a SNetworkObject, if it has, use SNetworkBehaviour instead
    //OnReady will trigger after the Start function, once, after connection is established and all initialization network data has been transfered
    //Use OnReady instead of Start if you want to make sure your are using the synchronized server data instead of uninitialized local data

    public class SMonoBehaviour : MonoBehaviour
    {
        protected virtual void Awake()
        {
            TheNetwork network = TheNetwork.Get();
            network.onReady += OnReady;
            network.onBeforeReady += OnBeforeReady;
            network.onAfterReady += OnAfterReady;
            network.onClientReady += OnClientReady;
        }

        protected virtual void OnDestroy()
        {
            TheNetwork network = TheNetwork.GetIfExist();
            if (network != null)
            {
                network.onReady -= OnReady;
                network.onBeforeReady -= OnBeforeReady;
                network.onAfterReady -= OnAfterReady;
                network.onClientReady -= OnClientReady;
            }
        }

        protected virtual void OnReady()
        {
            //Override this
        }

        protected virtual void OnBeforeReady()
        {
            //Override this
        }

        protected virtual void OnAfterReady()
        {
            //Override this
        }

        protected virtual void OnClientReady(ulong client_id)
        {
            //Override this
        }
    }
}
