using System;
using System.Collections.Generic;
using GameFramework;
using GameFramework.Event;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace GameMain.UnityGameFramework.Scripts.Runtime
{
    public class ChatAIComponent:GameFrameworkComponent
    {
        [Header("根据需要挂载不同的llm脚本")]
        [SerializeField] public LLM m_ChatModel;

        private Dictionary<int, Action<string>> m_WaitCallbackDic = new Dictionary<int, Action<string>>();

        private void Start()
        {
            var eventComp = GameEntry.GetComponent<EventComponent>();
            eventComp.Subscribe(WebRequestSuccessEventArgs.EventId,OnWebRequestSuccessCallback);
        }

        public void SendMsg(string _msg,Action<string> _callback)
        {
            if (m_ChatModel == null)
                return;
            m_WaitCallbackDic.TryAdd(m_ChatModel.PostMsg(_msg),_callback);
        }
        private void OnWebRequestSuccessCallback(object sender, GameEventArgs e)
        {
            WebRequestSuccessEventArgs ee = (WebRequestSuccessEventArgs)e;
            if (ee == null)
            {
                return;
            }

            if (m_WaitCallbackDic.ContainsKey(ee.SerialId))
            {
                string responseFullStr =System.Text.Encoding.UTF8.GetString(ee.GetWebResponseBytes());
                string aiResponse = m_ChatModel.OnChatRequestSuccessCallback(responseFullStr);
                m_WaitCallbackDic[ee.SerialId].Invoke(aiResponse);
            }
        }
    }
    [Serializable]
    public class SendData:IReference
    {
        [SerializeField] public string role;
        [SerializeField] public string content;
        public SendData() { }
        public SendData(string _role, string _content)
        {
            role = _role;
            content = _content;
        }
        public SendData Init(string _role, string _content)
        {
            role = _role;
            content = _content;
            return this;
        }
        public void Clear()
        {
            
        }
    }
    [Serializable]
    public class MessageBack
    {
        public string created_at;
        public string model;
        public Message message;
    }
    [Serializable]
    public class Message
    {
        public string role;
        public string content;
    }
}