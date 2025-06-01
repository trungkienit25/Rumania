using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace NetcodePlus
{
    /// <summary>
    /// This authenticator is the base auth for Unity Services
    /// It will login in anonymous mode
    /// It is ideal for quick testing since it will skip login UI and create a temporary user.
    /// </summary>

    public class AuthenticatorUnity : Authenticator
    {
        public override async Task Initialize()
        {
            if(UnityServices.State == ServicesInitializationState.Uninitialized)
                await UnityServices.InitializeAsync();
            inited = true; //Set this to true only after finish initializing
        }

        public override async Task<bool> Login()
        {
            if (NetworkData.Get().auth_auto_logout && !IsConnected())
                AuthenticationService.Instance.ClearSessionToken();

            if (IsConnectedOnline())
                return true; //Already connected

            try
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                user_id = AuthenticationService.Instance.PlayerId;
                if (username == null)
                    username = user_id;
                Debug.Log("Unity Auth: " + user_id + " " + username);
                logged_in = true;
                test_login = false;
                return true;
            }
            catch (AuthenticationException ex) { Debug.LogException(ex); }
            catch (RequestFailedException ex) { Debug.LogException(ex); }
            return false;
        }

        public override async Task<bool> Login(string username)
        {
            this.username = username;
            return await Login();
        }

        public override void Logout()
        {
            try
            {
                AuthenticationService.Instance.SignOut(true);
                user_id = null;
                username = null;
                logged_in = false;
                test_login = false;
            }
            catch (System.Exception) { }
        }

        public override bool IsConnectedOnline()
        {
            return inited && AuthenticationService.Instance.IsAuthorized;
        }

        public override bool IsConnected()
        {
            if (test_login)
                return true; //Test loggin
            return inited && AuthenticationService.Instance.IsAuthorized;
        }

        public override bool IsSignedIn()
        {
            if (test_login)
                return true; //Test loggin
            return inited && AuthenticationService.Instance.IsSignedIn;
        }

        public override bool IsExpired()
        {
            if (test_login)
                return false; //Test loggin
            return inited && AuthenticationService.Instance.IsExpired;
        }

        public override bool IsUnityServices()
        {
            return true;
        }

        public override string GetUsername()
        {
            return username;
        }

        public override string GetUserId()
        {
            return user_id;
        }
    }
}
