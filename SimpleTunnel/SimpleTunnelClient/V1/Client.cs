using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace SimpleTunnelClient.V1
{
    internal class Client
    {
        public void Start()
        {
            //Task.Run(() =>
            //{
            //    ConnectServer();
            //});

            ConnectServer();
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
        string _secret = "5B68ECD5-E552-491B-AA23-6B37163A9FB5";
        /// <summary>
        /// 监听域名
        /// </summary>
        string _domain = "houfb.cn"; //"http://houfb.cn";
        //System.Collections.Concurrent.ConcurrentBag<STClient> _STClientArray = new System.Collections.Concurrent.ConcurrentBag<STClient>();
        string _serverIP = "127.0.0.1";
        int _serverPort = 10011;




        /// <summary>
        /// 客户端实例对象模型
        /// </summary>
        class STClient
        {
            /// <summary>
            /// 客户端连接套接字
            /// </summary>
            public Socket socket;
            /// <summary>
            /// 请求信息包队列
            /// </summary>
            public System.Collections.Concurrent.ConcurrentQueue<RequestInfoPack> requestQueue = new System.Collections.Concurrent.ConcurrentQueue<RequestInfoPack>();

            /// <summary>
            /// 客户端状态：0表示已连接成功，1表示已通过登录认证，2表示已断开连接
            /// </summary>
            public short status = 0;
            /// <summary>
            /// 最后一次收到请求的时间
            /// </summary>
            public DateTime lastRequestTime = DateTime.Now;

            /// <summary>
            /// 客户端指定要监听的域名
            /// </summary>
            public string domain;
            /// <summary>
            /// 客户端指定要监听的域名
            /// </summary>
            public string connection;

        }
        /// <summary>
        /// 请求信息包模型
        /// </summary>
        class RequestInfoPack
        {
            public HttpRequestMessage request;
            public string headerText;
            public string url;
            public string method;
            public string host;

            /// <summary>
            /// 请求信息包的处理状态：0表示未处理，1表示已发送到客户端，2表示客户端已回致响应包，3表示已将响应包响应出去
            /// </summary>
            public short status;

            public HttpResponseMessage response;

        }


        Socket socket = null!;
        void ConnectServer()
        {
            Task.Run(() =>
            {
                var ic_redo = 0;

                if (ic_redo > 0) { Thread.Sleep(5000); }
            redo:
                ic_redo++;
                //Socket socket = null!;
                try
                {
                    //var loginMsg = $"login\r\n5B68ECD5-E552-491B-AA23-6B37163A9FB5"; 
                    var loginMsg = $"login\r\n{_secret}\r\n{_domain}";


                    //连接服务器，并立即请求登录
                    socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    socket.SendTimeout = 5000; socket.ReceiveTimeout = 5000;

                    socket.Connect(new System.Net.IPEndPoint(IPAddress.Parse(_serverIP), _serverPort));
                    socket.Send(Encoding.UTF8.GetBytes(loginMsg));
                    //socket.Close();
                    AddRich("服务端连接成功！");



                    //获取回致，登录成功后，服务端应返回一个OK
                    var buffer = new byte[1024];
                    var len = socket.Receive(buffer);
                    if (len >= 2 && buffer[0] == 'O' && buffer[1] == 'K')
                    {
                        AddRich("服务端登录成功！");

                        var done = Pool.NewManualResetEventSlim();
                        var e = Pool.NewSocketAsyncEventArgs();
                        e.SetBuffer(Array.Empty<byte>());
                        e.Completed += (obj, arg) => { done.Set(); };
                        while (true)
                        {
                            try
                            {
                                var b = socket.SendAsync(e);//(new ArraySegment<byte>(), SocketFlags.None);
                                if (b) { done.Wait(1000); }

                                if (e.SocketError == SocketError.Success)
                                {
                                    Thread.Sleep(TimeSpan.FromMinutes(0.5));
                                }
                                else
                                {
                                    throw new Exception($"连接失败，{{{e.SocketError}}}");
                                }
                            }
                            catch (Exception ex)
                            {
                                AddRich($"M111913,服务端连接异常，{ex.Message},将在稍后自动尝试重新连接！");


                                //释放资源，并在稍后自动尝试重新连接
                                //Thread.Sleep(5000);
                                using (done) { }
                                using (e) { }
                                using (socket) { }
                                goto redo;
                            }
                            //Thread.Sleep(TimeSpan.FromMinutes(1));
                        }
                    }
                    else
                    {
                        AddRich("服务端登录失败：" + Encoding.UTF8.GetString(buffer, 0, len));

                        //释放资源，并在稍后自动尝试重新连接
                        using (socket) { }
                        goto redo;

                    }
                }
                catch (Exception ex)
                {
                    AddRich($"M111809,{ex.Message}"); using (socket) { }

                    //释放资源，并在稍后自动尝试重新连接
                    // socket = null;
                    if (socket != null) { using (socket) { } }
                    goto redo;
                }
            });
        }




    }
}
