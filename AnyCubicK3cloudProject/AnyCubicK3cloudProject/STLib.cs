using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace AnyCubicK3cloudProject
{
    public class STLib
    {
        /// <summary>
        /// 获取签名
        /// </summary>
        /// <param name="params"></param>
        /// <param name="AppSecret"></param>
        /// <returns></returns>
        public string GetSignContent(IDictionary<string, string> @params, string AppSecret)
        {
            SortedDictionary<string, string> treeMap = new SortedDictionary<string, string>(@params, new Comparator());
            // 拼接要签名的字符串
            StringBuilder builder = new StringBuilder();
            builder.Append(AppSecret);
            foreach (string key in treeMap.Keys)
            {
                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(treeMap[key])) continue;
                builder.Append(key + treeMap[key]);
            }
            builder.Append(AppSecret);
            // 使用MD5加密
            byte[] bytes = MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
            //把二进制转化为大写的十六进制
            builder.Clear();
            for (int i = 0; i < bytes.Length; i++)
            {
                string hex = bytes[i].ToString("X");
                builder.Append(hex.Length == 1 ? "0" + hex : hex);
            }

            return builder.ToString();
        }
        /**
        * 比较器
        */
        private class Comparator : IComparer<String>
        {
            public int Compare(string x, string y)
            {
                return x.ToLower().CompareTo(y.ToLower());
            }
        }

        /// <summary>
        /// MD5字符串加密
        /// </summary>
        /// <param name="txt"></param>
        /// <returns>加密后字符串</returns>
        public string GenerateMD5(string txt)
        {
            //md5(secret+bar2foo1foo_bar3foobar4+secret)；
            using (MD5 mi = MD5.Create())
            {
                byte[] buffer = Encoding.UTF8.GetBytes(txt);
                //开始加密
                byte[] newBuffer = mi.ComputeHash(buffer);
                StringBuilder sb = new StringBuilder();
                sb.Clear();
                for (int i = 0; i < newBuffer.Length; i++)
                {
                    //sb.Append(newBuffer[i].ToString("X2"));
                    string hex = newBuffer[i].ToString("X");
                    sb.Append(hex.Length == 1 ? "0" + hex : hex);
                }
                return sb.ToString().Trim();
            }
        }


        /// <summary>
        /// 刷新token（不需要）
        /// </summary>
        /// <param name="AppSecret"></param>
        /// <param name="app_key"></param>
        /// <returns></returns>
        public string RefreshToken(string AppSecret, string app_key,string refresh_token)
        {
            Httpclient httpclient = new Httpclient();
            string url = "http://oauth-test.thewto.com/token";
            Dictionary<string, string> pairs = new Dictionary<string, string>();
            pairs.Add("client_id", app_key);
            pairs.Add("client_secret", AppSecret);
            pairs.Add("grant_type", "refresh_token");
            pairs.Add("refresh_token", refresh_token);
            pairs.Add("state", "1212");
            pairs.Add("redirect_uri", "http://www.oauth.net/2/");
            pairs.Add("view", "web");
            string jsonstr = httpclient.RequstPost(pairs, url);
            JObject Json = JsonConvert.DeserializeObject<JObject>(jsonstr);
            string token = Json["access_token"].ToString();
            return token;
        }


        /// <summary>
        /// 获取token
        /// </summary>
        /// <param name="AppSecret"></param>
        /// <param name="app_key"></param>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public string GetAccessToken(string AppSecret, string app_key, string username, string password,string tokenUrl,string redirect_uri,string CodeUrl)
        {
            string code = GetCode(app_key, username, password,  redirect_uri,  CodeUrl);
            Httpclient httpclient = new Httpclient();
            Dictionary<string, string> pairs = new Dictionary<string, string>();
            pairs.Add("client_id", app_key);
            pairs.Add("client_secret", AppSecret);
            pairs.Add("grant_type", "authorization_code");
            pairs.Add("code", code);
            pairs.Add("redirect_uri", redirect_uri);
            pairs.Add("state", "1212");
            pairs.Add("view", "web");
            string jsonstr = httpclient.RequstPost(pairs, tokenUrl);
            JObject Json = JsonConvert.DeserializeObject<JObject>(jsonstr);
            string token = Json["access_token"].ToString();
            return token;
        }


        /// <summary>
        /// 获取Code
        /// </summary>
        /// <param name="app_key"></param>
        /// <param name="username">账号</param>
        /// <param name="password">密码</param>
        /// <returns></returns>
        public string GetCode(string app_key, string username, string password, string redirect_uri, string CodeUrl)
        {
            string code = "";
            Httpclient httpclient = new Httpclient();
            Dictionary<string, string> pairs = new Dictionary<string, string>();
            pairs.Add("client_id", app_key);
            pairs.Add("response_type", "code");
            pairs.Add("redirect_uri", redirect_uri);
            pairs.Add("state", "1212");
            pairs.Add("view", "web");
            pairs.Add("username", username);
            pairs.Add("password", password);
            string jsonstr = httpclient.RequstPost(pairs, CodeUrl);
            JObject OrderJson = JsonConvert.DeserializeObject<JObject>(jsonstr);
            string results = OrderJson["code"].ToString();
            if (results == "200")
            {
                string urlstr = OrderJson["data"].ToString();
                code = GetUrlParameterValue(urlstr, "code");
            }
            return code;
        }

        /// <summary>
        /// 获取 url链接 参数名对应的值，需要特定格式
        /// </summary>
        /// <param name="url">url链接</param>
        /// <param name="parameter">参数名</param>
        /// <returns>对应参数值</returns>
        public static string GetUrlParameterValue(string url, string parameter)
        {
            var index = url.IndexOf("?");
            //判断是否携带参数
            if (index > -1)
            {
                //为了去掉问号
                index++;
                //截取 参数部分
                var targetUrl = url.Substring(index, url.Length - index);
                //按 '&' 分成N个数组
                string[] Param = targetUrl.Split('&');
                //循环匹配
                foreach (var parm in Param)
                {
                    //再按等号分组
                    var values = parm.Split('=');
                    //统一按小写 去匹配
                    if (values[0].ToLower().Equals(parameter.ToLower()))
                    {
                        //返回匹配成功的值
                        return values[1];
                    }
                }
            }
            return null;
        }
    }
}
