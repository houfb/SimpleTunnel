using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SimpleTunnelVisitor.V1
{
    internal class Visitor
    {
        public void Start()
        {
            Task.Run(() =>
            {
                DoHttpRequest();
            });
        }


        /// <summary>
        /// 输出日志信息到控制台
        /// </summary>
        /// <param name="txt"></param>
        /// <param name="type"></param>
        void AddRich(string txt, int type = 0) { if (type == 0) Console.WriteLine(txt); }
        /// <summary>
        /// 服务端的对外监听端口号，用于对外监听接收来自互联网的信息包
        /// </summary>
        //int _outerPort = 80;
        /// <summary>
        /// 服务端的对内监听端口号，用于客户端长连接专用
        /// </summary>
        //int _innerPort = 10011;
        /// <summary>
        /// 密钥集，用来验证客户端的准入
        /// </summary>
        //string[] _secrets = { "5B68ECD5-E552-491B-AA23-6B37163A9FB5", "ABE334B0-059D-4EC4-B65E-BB53398AAE6D" };
        //string _secret = "5B68ECD5-E552-491B-AA23-6B37163A9FB5";
        /// <summary>
        /// 监听域名
        /// </summary>
        //string _domain = "http://houfb.cn";
        //System.Collections.Concurrent.ConcurrentBag<STClient> _STClientArray = new System.Collections.Concurrent.ConcurrentBag<STClient>();
        string _serverIP = "127.0.0.1";
        int _serverPort = 80;



        void DoHttpRequest()
        {
            try
            {
                AddRich("-------------------------------------------------");
               
                //组装请求报文
                var link = "/getinfo/"; var method = "GET"; var body = Encoding.UTF8.GetBytes("userid=001");
                var rbuf = Common.MakeHttpRequestMessageText(link, method, body, new Dictionary<string, string>() { { "Content-Length", body.Length.ToString() } });

                 
                //连接服务器 
                using var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                socket.SendTimeout = 1000; socket.ReceiveTimeout = 1000;
                socket.Connect(new System.Net.IPEndPoint(IPAddress.Parse(_serverIP), _serverPort));
                AddRich("服务端连接成功！");
                

                //发送请求报文
                socket.Send(rbuf);
               

                //获取回致报文
                var buffer = new byte[1024];
                var len = socket.Receive(buffer);
                if (len >0)
                {
                    AddRich("回致报文：" + Encoding.UTF8.GetString(buffer,0,len));
                }
                else
                {
                    AddRich("回致报文长度：" + len); 
                }
            }
            catch (Exception ex) { AddRich($"M111809,{ex.Message}");   }
        }





    }
}
