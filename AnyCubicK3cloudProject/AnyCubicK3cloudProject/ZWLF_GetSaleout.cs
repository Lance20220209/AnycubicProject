using System;
using System.Collections.Generic;
using System.Linq;
using Kingdee.BOS;
using Kingdee.BOS.App;
using Kingdee.BOS.Contracts;
using Kingdee.BOS.Core;
using Kingdee.BOS.Core.Bill;
using Kingdee.BOS.Core.DynamicForm;
using Kingdee.BOS.Core.DynamicForm.PlugIn;
using Kingdee.BOS.Core.Metadata;
using Kingdee.BOS.Core.Metadata.FormElement;
using Kingdee.BOS.Orm;
using Kingdee.BOS.ServiceHelper;
using Kingdee.BOS.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Data;

namespace AnyCubicK3cloudProject
{
    public class ZWLF_GetSaleout
    {
        /// <summary>
        /// 调用胜途销售出库接口
        /// </summary>
        /// <param name="param"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public Msg GetSaleout(Parameters param, Context context)
        {
            Msg msg = new Msg();
            try
            {
                //应用级输入参数：
                Dictionary<string, string> @params = new Dictionary<string, string>();
                @params.Add("method", param.method);
                @params.Add("app_key", param.app_key);
                @params.Add("sign_method", "md5");
                // @params.Add("sign", zWLF.sign); 
                @params.Add("session", param.access_token);
                @params.Add("timestamp", DateTime.Now.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                @params.Add("format", "json");
                @params.Add("v", "1.0");
                //业务输入参数：
                // 默认为0（0:平台发货时间，1：系统发货时间）
                @params["date_type"] = "0";
                DateTime Btime = Convert.ToDateTime(param.start_date);
                @params["start_date"] = Btime.ToString("yyyy-MM-dd HH:mm:ss");
                DateTime Etime = Convert.ToDateTime(param.end_date);
                @params["end_date"] = Etime.ToString("yyyy-MM-dd HH:mm:ss");
                @params["page_size"] = param.page_size;
                @params["page_no"] = param.page_no.ToString();
                @params["id"] = param.id; //订单唯一标识，默认为空
                @params["site_id"] = param.site_id;// 店铺ID
                @params["wh_code"] = param.wh_code;// 仓库编码
                Httpclient httpclient = new Httpclient();
                string OrderJson = httpclient.ZWLFRequstPost(@params, param.url, param.AppSecret);
                JObject json = JsonConvert.DeserializeObject<JObject>(OrderJson);
                string code = json["code"].ToString();
                if (code == "200")
                {
                    //本次返回的订单数
                    int total_count = Convert.ToInt32(json["data"]["total_count"].ToString());
                    #region 解析
                    JArray array = json["data"]["data"] as JArray;
                    string sql = "";
                    for (int i = 0; i < array.Count; i++)
                    {
                      
                        #region 表头数据
                        //胜途订单id
                        string  id= array[i]["id"].ToString();
                        //网上订单号
                        string site_trade_id = array[i]["site_trade_id"].ToString();
                        //网店名称
                        string site_name = array[i]["wh_code"].ToString();
                        //网店账号
                        string site_user = array[i]["site_user"].ToString();
                        //平台类型
                        string site_type = array[i]["site_type"].ToString();
                        //发货仓库编码
                        string wh_code = array[i]["wh_code"].ToString();
                        //仓库名称
                        string wh_name = array[i]["wh_name"].ToString();
                        //销售出库单号
                        string sal_bilno = array[i]["sal_bilno"].ToString();
                        //发货时间
                        string send_date = array[i]["send_date"].ToString();
                        //方法
                        string method = param.method;
                        //推送状态
                        string IsPush = "0";
                        DateTime PullTime = DateTime.Now;
                        //平台类型id
                        string site_type_id = array[i]["site_type_id"].ToString();
                        string cur_code = "";
                        if (array[i].ToString().Contains("cur_code"))
                        {
                            cur_code = array[i]["cur_code"].ToString();
                        }
                        decimal exc_rto = 0;
                        if (array[i].ToString().Contains("exc_rto"))
                        {
                            exc_rto = Convert.ToDecimal(array[i]["exc_rto"].ToString());
                        }
                        string buyer_nick = "";
                        //if (array[i].ToString().Contains("buyer_nick"))
                        //{
                        //    buyer_nick = array[i]["buyer_nick"].ToString();
                        //}
                        string receiver_country = "";
                        if (array[i].ToString().Contains("receiver_country"))
                        {
                            receiver_country = array[i]["receiver_country"].ToString().Replace("'", ""); ;
                        }
                        string logistics_name = "";
                        if (array[i].ToString().Contains("logistics_name"))
                        {
                            logistics_name = array[i]["logistics_name"].ToString().Replace("'", ""); ;
                        }
                        string logistics_no = "";
                        if (array[i].ToString().Contains("logistics_no"))
                        {
                            logistics_no = array[i]["logistics_no"].ToString().Replace("'", ""); ;
                        }
                        string war_out_code = "";
                        if (array[i].ToString().Contains("war_out_code"))
                        {
                            war_out_code = array[i]["war_out_code"].ToString();
                        }
                        decimal amtn = 0;
                        if (array[i].ToString().Contains("amtn"))
                        {
                            amtn = Convert.ToDecimal(array[i]["amtn"].ToString());
                        }
                        decimal discount_fee = 0;
                        if (array[i].ToString().Contains("discount_fee"))
                        {
                            var item = array[i]["discount_fee"];
                            if (item != null)
                            {
                                discount_fee = Convert.ToDecimal(array[i]["discount_fee"].ToString());
                            }
                            
                        }
                        decimal sellings_fee = 0;
                        if (array[i].ToString().Contains("sellings_fee"))
                        {
                            var item = array[i]["sellings_fee"];
                            if (item != null)
                            {
                                sellings_fee = Convert.ToDecimal(array[i]["sellings_fee"].ToString());
                            }
                        }
                        decimal accounts_fee = 0;
                        if (array[i].ToString().Contains("accounts_fee"))
                        {
                            var item = array[i]["accounts_fee"];
                            if (item != null)
                            {
                                accounts_fee = Convert.ToDecimal(array[i]["accounts_fee"].ToString());
                            }
                        }
                        decimal adjust_fee = 0;
                        if (array[i].ToString().Contains("adjust_fee"))
                        {
                            var item = array[i]["adjust_fee"];
                            if (item != null)
                            {
                                adjust_fee = Convert.ToDecimal(array[i]["adjust_fee"].ToString());
                            }
                        }
                        decimal post_fee = 0;
                        if (array[i].ToString().Contains("post_fee"))
                        {
                            var item = array[i]["post_fee"];
                            if (item != null)
                            {
                                post_fee = Convert.ToDecimal(array[i]["post_fee"].ToString());
                            }
                        }
                        string order_type = "";
                        if (array[i].ToString().Contains("order_type"))
                        {
                            order_type = array[i]["order_type"].ToString();
                        }
                        string chk_date= "";
                        string dept_code_top = "";
                        if (array[i].ToString().Contains("dept_code_top"))
                        {
                            dept_code_top = array[i]["dept_code_top"].ToString();
                        }
                        string dept_code = "";
                        if (array[i].ToString().Contains("dept_code"))
                        {
                            dept_code = array[i]["dept_code"].ToString();
                        }
                        string dept_name = "";
                        if (array[i].ToString().Contains("dept_name"))
                        {
                            dept_name = array[i]["dept_name"].ToString();
                        }
                        string is_declare = "";
                        if (array[i].ToString().Contains("is_declare"))
                        {
                            is_declare = array[i]["is_declare"].ToString();
                        }
                        string seller_memo = "";
                        if (array[i].ToString().Contains("seller_memo"))
                        {
                            seller_memo = array[i]["seller_memo"].ToString().Replace("'", ""); ;
                        }
                        sql += string.Format(@"/*dialect*/ if not exists (select  *  from ZWLF_T_SaleOut where id ='{0}' and site_trade_id='{1}') 
                                                begin INSERT INTO [dbo].[ZWLF_T_Saleout]
                                                  ([id]
                                                  ,[site_trade_id]
                                                  ,[site_name]
                                                  ,[site_user]
                                                  ,[site_type]
                                                  ,[wh_code]
                                                  ,[wh_name]
                                                  ,[sal_bilno]
                                                  ,[send_date]
                                                  ,[method]
                                                  ,[IsPush]
                                                  ,[PullTime]
                                                  ,[site_type_id]
                                                  ,[cur_code]
                                                  ,[exc_rto]
                                                  ,[buyer_nick]
                                                  ,[receiver_country]
                                                  ,[logistics_name]
                                                  ,[logistics_no]
                                                  ,[war_out_code]
                                                  ,[amtn]
                                                  ,[discount_fee]
                                                  ,[sellings_fee]
                                                  ,[accounts_fee]
                                                  ,[adjust_fee]
                                                  ,[post_fee]
                                                  ,[order_type]
                                                  ,[chk_date]
                                                  ,[dept_code_top]
                                                  ,[dept_code]
                                                  ,[dept_name]
                                                  ,[is_declare]
                                                  ,[seller_memo])
                                            VALUES
                                                  ('{0}'
                                                  ,'{1}'
                                                  ,'{2}'
                                                  ,'{3}'
                                                  ,'{4}'
                                                  ,'{5}'
                                                  ,'{6}'
                                                  ,'{7}'
                                                  ,'{8}'
                                                  ,'{9}'
                                                  ,'{10}'
                                                  ,'{11}'
                                                  ,'{12}'
                                                  ,'{13}'
                                                  ,'{14}'
                                                  ,'{15}'
                                                  ,'{16}'
                                                  ,'{17}'
                                                  ,'{18}'
                                                  ,'{19}'
                                                  ,{20}
                                                  ,{21}
                                                  ,{22}
                                                  ,{23}
                                                  ,{24}
                                                  ,{25}
                                                  ,'{26}'
                                                  ,'{27}'
                                                  ,'{28}'
                                                  ,'{29}'
                                                  ,'{30}'
                                                  ,'{31}'
                                                  ,'{32}') end;", id, site_trade_id, site_name, site_user, site_type, wh_code,
                                                  wh_name, sal_bilno, send_date, method, IsPush, PullTime, site_type_id, cur_code, exc_rto, buyer_nick,
                                                  receiver_country, logistics_name, logistics_no, war_out_code, amtn, discount_fee, sellings_fee, accounts_fee,
                                                  adjust_fee, post_fee, order_type, chk_date, dept_code_top, dept_code, dept_name, is_declare, seller_memo
                                                  );

                        #endregion
                        #region  表体
                        //表体（一个订单可能又多个物料）
                        JArray details = array[i]["details"] as JArray;
                        for (int j = 0; j < details.Count; j++)
                        {
                            string detailid = "";
                            if (details[j].ToString().Contains("id"))
                            {
                                detailid = details[j]["id"].ToString();
                            }
                            string   OrderId = id;
                            string prdt_id = "";
                            if (details[j].ToString().Contains("prdt_id"))
                            {
                                prdt_id = details[j]["prdt_id"].ToString();
                            }
                            string prdt_code = "";
                            if (details[j].ToString().Contains("prdt_code"))
                            {
                                prdt_code = details[j]["prdt_code"].ToString();
                            }
                            string prdt_name = "";
                            if (details[j].ToString().Contains("prdt_name"))
                            {
                                prdt_name = details[j]["prdt_name"].ToString().Replace("'", ""); 
                            }
                            string spc = "";
                            if (details[j].ToString().Contains("spc"))
                            {
                                spc = details[j]["spc"].ToString().Replace("'", ""); 
                            }
                            decimal qty = 0;
                            if (details[j].ToString().Contains("qty"))
                            {
                                qty =Convert.ToDecimal(details[j]["qty"].ToString());
                            }
                            string unit_name = "";
                            if (details[j].ToString().Contains("unit_name"))
                            {
                                unit_name =details[j]["unit_name"].ToString().Replace("'", ""); 
                            }
                            decimal up = 0;
                            if (details[j].ToString().Contains("up"))
                            {
                                up = Convert.ToDecimal(details[j]["up"].ToString());
                            }
                            decimal details_amtn = 0;
                            if (details[j].ToString().Contains("amtn"))
                            {
                                details_amtn = Convert.ToDecimal(details[j]["amtn"].ToString());
                            }
                            decimal real_amtn = 0;
                            if (details[j].ToString().Contains("real_amtn"))
                            {
                                real_amtn = Convert.ToDecimal(details[j]["real_amtn"].ToString());
                            }
                            decimal prdt_tax_fee = 0;
                            if (details[j].ToString().Contains("prdt_tax_fee"))
                            {
                                var item = details[j]["prdt_tax_fee"];
                                if (item != null)
                                {
                                    prdt_tax_fee = Convert.ToDecimal(details[j]["prdt_tax_fee"].ToString());
                                }
                            }
                            decimal prdt_freight_tax_fee = 0;
                            if (details[j].ToString().Contains("prdt_freight_tax_fee"))
                            {
                                var item = details[j]["prdt_freight_tax_fee"];
                                if (item != null)
                                {
                                    prdt_freight_tax_fee = Convert.ToDecimal(details[j]["prdt_freight_tax_fee"].ToString());
                                }
                            }
                            decimal shipping_price = 0;
                            if (details[j].ToString().Contains("shipping_price"))
                            {
                                var item = details[j]["shipping_price"];
                                if (item != null)
                                {
                                    shipping_price = Convert.ToDecimal(details[j]["shipping_price"].ToString());
                                }
                            }
                            decimal shipping_discount = 0;
                            if (details[j].ToString().Contains("shipping_discount"))
                            {
                                var item = details[j]["shipping_discount"];
                                if (item != null)
                                {
                                    shipping_discount = Convert.ToDecimal(details[j]["shipping_discount"].ToString());
                                }
                            }
                            decimal promotion_discount = 0;
                            if (details[j].ToString().Contains("promotion_discount"))
                            {
                                var item = details[j]["promotion_discount"];
                                if (item != null)
                                {
                                    promotion_discount = Convert.ToDecimal(details[j]["promotion_discount"].ToString());
                                }
                            }
                            decimal prdt_wrap_tax = 0;
                            if (details[j].ToString().Contains("prdt_wrap_tax"))
                            {
                                var item = details[j]["prdt_wrap_tax"];
                                if (item != null)
                                {
                                    prdt_wrap_tax = Convert.ToDecimal(details[j]["prdt_wrap_tax"].ToString());
                                }
                            }
                            string is_free = "";
                            if (details[j].ToString().Contains("is_free"))
                            {
                                is_free = details[j]["is_free"].ToString();
                            }
                            decimal details_discount_fee = 0;
                            if (details[j].ToString().Contains("discount_fee"))
                            {
                                var item = details[j]["discount_fee"];
                                if (item != null)
                                {
                                    details_discount_fee = Convert.ToDecimal(details[j]["discount_fee"].ToString());
                                }
                            }
                            decimal promotion_rebates_tax = 0;
                            if (details[j].ToString().Contains("promotion_rebates_tax"))
                            {
                                var item = details[j]["promotion_rebates_tax"];
                                if (item != null)
                                {
                                    promotion_rebates_tax = Convert.ToDecimal(details[j]["promotion_rebates_tax"].ToString());
                                }
                            }
                            string refund_status = "";
                            if (details[j].ToString().Contains("refund_status"))
                            {
                                refund_status = details[j]["refund_status"].ToString();
                            }
                            decimal post_fee_trade = 0;
                            if (details[j].ToString().Contains("post_fee_trade"))
                            {
                                var item = details[j]["post_fee_trade"];
                                if (item != null)
                                {
                                    post_fee_trade = Convert.ToDecimal(details[j]["post_fee_trade"].ToString());
                                }
                            }
                            decimal accounts_fee_trade = 0;
                            if (details[j].ToString().Contains("accounts_fee_trade"))
                            {
                                var item = details[j]["accounts_fee_trade"];
                                if (item != null)
                                {
                                    accounts_fee_trade = Convert.ToDecimal(details[j]["accounts_fee_trade"].ToString());
                                }
                            }
                            decimal sellings_fee_trade = 0;
                            if (details[j].ToString().Contains("sellings_fee_trade"))
                            {
                                var item = details[j]["sellings_fee_trade"];
                                if (item != null)
                                {
                                    sellings_fee_trade = Convert.ToDecimal(details[j]["sellings_fee_trade"].ToString());
                                }
                            }
                            decimal discount_fee_trade = 0;
                            if (details[j].ToString().Contains("discount_fee_trade"))
                            {
                                var item = details[j]["discount_fee_trade"];
                                if (item != null)
                                {
                                    discount_fee_trade = Convert.ToDecimal(details[j]["discount_fee_trade"].ToString());
                                } 
                            }
                            decimal adjust_fee_trade = 0;
                            if (details[j].ToString().Contains("adjust_fee_trade"))
                            {
                                var item = details[j]["adjust_fee_trade"];
                                if (item != null)
                                {
                                    adjust_fee_trade = Convert.ToDecimal(details[j]["adjust_fee_trade"].ToString());
                                }
                            }
                            string status = "";
                            if (details[j].ToString().Contains("status"))
                            {
                                status = details[j]["status"].ToString();
                            }
                            sql += string.Format(@"/*dialect*/ if not exists (select  *  from ZWLF_T_SaleOutEntry where id ='{0}' and OrderId='{1}') 
                                                        begin 
                                                        INSERT INTO [dbo].[ZWLF_T_SaleOutEntry]
                                                                   ([id]
                                                                   ,[OrderId]
                                                                   ,[prdt_id]
                                                                   ,[prdt_code]
                                                                   ,[prdt_name]
                                                                   ,[spc]
                                                                   ,[qty]
                                                                   ,[unit_name]
                                                                   ,[up]
                                                                   ,[amtn]
                                                                   ,[real_amtn]
                                                                   ,[prdt_tax_fee]
                                                                   ,[prdt_freight_tax_fee]
                                                                   ,[shipping_price]
                                                                   ,[shipping_discount]
                                                                   ,[promotion_discount]
                                                                   ,[prdt_wrap_tax]
                                                                   ,[is_free]
                                                                   ,[discount_fee]
                                                                   ,[promotion_rebates_tax]
                                                                   ,[refund_status]
                                                                   ,[post_fee_trade]
                                                                   ,[accounts_fee_trade]
                                                                   ,[sellings_fee_trade]
                                                                   ,[discount_fee_trade]
                                                                   ,[adjust_fee_trade]
                                                                   ,[status])
                                                             VALUES
                                                                   ('{0}'
                                                                   ,'{1}'
                                                                   ,'{2}'
                                                                   ,'{3}'
                                                                   ,'{4}'
                                                                   ,'{5}'
                                                                   ,{6}
                                                                   ,'{7}'
                                                                   ,{8}
                                                                   ,{9}
                                                                   ,{10}
                                                                   ,{11}
                                                                   ,{12}
                                                                   ,{13}
                                                                   ,{14}
                                                                   ,{15}
                                                                   ,{16}
                                                                   ,'{17}'
                                                                   ,{18}
                                                                   ,{19}
                                                                   ,'{20}'
                                                                   ,{21}
                                                                   ,{22}
                                                                   ,{23}
                                                                   ,{24}
                                                                   ,{25}
                                                                   ,'{26}') end ;",detailid, OrderId, prdt_id, prdt_code, prdt_name, spc, qty,
                                                                   unit_name, up, details_amtn, real_amtn, prdt_tax_fee, prdt_freight_tax_fee, shipping_price,
                                                                   shipping_discount, promotion_discount, prdt_wrap_tax, is_free, details_discount_fee, promotion_rebates_tax,
                                                                   refund_status, post_fee_trade, accounts_fee_trade, sellings_fee_trade, discount_fee_trade, adjust_fee_trade, status
                                                                   );

                        }
                        #endregion
                    }
                    //插入数据库
                    if (sql != "")
                    {
                        DBServiceHelper.Execute(context, sql);
                    }
                    #endregion

                    msg.status = true;
                    msg.sum = array.Count;
                }
                return msg;
            }
            catch (KDException ex)
            {
                //记录每一页获取情况
                msg.status = false;
                msg.result = ex.ToString().Substring(0, 500).Replace("'", "");
                return msg;
            }
        }
    }
}
