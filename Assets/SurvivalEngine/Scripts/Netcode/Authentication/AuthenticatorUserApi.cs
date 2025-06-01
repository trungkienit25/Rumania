using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

#if USER_LOGIN
using UserLogin;
#endif

namespace NetcodePlus
{
    /// <summary>
    /// This authenticator require external UserLogin API asset
    /// It works with an actual web API and database containing all user info
    /// </summary>

#if USER_LOGIN
    public class AuthenticatorUserApi : Authenticator
    {
        public override async Task Initialize()
        {
            await base.Initialize();
            if (NetworkData.Get().auth_auto_logout)
                Logout();
        }

        public override async Task<bool> Login(string username, string password)
        {
            LoginResponse res = await Client.Login(username, password);
            if (res.success)
            {
                this.logged_in = true;
                this.user_id = res.id;
                this.username = res.username;
                test_login = false;
            }
            return res.success;
        }

        public override async Task<bool> RefreshLogin()
        {
            if (!logged_in)
                return false;

            LoginResponse res = await Client.RefreshLogin();
            if (res.success)
            {
                this.logged_in = true;
                this.user_id = res.id;
                this.username = res.username;
                test_login = false;
            }
            return res.success;
        }

        public override async Task<bool> Register(string username, string email, string password)
        {
            RegisterResponse res = await Client.Register(username, email, password);

            if (res.success)
                await Login(username, password);

            return res.success;
        }

        public override bool IsSignedIn()
        {
            return Client.IsLoggedIn();
        }

        public override bool IsExpired()
        {
            return Client.IsExpired();
        }

        public override string GetError()
        {
            return Client.GetLastError();
        }

        public ApiClient Client { get { return ApiClient.Get(); } }

    }
#endif
}
