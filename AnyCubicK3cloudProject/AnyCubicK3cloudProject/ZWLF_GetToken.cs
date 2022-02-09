using Kingdee.BOS;
using Kingdee.BOS.Core.List;
using Kingdee.BOS.Core.List.PlugIn;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.ServiceHelper;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;

namespace AnyCubicK3cloudProject
{
    [Description("重新获取授权")]
    [Kingdee.BOS.Util.HotUpdate]
    public  class ZWLF_GetToken : AbstractListPlugIn
    {
        public override void BarItemClick(Kingdee.BOS.Core.DynamicForm.PlugIn.Args.BarItemClickEventArgs e)
        {
            base.BarItemClick(e);
            if (e.BarItemKey.Equals("ZWLF_tbGetToken"))
            {
                if(this.Context.UserId == 157357)
                {
                    try
                    {
                        STLib sTLib = new STLib();
                        //获取token
                        string sql = string.Format(@"/*dialect*/ select  top 1 app_key,AppSecret,username,password,tokenUrl,redirect_uri,CodeUrl
                                    from ZWLF_T_Configuration where IsDisable=0;");
                        DataSet ds = DBServiceHelper.ExecuteDataSet(Context, sql);
                        DataTable dt_C = ds.Tables[0];
                        if (dt_C.Rows.Count > 0)
                        {
                            for (int j = 0; j < dt_C.Rows.Count; j++)
                            {
                                string app_key = dt_C.Rows[j]["app_key"].ToString();
                                string AppSecret = dt_C.Rows[j]["AppSecret"].ToString();
                                string username = dt_C.Rows[j]["username"].ToString();
                                string password = dt_C.Rows[j]["password"].ToString();
                                string tokenUrl = dt_C.Rows[j]["tokenUrl"].ToString();
                                string redirect_uri = dt_C.Rows[j]["redirect_uri"].ToString();
                                string CodeUrl = dt_C.Rows[j]["CodeUrl"].ToString();
                                //获取token
                                string access_token = sTLib.GetAccessToken(AppSecret, app_key, username, password, tokenUrl, redirect_uri, CodeUrl);
                                string upsql = string.Format(@"/*dialect*/ update ZWLF_T_Configuration set getTokenTime=GETDATE(),access_token='{0}'", access_token);
                                DBServiceHelper.Execute(Context, upsql);
                            }
                        }
                        this.View.ShowMessage("获取授权成功");
                    }
                    catch (Exception ex)
                    {
                        this.View.ShowErrMessage(ex.ToString());
                    }
                }
                else
                {
                    this.View.ShowErrMessage("当前用户没有权限获取授权！");
                }
            }
        }
    }
}
