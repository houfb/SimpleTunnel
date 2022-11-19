using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleTunnelVisitor
{
    internal class Common
    {

        public static byte[] MakeHttpRequestMessageText(string link = null, string method = "GET", byte[] body = null, Dictionary<string, string> headers = null, string protocol = "HTTP/1.1")
        {
            link ??= "/";// link = link ?? "/";
            method = (method ?? "").ToUpper() == "POST" ? "POST" : "GET";// sb.Append("POST"); } 
            body ??= Array.Empty<byte>();
            headers ??= new Dictionary<string, string>();
            protocol ??= "HTTP/1.1"; //var type = "HTTP/1.1";
          



            var sb = new StringBuilder();
            sb.AppendLine($"{method} {link} {protocol}");
            foreach (var kv in headers)
            {
                if (string.IsNullOrEmpty(kv.Key)) continue;
                var key = (kv.Key ?? "").Replace("\r", "").Replace("\n", "").Trim();
                var val = (kv.Value ?? "").Replace("\r", "").Replace("\n", "").Trim();
                if (!string.IsNullOrEmpty(key)) sb.AppendLine($"{key}:{val}");
            }
            sb.AppendLine();

            var pre = sb.ToString();
            var buf = Encoding.UTF8.GetBytes(pre);

            var buffer = new byte[buf.Length + body.Length];
            Array.Copy(buf, 0, buffer, 0, buf.Length);
            Array.Copy(body, 0, buffer, buf.Length, body.Length);
            return buffer;
             



            //HTTP请求报文示例：
            //POST /framework/cmn_params HTTP/1.1
            //Accept: application/json, text/javascript, */*; q=0.01
            //Accept-Encoding: gzip, deflate, br
            //Accept-Language: zh-CN,zh-TW;q=0.9,zh;q=0.8,en;q=0.7
            //Connection: keep-alive
            //Content-Length: 0
            //Cookie: _ga_34B604LFFQ=GS1.1.1661999395.6.0.1661999395.60.0.0; _ga=GA1.1.1287016976.1661323859; ASP.NET_SessionId=24gx1py14zeqhekdjfbx1xn0; FFF=E7E914055C16369B7DBA549FE8FDF189E5002C9E9D9D9B753402CE4108236EF51FBDAC9A7326F80DFEA62DBA44240E61E63D1024D38EFB5A16CA7F7470E9CDC81FFAC06C6C8439D145B06AD8665AA6DAA73ABE668C1F666D9F16F219D0300F01BA07A95FF07E27C9618D1E6991FC4602FCA9C1BAACA5860705386F5AD6D312D85694F02528A53C928E40DF01810579659F84253E16D37FBB456DBA150F30823A
            //DNT: 1
            //Host: 9s9s.com
            //Origin: https://9s9s.com
            //Referer: https://9s9s.com/main_left.html
            //Sec-Fetch-Dest: empty
            //Sec-Fetch-Mode: cors
            //Sec-Fetch-Site: same-origin
            //User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/107.0.0.0 Safari/537.36
            //X-Requested-With: XMLHttpRequest
            //sec-ch-ua: "Google Chrome";v="107", "Chromium";v="107", "Not=A?Brand";v="24"
            //sec-ch-ua-mobile: ?0
            //sec-ch-ua-platform: "Windows"
            //
            //正文部分的内容，它可以是文本字串、二进制字节串等。 
        }




    }
}
