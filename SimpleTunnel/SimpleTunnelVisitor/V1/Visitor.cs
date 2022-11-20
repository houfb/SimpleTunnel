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
            Task.Run(async () =>
            {
                while (true) { await DoHttpRequest(); await Task.Delay(1000); }
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
        string _serverIP = "127.0.0.1";//"houfb.cn";//"127.0.0.1";
        int _serverPort = 80;



        async void DoHttpRequest_v1()
        {
            await Task.Run(() =>
            {
                try
                {
                    AddRich("-------------------------------------------------");

                    //组装请求报文
                    var link = "/getinfo/"; var method = "GET"; var body = Encoding.UTF8.GetBytes("userid=001");
                    var rbuf = Common.MakeHttpRequestMessageText(link, method, body, new Dictionary<string, string>() {
                        { "Content-Length", body.Length.ToString() },
                        { "Host", "houfb.cn" },
                    });


                    //连接服务器 
                    using var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                    socket.SendTimeout = 1000; socket.ReceiveTimeout = 10000;
                    socket.Connect(new System.Net.IPEndPoint(IPAddress.Parse(_serverIP), _serverPort));
                    AddRich("服务端连接成功！");


                    //发送请求报文
                    socket.Send(rbuf);


                    //获取回致报文
                    var buffer = new byte[1024]; var lg = socket.Available; var sd = socket.Connected;
                    var len = socket.Receive(buffer);
                    if (len > 0)
                    {
                        AddRich("回致报文：" + Encoding.UTF8.GetString(buffer, 0, len));
                    }
                    else
                    {
                        AddRich("回致报文长度：" + len);
                    }
                }
                catch (Exception ex) { AddRich($"M111809,{ex.Message}"); }
            });

            Thread.Sleep(1000);
            DoHttpRequest();
        }

        async Task DoHttpRequest()
        {
            await Task.Run(  async () =>
           {
               Socket socket = null;
               try
               {
                   using var done = new ManualResetEventSlim(false); using var e = new SocketAsyncEventArgs();
                   e.Completed += (obj, arg) => { done.Set(); };
                   using var ee = new SocketAsyncEventArgs();
                   ee.Completed += (obj, arg) => { done.Set(); };

                   socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                   socket.SendTimeout = 1000; socket.ReceiveTimeout = 1000;
                   AddRich("-------------------------------------------------");


                   //组装请求报文
                   var link = "/main_left.html"; var method = "GET"; var body = Encoding.UTF8.GetBytes("");
                   var rbuf = Common.MakeHttpRequestMessageText(link, method, body, new Dictionary<string, string>() {
                        { "Content-Length", body.Length.ToString() },
                        { "Host", "houfb.cn" },
                   });


                   //连接服务器    
                   //socket.Connect(new System.Net.IPEndPoint(IPAddress.Parse(_serverIP), _serverPort));
                   //AddRich("服务端连接成功！");  
                   //if (socket.Connected == false)
                   //{ 
                   e.RemoteEndPoint = new System.Net.IPEndPoint(IPAddress.Parse(_serverIP), _serverPort);
                   if (socket.ConnectAsync(e)) { done.Wait(10000); }
                   if (e.SocketError != SocketError.Success) { AddRich($"服务端连接失败：{e.SocketError}"); goto PT; }
                   AddRich("服务端连接成功！");
                   //}



                   //发送请求报文
                   //socket.Send(rbuf);
                   e.SetBuffer(rbuf); done.Reset();
                   if (socket.SendAsync(e)) { done.Wait(10000); }
                   if (e.SocketError != SocketError.Success) { AddRich($"发送请求报文失败：{e.SocketError}"); goto PT; }
                   AddRich("发送请求报文成功！");


                   //获取回致报文
                   //var buffer = new byte[1024]; var lg = socket.Available; var sd = socket.Connected;
                   //var len = socket.Receive(buffer);
                   //if (len > 0)
                   //{
                   //    AddRich("回致报文：" + Encoding.UTF8.GetString(buffer, 0, len));
                   //}
                   //else
                   //{
                   //    AddRich("回致报文长度：" + len);
                   //}
                   Thread.Sleep(1000);
                   var buffer = new byte[1024]; done.Reset();
                   ee.SetBuffer(buffer);
                   if (socket.ReceiveAsync(ee)) { done.Wait(1000); }
                   if (ee.SocketError != SocketError.Success) { AddRich($"接收响应报文失败：{ee.SocketError}"); }
                   AddRich("接收响应报文成功！响应报文：" + ee.BytesTransferred);
                   AddRich("" + Encoding.UTF8.GetString(buffer, ee.Offset, ee.BytesTransferred));
                   //if (ee.BytesTransferred < 1)
                   //{
                   //    await socket.DisconnectAsync(false); 
                   //}



               PT:

                   //释放连接
                   socket.Shutdown(SocketShutdown.Both); socket.Disconnect(false); socket.Close(); socket.Dispose();

               }
               catch (ObjectDisposedException ex) { AddRich($"M112012,{ex.Message}"); }
               catch (SocketException ex) { AddRich($"M112013,{ex.Message}"); }
               catch (Exception ex) { AddRich($"M111809,{ex.Message}"); }
               finally
               {
                   try { await socket.DisconnectAsync(false); } catch { }
                   using (socket) {    }
               }
           });

            //Thread.Sleep(1000);
            //DoHttpRequest();
        }



    }
}
