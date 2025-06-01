using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

//using Steamworks;
//using Steamworks.Data;

namespace NetcodePlus
{
    /// <summary>
    /// This code has not been tested and is just an implementation example
    /// Some additional integration may be required
    /// you would need to setup Steam SDK (https://wiki.facepunch.com/steamworks) and uncomment the code related to steam
    /// More info here: https://docs.unity.com/authentication/SettingupSteamSignin.html 
    /// I recommend using facepunch instead of steamworks.net, it's a lot better
    /// </summary>

    public class AuthenticatorSteam : AuthenticatorUnity
    {
        public const uint steam_id = 420;  //Replace with your steam ID

        protected string session_ticket;
        protected string identity;

        private async Task SignInWithSteam()
        {
            //AuthTicket ticket = await SteamUser.GetAuthSessionTicketAsync();
            //session_ticket = System.BitConverter.ToString(ticket.Data).Replace("-", string.Empty);
            //identity = ???
            await Task.Yield();
        }

        public override async Task Initialize()
        {
            await base.Initialize();
            //SteamClient.Init(steam_app_id);
            await Task.Yield();
        }

        public override async Task<bool> Login()
        {
            if (IsConnectedOnline())
                return true; //Already connected

            await SignInWithSteam();

            try
            {
                await AuthenticationService.Instance.SignInWithSteamAsync(session_ticket, identity);
                user_id = AuthenticationService.Instance.PlayerId;
                if (username == null)
                    username = user_id;
                logged_in = true;
                test_login = false;
                return true;
            }
            catch (AuthenticationException ex) { Debug.LogException(ex); }
            catch (RequestFailedException ex) { Debug.LogException(ex); }
            return false;
        }

        public override bool IsSignedIn()
        {
            bool valid = true;
            //valid = SteamClient.IsValid && SteamClient.IsLoggedOn;
            return AuthenticationService.Instance.IsSignedIn && valid;
        }

        public override string GetProviderId()
        {
            return user_id; //Comment this line
            //SteamId sid = SteamClient.SteamId;
            //return sid.Value.ToString();
        }
    }
}
