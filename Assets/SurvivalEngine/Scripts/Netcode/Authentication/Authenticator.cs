using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace NetcodePlus
{
    /// <summary>
    /// Base class for all Authenticators, must be inherited
    /// Note: Steam and Google Authenticator are just code examples and are not tested/implemented
    /// </summary>

    public abstract class Authenticator
    {
        protected string user_id = null;
        protected string username = null;
        protected bool logged_in = false;
        protected bool test_login = false;
        protected bool inited = false;

        public virtual async Task Initialize()
        {
            inited = true;
            await Task.Yield(); //Do nothing
        }

        public virtual async Task<bool> Login()
        {
            await Task.Yield(); //Do nothing
            return false;
        }

        public virtual async Task<bool> Login(string username)
        {
            return await Login(); //Some authenticator dont define this function
        }

        public virtual async Task<bool> Login(string username, string token)
        {
            return await Login(username); //Some authenticator dont define this function
        }

        public virtual async Task<bool> RefreshLogin()
        {
            return await Login(); //Same as Login if not defined
        }

        public virtual void LoginTest()
        {
            LoginTest(NetworkTool.GenerateRandomID());
        }

        //Bypass login system by just assigning your own values, for testing
        public virtual void LoginTest(string username)
        {
            this.user_id = username;
            this.username = username;
            logged_in = true;
            test_login = true;
        }

        public virtual async Task<bool> Register(string username)
        {
            return await Login(username); //Some authenticator dont define this function
        }

        public virtual async Task<bool> Register(string username, string token)
        {
            return await Login(username, token); //Some authenticator dont define this function
        }

        public virtual async Task<bool> Register(string username, string email, string token)
        {
            return await Login(username, token); //Some authenticator dont define this function
        }

        public virtual async void Update(float delta)
        {
            await Task.Yield();
        }

        public virtual void Logout()
        {
            logged_in = false;
            test_login = false;
            user_id = null;
            username = null;
        }

        public virtual bool IsInited()
        {
            return inited;
        }

        public virtual bool IsConnected()
        {
            return IsSignedIn() && !IsExpired(); 
        }

        public virtual bool IsConnectedOnline()
        {
            return IsConnected() && !test_login; //Check that it is truly connected and not connected in test mode
        }

        public virtual bool IsSignedIn()
        {
            return logged_in; //IsSignedIn will still be true if the login expires
        }

        public virtual bool IsExpired()
        {
            return false;
        }

        public virtual bool IsUnityServices()
        {
            return false; //Override condition
        }

        public virtual string GetUserId()
        {
            return user_id;
        }

        public virtual string GetUsername()
        {
            return username;
        }

        public virtual string GetProviderId()
        {
            return GetUserId(); //By default, its same than user id
        }

        public virtual string GetError()
        {
            return ""; //Should return the latest error
        }

        public string UserID{ get{ return GetUserId(); }}
        public string Username{ get { return GetUsername(); } }

        public static Authenticator Create(AuthenticatorType type)
        {
#if USER_LOGIN
            if (type == AuthenticatorType.UserLoginAPI)
                return new AuthenticatorUserApi();
#endif

            if (type == AuthenticatorType.Google)
                return new AuthenticatorGoogle();
            else if (type == AuthenticatorType.Steam)
                return new AuthenticatorSteam();
            else if (type == AuthenticatorType.Unity)
                return new AuthenticatorUnity();
            else
                return new AuthenticatorTest();
        }

        public static Authenticator Get()
        {
            return TheNetwork.Get().Auth; //Access authenticator
        }
    }

    public enum AuthenticatorType
    {
        Test = 0, //Ideal for quick testing, will just generate random ID, requires no integration

        Unity = 5,          //Ideal for testing Unity Services, will require to have Unity Services project ID linked to your unity project
        Google = 10,        //Not implemented
        Steam = 20,         //Not implemented

        UserLoginAPI = 80,  //3rd Party Asset required

        Custom = 100,       //Not implemented
        

    }
}