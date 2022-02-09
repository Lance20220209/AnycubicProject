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
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;


namespace AnyCubicK3cloudProject
{
    [Description("根据仓库，店铺id获取订单")]
    [Kingdee.BOS.Util.HotUpdate]
    public class ZWLF_GetOrderScheduleService : IScheduleService
    {

        /// <summary>
        /// 定时任务
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="schedule"></param>
        public void Run(Kingdee.BOS.Context ctx, Schedule schedule)
        {
            try
            {
                Context context = ctx;
               
                ZWLF_GetAmazonOrder zWLF_GetAmazonOrder = new ZWLF_GetAmazonOrder();
                ZWLF_GetSaleOrder zWLF_GetSaleOrder = new ZWLF_GetSaleOrder();
                ZWLF_HKGetSaleOrder zWLF_HKGetSaleOrder = new ZWLF_HKGetSaleOrder();
                //配置表
                string sql = string.Format(@"/*dialect*/ select  app_key,AppSecret,date_type,end_date,page_no,page_size,
                                     username,password,url,wh_code ,method,start_date,access_token,site_id,site_type
                                    from ZWLF_T_Configuration_New where IsDisable=0");
                DataSet ds = DBServiceHelper.ExecuteDataSet(ctx, sql);
                DataTable dt = ds.Tables[0];
                if (dt.Rows.Count > 0)
                {
                    Parameters parameters = new Parameters();
                    for (int i = 0; i < dt.Rows.Count; i++)
                    {
                        Msg msg = new Msg();
                        parameters.app_key = dt.Rows[i]["app_key"].ToString();
                        parameters.AppSecret = dt.Rows[i]["AppSecret"].ToString();
                        parameters.page_size = dt.Rows[i]["page_size"].ToString();
                        parameters.username = dt.Rows[i]["username"].ToString();
                        parameters.password = dt.Rows[i]["password"].ToString();
                        parameters.page_no = Convert.ToInt32(dt.Rows[i]["page_no"].ToString());
                        parameters.url = dt.Rows[i]["url"].ToString();
                        parameters.start_date = dt.Rows[i]["start_date"].ToString();
                        parameters.end_date = dt.Rows[i]["end_date"].ToString();
                        parameters.method= dt.Rows[i]["method"].ToString();
                        parameters.access_token = dt.Rows[i]["access_token"].ToString();
                        parameters.wh_code = dt.Rows[i]["wh_code"].ToString();
                        parameters.site_id = dt.Rows[i]["site_id"].ToString();
                        //本地仓库
                        if (dt.Rows[i]["site_type"].ToString() == "1")
                        {
                            msg = zWLF_GetSaleOrder.SaleTradeGet(parameters,ctx);
                            if (msg.status)
                            {
                                for (int a = 0; a < 1000; a++)
                                {
                                    if (msg.sum == Convert.ToInt32(dt.Rows[i]["page_size"].ToString()))
                                    {
                                        parameters.page_no = parameters.page_no + 1;
                                        msg = zWLF_GetSaleOrder.SaleTradeGet(parameters, ctx);
                                    }
                                    else
                                    {
                                        a = 1000;
                                        break;
                                    }
                                }

                            }
                        }
                        //亚马逊
                        else if (dt.Rows[i]["site_type"].ToString() == "2")
                        {
                            msg = zWLF_GetAmazonOrder.SaleTradeGet(parameters, ctx);
                            if (msg.status)
                            {
                                for (int a = 0; a < 1000; a++)
                                {
                                    if (msg.sum == Convert.ToInt32(dt.Rows[i]["page_size"].ToString()))
                                    {
                                        parameters.page_no = parameters.page_no + 1;
                                        msg = zWLF_GetAmazonOrder.SaleTradeGet(parameters, ctx);
                                    }
                                    else
                                    {
                                        a = 1000;
                                        break;
                                    }
                                }

                            }
                        }
                        //海外平台
                        else if (dt.Rows[i]["site_type"].ToString() == "3")
                        {
                            msg = zWLF_HKGetSaleOrder.SaleTradeGet(parameters, ctx);
                            if (msg.status)
                            {
                                for (int a = 0; a < 1000; a++)
                                {
                                    if (msg.sum == Convert.ToInt32(dt.Rows[i]["page_size"].ToString()))
                                    {
                                        parameters.page_no = parameters.page_no + 1;
                                        msg = zWLF_HKGetSaleOrder.SaleTradeGet(parameters, ctx);
                                    }
                                    else
                                    {
                                        a = 1000;
                                        break;
                                    }
                                }

                            }
                        }
                    }
                }
            }
            catch (KDException ex)
            {
                throw new KDException("", ex.ToString());
            }
        }
    }
}
