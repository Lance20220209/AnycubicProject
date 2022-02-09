using Kingdee.BOS;
using Kingdee.BOS.Core.List;
using Kingdee.BOS.Core.List.PlugIn;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.ServiceHelper;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnyCubicK3cloudProject
{
    [Description("重新获取失败的退货单")]
    [Kingdee.BOS.Util.HotUpdate]
    public class ZWLF_GetFailSaleReturn : AbstractListPlugIn
    {
        public override void BarItemClick(Kingdee.BOS.Core.DynamicForm.PlugIn.Args.BarItemClickEventArgs e)
        {
            base.BarItemClick(e);
            if (e.BarItemKey.Equals("ZWLF_tbGetfailReturn"))
            {
                string mssg = "";
                Context context = this.Context;
                string sql = "";
                //选择的行,获取所有信息,放在listcoll里面
                ListSelectedRowCollection listcoll = this.ListView.SelectedRowsInfo;
                if (listcoll.Count > 0)
                {
                    //接收返回的数组值
                    string[] listKey = listcoll.GetPrimaryKeyValues();
                    string ids = string.Join("','", listKey);
                    ZWLF_GetSalesReturn wLF_GetSalesReturn = new ZWLF_GetSalesReturn();
                    STLib sTLib = new STLib();
                    Parameters parameters = new Parameters();
                    Msg msg = new Msg();
                    sql = string.Format(@"/*dialect*/ select  top 1 app_key,AppSecret,date_type,end_date,page_no,page_size,
                                     username,password,url,wh_code ,method,start_date,access_token
                                    from ZWLF_T_Configuration where  IsDisable=0;");
                    sql += string.Format(@"/*dialect*/ select distinct  F_ZWLF_ID ,F_ZWLF_SourceType from  ZWLF_T_OrderLog 
                                                       where F_ZWLF_SourceType in('04','07') and (F_ZWLF_QRSTATE='0' 
                                                       or F_ZWLF_Orderstate='0') and  FID in ('{0}')", ids);
                    DataSet ds = DBServiceHelper.ExecuteDataSet(context, sql);
                    DataTable dt_C = ds.Tables[0];
                    DataTable dt_L = ds.Tables[1];
                    if (dt_C.Rows.Count > 0)
                    {
                        for (int j = 0; j < dt_C.Rows.Count; j++)
                        {
                            parameters.app_key = dt_C.Rows[j]["app_key"].ToString();
                            parameters.AppSecret = dt_C.Rows[j]["AppSecret"].ToString();
                            parameters.page_size = dt_C.Rows[j]["page_size"].ToString();
                            parameters.username = dt_C.Rows[j]["username"].ToString();
                            parameters.password = dt_C.Rows[j]["password"].ToString();
                            DateTime Btime = Convert.ToDateTime(dt_C.Rows[j]["start_date"].ToString()).AddDays(-60);
                            parameters.start_date = Btime.ToString();
                            parameters.end_date = dt_C.Rows[j]["end_date"].ToString();
                            parameters.wh_code = "";
                            parameters.page_no = Convert.ToInt32(dt_C.Rows[j]["page_no"].ToString());
                            parameters.url = dt_C.Rows[j]["url"].ToString();
                            //获取token
                            //string access_token = sTLib.GetAccessToken(parameters.AppSecret, parameters.app_key, parameters.username, parameters.password);
                            parameters.access_token = dt_C.Rows[j]["access_token"].ToString();
                        }
                    }

                    if (dt_L.Rows.Count > 0)
                    {
                        //循环订单
                        for (int i = 0; i < dt_L.Rows.Count; i++)
                        {
                            //胜途订单唯一标识
                            string F_ZWLF_ID = dt_L.Rows[i]["F_ZWLF_ID"].ToString().Trim();
                            string F_ZWLF_SourceType = dt_L.Rows[i]["F_ZWLF_SourceType"].ToString().Trim();
                            if (F_ZWLF_SourceType == "04")
                            {
                                parameters.method = "frdas.erp.refunds.get";
                            }
                            else
                            {
                                parameters.method = "frdas.erp.refunds.getcloud";
                            }
                            parameters.id = F_ZWLF_ID;
                            try
                            {
                                msg = wLF_GetSalesReturn.SaleReturnGet(parameters, context);
                                mssg = "重新获取：" + msg.result;
                                sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_NOTE='{0}'   where F_ZWLF_ID='{1}';", mssg, F_ZWLF_ID);
                                DBServiceHelper.Execute(context, sql);
                            }
                            catch (Exception ex)
                            {
                                mssg = ex.ToString().Substring(0, 500);
                                //记录生成订单成功
                                sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_NOTE='{0}'  where F_ZWLF_ID='{1}';", mssg, F_ZWLF_ID);
                                DBServiceHelper.Execute(context, sql);
                            }
                        }
                    }
                    else
                    {
                        this.View.ShowErrMessage("请选择胜途单据类型为胜途退货单，并且生成金蝶退货单状态是失败的数据行！");
                        return;
                    }
                }
                else
                {
                    this.View.ShowErrMessage("未选择数据行！");
                    return;
                }
                this.View.ShowMessage("重新获取失败的退货单成功");
            }
        }
    }
}
