using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

//using GooglePlayGames;
//using GooglePlayGames.BasicApi;

namespace NetcodePlus
{
    /// <summary>
    /// This code has not been tested and is just an implementation example
    /// Some additional integration may be required
    /// you would need to setup Google Play Games SDK (https://github.com/playgameservices/play-games-plugin-for-unity/tree/master/current-build) 
    /// and uncomment the code related to Google Play Games
    /// More info here: (https://docs.unity.com/authentication/SettingupGooglePlayGamesSignin.html)
    /// </summary>

    public class AuthenticatorGoogle : AuthenticatorUnity
    {
        protected string token = null;
        protected string error = null;

        private void LoginGooglePlayGames()
        {
            /*PlayGamesPlatform.Instance.Authenticate((success) =>
            {
                if (success == SignInStatus.Success)
                {
                    Debug.Log("Login with Google Play games successful.");

                    PlayGamesPlatform.Instance.RequestServerSideAccess(true, code =>
                    {
                        Debug.Log("Google Authorization code: " + code);
                        token = code;
                        // This token serves as an example to be used for SignInWithGooglePlayGames
                    });
                }
                else
                {
                    error = "Failed to retrieve Google play games authorization code";
                    Debug.Log("Google Login Unsuccessful");
                }
            });*/
        }

        private async Task WaitForConnect()
        {
            while (token == null && error == null)
            {
                await TimeTool.Delay(100);
            }
        }

        public override async Task Initialize()
        {
            await base.Initialize();
            //PlayGamesPlatform.Activate();
            await Task.Yield();
        }

        public override async Task<bool> Login()
        {
            if (IsConnectedOnline())
                return true; //Already connected

            token = error = null;
            LoginGooglePlayGames();

            await WaitForConnect();

            try
            {
                if (token != null)
                {
                    await AuthenticationService.Instance.SignInWithGoogleAsync(token);
                    user_id = AuthenticationService.Instance.PlayerId;
                    if (username == null)
                        username = user_id;
                    logged_in = true;
                    test_login = false;
                    return true;
                }
            }
            catch (AuthenticationException ex) { Debug.LogException(ex); }
            catch (RequestFailedException ex) { Debug.LogException(ex); }
            return false;
        }

        public override string GetUsername()
        {
            return Social.localUser.userName;
        }

        public override string GetProviderId()
        {
            return Social.localUser.id;
        }
    }
}
