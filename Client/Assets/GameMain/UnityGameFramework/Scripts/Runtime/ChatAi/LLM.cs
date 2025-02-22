using System;
using System.Collections.Generic;
using GameFramework;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace GameMain.UnityGameFramework.Scripts.Runtime
{
    public class LLM:MonoBehaviour
    {
        /// <summary>
        /// api地址
        /// </summary>
        [SerializeField] protected string url;
        /// <summary>
        /// 上下文保留条数
        /// </summary>
        [Header("上下文保留条数")]
        [SerializeField] protected int m_HistoryKeepCount = 15;
        /// <summary>
        /// 缓存对话
        /// </summary>
        [SerializeField] public List<SendData> m_DataList = new List<SendData>();
        /// <summary>
        /// 发送消息
        /// </summary>
        public virtual int PostMsg(string _msg) {
            //上下文条数设置
            CheckHistory();
            //提示词处理
            string message = _msg;

            //缓存发送的信息列表
            m_DataList.Add(ReferencePool.Acquire<SendData>().Init("user", message));
            PostData _postData = ReferencePool.Acquire<PostData>();
            _postData.model = "deepseek-r1:14b";
            _postData.messages = m_DataList;

            string _jsonText = JsonUtility.ToJson(_postData);
            byte[] data = System.Text.Encoding.UTF8.GetBytes(_jsonText);
            return GameEntry.WebRequest.AddWebRequest(url, data);
        }

        public virtual string OnChatRequestSuccessCallback(string msg)
        {
            MessageBack _textback = JsonUtility.FromJson<MessageBack>(msg);
            if (_textback != null && _textback.message!=null)
            {

                string _aiResponse = _textback.message.content;
                m_DataList.Add(new SendData("assistant", _aiResponse));
                return _aiResponse;
            }
            return string.Empty;
        }
        /// <summary>
        /// 设置保留的上下文条数，防止太长
        /// </summary>
        public virtual void CheckHistory()
        {
            if(m_DataList.Count> m_HistoryKeepCount)
            {
                m_DataList.RemoveAt(0);
            }
        }
    }
    [Serializable]
    public class PostData:IReference
    {
        public string model;
        public List<SendData> messages;
        public bool stream = false;
        public void Clear()
        {
            model = string.Empty;
            messages = null;
            stream = false;
        }
    }
}