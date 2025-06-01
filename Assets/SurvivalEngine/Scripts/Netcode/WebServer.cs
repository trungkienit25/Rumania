using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using UnityEngine;

namespace NetcodePlus
{
    public class WebServer : MonoBehaviour
    {
        public Action<WebContext> onReceive;

        private HttpListener listener;
        private Thread listenerThread;
        private ConcurrentQueue<HttpListenerContext> pending;

        private Dictionary<string, Action<WebContext>> triggers = new Dictionary<string, Action<WebContext>>();

        private static WebServer instance;

        private void Awake()
        {
            instance = this;
        }

        public void StartServer(ushort port, bool secured = false)
        {
            string http = secured ? "https://" : "http://";
            listener = new HttpListener();
            listener.Prefixes.Add(http + "*:" + port + "/");
            listener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
            listener.Start();

            pending = new ConcurrentQueue<HttpListenerContext>();
            listenerThread = new Thread(RunListen);
            listenerThread.Start();
        }

        private void OnDestroy()
        {
            Clear();
        }

        private void Update()
        {
            if (pending == null)
                return;

            HttpListenerContext context;
            while (pending.TryDequeue(out context))
            {
                WebContext wcontext = WebContext.Create(context);
                ReceiveHttp(wcontext);
            }
        }

        private void ReceiveHttp(WebContext wcontext)
        {
            if (wcontext.method == "OPTIONS")
            {
                wcontext.SendResponse();
            }
            else
            {
                onReceive?.Invoke(wcontext);
                TriggerRequest(wcontext.path, wcontext);
            }
        }

        public void RegisterRequest(string path, Action<WebContext> callback)
        {
            triggers[path] = callback;
        }

        public void UnRegisterRequest(string path)
        {
            triggers.Remove(path);
        }

        private void TriggerRequest(string path, WebContext context)
        {
            if (triggers.ContainsKey(path))
                triggers[path].Invoke(context);
        }

        private void RunListen()
        {
            Debug.Log("Web Server Started");

            while (listener.IsListening)
            {
                try
                {
                    // wait for request ...
                    HttpListenerContext context = listener.GetContext();
                    //Debug.Log("Request received.");
                    pending.Enqueue(context);

                }
                catch (System.Exception e)
                {
                    Debug.Log(e);
                }
            }
        }

        public void Clear()
        {
            if (pending == null)
                return;

            while (!pending.IsEmpty)
            {
                HttpListenerContext context;
                while (pending.TryDequeue(out context))
                {
                    context.Response.Close();
                }
            }
            listener.Close();
            listenerThread.Abort();
        }


        public static WebServer Get()
        {
            return instance;
        }
    }

}