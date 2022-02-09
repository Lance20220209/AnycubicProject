using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnyCubicK3cloudProject
{
    public class ZWLF_CreateErpOrderTest
    {
        public void CreateErpOrderTest()
        {
            string  AppSecret = "158820494c2e53c8923f05926cc7695a";
            string url = "http://erpapi.thewto.com/router/rest.shtml";
            //应用级输入参数：
            Dictionary<string, string> @params = new Dictionary<string, string>();
            @params.Add("method", "frdas.erp.order.add");
            @params.Add("app_key", "9596741624");
            @params.Add("sign_method", "md5");
           // @params.Add("sign", ""); 
            @params.Add("session", "c0ec54a1d2afd177461388c18945af4b");
            @params.Add("timestamp", DateTime.Now.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
            @params.Add("format", "json");
            @params.Add("v", "1.0");
            //业务输入参数：
            @params.Add("pay_time",DateTime.Now.AddMinutes(-10).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
            @params.Add("country_code_two", "CN");
            @params.Add("cur_code", "CNY");
            @params.Add("logistics_code", "1");
            @params.Add("wh_code", "CK002");
            @params.Add("site_user", "AliLucy");
            @params.Add("site_trade_id", "测试202220118");
            @params.Add("buyer_nick", "黄小生");
            @params.Add("receiver_name", "黄小生");
            //商品信息明细
            JArray Jsonarray = new JArray();
            JObject jObject = new JObject();
            jObject.Add("prdt_code", "E07020244");
            jObject.Add("qty", 2);
            Jsonarray.Add(jObject);
            @params["details"] = Jsonarray.ToString();
            Httpclient httpclient = new Httpclient();
            string json = httpclient.ZWLFRequstPost(@params, url, AppSecret);
            JObject OrderJson = JsonConvert.DeserializeObject<JObject>(json);
        }
    }
}
