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
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;

namespace AnyCubicK3cloudProject
{
    [Description("获取胜途第三方仓销售订单定时任务插件")]
    [Kingdee.BOS.Util.HotUpdate]
    public class ZWLF_HKGetSaleOrderIScheduleService : IScheduleService
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
                ZWLF_HKGetSaleOrder zWLF_HKGetSale = new ZWLF_HKGetSaleOrder();
                STLib sTLib = new STLib();
                Context context = ctx;
                Msg msg = new Msg();
                //配置表
                string sql = string.Format(@"/*dialect*/ select  app_key,AppSecret,date_type,end_date,page_no,page_size,
                                     username,password,url,wh_code ,method,start_date,access_token
                                    from ZWLF_T_Configuration where id=29 and IsDisable=0");
                DataSet ds = DBServiceHelper.ExecuteDataSet(ctx, sql);
                DataTable dt = ds.Tables[0];
                if (dt.Rows.Count > 0)
                {
                    for (int i = 0; i < dt.Rows.Count; i++)
                    {
                        Parameters parameters = new Parameters();
                        parameters.app_key = dt.Rows[i]["app_key"].ToString();
                        parameters.AppSecret = dt.Rows[i]["AppSecret"].ToString();
                        parameters.page_size = dt.Rows[i]["page_size"].ToString();
                        parameters.username = dt.Rows[i]["username"].ToString();
                        parameters.password = dt.Rows[i]["password"].ToString();
                        DateTime Btime = Convert.ToDateTime(dt.Rows[i]["start_date"].ToString());
                        parameters.start_date = Btime.ToString();
                        DateTime Etime = DateTime.Now;
                        parameters.end_date = dt.Rows[i]["end_date"].ToString();
                        parameters.page_no = Convert.ToInt32(dt.Rows[i]["page_no"].ToString());
                        parameters.url = dt.Rows[i]["url"].ToString();
                        parameters.method = dt.Rows[i]["method"].ToString();
                        parameters.id = "";
                        //获取token
                        parameters.access_token = dt.Rows[i]["access_token"].ToString();
                        msg = zWLF_HKGetSale.SaleTradeGet(parameters, context);
                        if (msg.status)
                        {
                            for (int a = 0; a < 1000; a++)
                            {
                                if (msg.sum == Convert.ToInt32(dt.Rows[i]["page_size"].ToString()))
                                {
                                    parameters.page_no = parameters.page_no + 1;
                                    msg = zWLF_HKGetSale.SaleTradeGet(parameters, context);
                                }
                                else
                                {
                                    a = 1000;
                                    break;
                                }
                            }
                        }
                        Btime = Convert.ToDateTime(dt.Rows[i]["end_date"].ToString());
                        Etime = Convert.ToDateTime(dt.Rows[i]["end_date"].ToString()).AddHours(1);
                        //更新获取时间
                        sql = string.Format(@"/*dialect*/ update ZWLF_T_Configuration set start_date='{0}',end_date='{1}' 
                                         where  method='frdas.erp.saleout.get' and  id=29 ", Btime, Etime);
                        DBServiceHelper.Execute(context, sql);
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
