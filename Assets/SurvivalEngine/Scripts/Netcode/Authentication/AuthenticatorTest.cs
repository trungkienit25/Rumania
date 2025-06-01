using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace NetcodePlus
{
    /// <summary>
    /// Test authenticator just generates a random ID to use as user id
    /// This is very useful to test the game in multiplayer without needing to login each time
    /// Unity Services features won't work in test mode (Relay, Cloud Saves...)
    /// Use Anonymous mode to test those features (after connecting your project ID in services window)
    /// </summary>

    public class AuthenticatorTest : Authenticator
    {
        public override async Task Initialize()
        {
            await base.Initialize();
            await Login(); //Login with random username for testing purporse
        }

        public override async Task<bool> Login()
        {
            if (user_id == null)
                user_id = NetworkTool.GenerateRandomID(12, 15);
            username = user_id;
            logged_in = true;
            await Task.Yield(); //Do nothing
            return true;
        }

        public override async Task<bool> Login(string username)
        {
            this.user_id = username;  //User username as ID for save file consistency when testing
            this.username = username;
            logged_in = true;
            await Task.Yield(); //Do nothing
            return true;
        }

    }
}
