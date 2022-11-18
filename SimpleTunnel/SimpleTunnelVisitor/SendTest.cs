using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace SimpleTunnelVisitor
{
    internal class SendTest
    {
        public void Start()
        {
            Task.Run(() =>
            {
                var loginMsg = $"login\r\n5B68ECD5-E552-491B-AA23-6B37163A9FB5";
                Send(loginMsg);
            });
        }

        string _webIP = "127.0.0.1";
        int _webPort = 10011;

        void Send(string text)
        {

            using Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(new System.Net.IPEndPoint(IPAddress.Parse(_webIP), _webPort));
            socket.Send(Encoding.UTF8.GetBytes(text));

            socket.Close();

            //HttpRequestMessage request = new HttpRequestMessage();
            //request.Method = HttpMethod.Post;
            //request.Content = new StringContent(text);
            //var snd = request.ToString();



        }



    }
}
