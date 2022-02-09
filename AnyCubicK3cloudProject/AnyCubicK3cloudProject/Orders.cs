using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnyCubicK3cloudProject
{
    /// <summary>
    /// 订单信息类
    /// </summary>
    public class Orders
    {
        public string order_id { set; get; }

        public string prdt_code { set; get; }

        public int qty { set; get; }

        public string id { set; get; }

        public string regist_id { set; get; }

        public string back_time { set; get; }

        public decimal amt { set; get; }

        public string is_free { set; get; }

    }
    public class Parameters
    {
        public string id { set; get; }
        public string app_key { set; get; }
        public string AppSecret { set; get; }
        public string page_size { set; get; }
        public string start_date { set; get; }
        public string end_date { set; get; }
        public string access_token { set; get; }
        public string url { set; get; }
        public int page_no { set; get; }
        public string method { set; get; }
        public string username { set; get; }
        public string password { set; get; }
        public string wh_code { set; get; }

        public string site_id { set; get; }
    }
}
