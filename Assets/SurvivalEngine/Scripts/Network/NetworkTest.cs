using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NetcodePlus;

namespace SurvivalEngine
{
    //Run right after TheNetwork but before gameplay scripts
    [DefaultExecutionOrder(-9)]
    public class NetworkTest : MonoBehaviour
    {
        void Start()
        {
            if (!TheNetwork.Get().IsActive())
            {
                //Start in test mode, when running directly from Unity Scene
                Authenticator.Get().LoginTest("Player");
                PlayerData.NewOrLoad(Authenticator.Get().UserID, Authenticator.Get().Username);
                TheNetwork.Get().StartHost(NetworkData.Get().game_port);
            }
        }
    }
}
