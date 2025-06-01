using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace NetcodePlus
{

    public class WebClient : MonoBehaviour
    {
        private static WebClient instance;

        private string url;
        private ulong client_id = 0;    //Will be sent as key
        private string key;             //Custom key, will only be used if the client_id is 0

        void Awake()
        {
            instance = this;
            client_id = NetworkTool.GenerateRandomUInt64();
        }

        public void SetDefaultUrl(string host, ushort port, bool secured = false)
        {
            this.url = GetRawUrl(host, port, secured);
        }

        public void SetDefaultUrlRaw(string url)
        {
            this.url = url;
        }

        public void SetClientID(ulong client_id)
        {
            this.client_id = client_id;
            this.key = "";
        }

        public void SetKey(string key)
        {
            this.key = key;
            this.client_id = 0;
        }

        // ---------- Send Values --------------

        public async Task<WebResponse> Send(string path)
        {
            return await SendGetRequest(url + "/" + path);
        }

        public async Task<WebResponse> Send(string path, ulong data)
        {
            string jdata = data.ToString();
            return await SendPostRequest(url + "/" + path, jdata);
        }

        public async Task<WebResponse> Send(string path, string data)
        {
            return await SendPostRequest(url + "/" + path, data);
        }

        public async Task<WebResponse> Send<T1>(string path, T1 data)
        {
            string jdata = WebTool.ToJson(data);
            return await SendPostRequest(url + "/" + path, jdata);
        }

        // ---------- Send Values to raw URL --------------

        public async Task<WebResponse> SendUrl(string furl)
        {
            return await SendGetRequest(furl);
        }

        public async Task<WebResponse> SendUrl(string furl, ulong data)
        {
            string jdata = data.ToString();
            return await SendPostRequest(furl, jdata);
        }

        public async Task<WebResponse> SendUrl(string furl, string data)
        {
            return await SendPostRequest(furl, data);
        }

        public async Task<WebResponse> SendUrl<T1>(string furl, T1 data)
        {
            string jdata = WebTool.ToJson(data);
            return await SendPostRequest(furl, jdata);
        }

        // ---------- Requests --------------

        public async Task<WebResponse> SendGetRequest(string url)
        {
            return await SendRequest(url, WebRequest.METHOD_GET, "");
        }

        public async Task<WebResponse> SendPostRequest(string url, string json_data)
        {
            return await SendRequest(url, WebRequest.METHOD_POST, json_data);
        }

        public async Task<WebResponse> SendRequest(string url, string method, string json_data)
        {
            string akey = client_id > 0 ? client_id.ToString() : key;
            UnityWebRequest request = WebRequest.Create(url, method, json_data, akey);
            return await WebTool.SendRequest(request);
        }

        public async Task<WebResponse> SendUploadRequest(string url, string path, byte[] data)
        {
            string akey = client_id > 0 ? client_id.ToString() : key;
            UnityWebRequest request = WebRequest.CreateImageUploadForm(url, path, data, akey);
            return await WebTool.SendRequest(request);
        }

        // ---------- Getters --------------

        public ulong GetClientID()
        {
            return client_id;
        }

        public string GetKey()
        {
            return key;
        }

        public string GetRawUrl(string host, ushort port, bool secured = false)
        {
            string http = secured ? "https://" : "http://";
            return http + host + (port != 80 ? ":" + port : "");
        }

        public string GetRawUrl(string host, ushort port, string path, bool secured = false)
        {
            string http = secured ? "https://" : "http://";
            return http + host + (port != 80 ? ":" + port : "") + "/" + path;
        }

        public static WebClient Get()
        {
            return instance;
        }
    }
}
