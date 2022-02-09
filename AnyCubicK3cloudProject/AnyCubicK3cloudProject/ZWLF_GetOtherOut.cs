using System;
using System.Collections.Generic;
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
    public class ZWLF_GetOtherOut
    {
        /// <summary>
        /// 调用胜途其他入库接口
        /// </summary>
        /// <param name="param"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public Msg GetOtherOut(Parameters param, Context context)
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
                @params["date_type"] = "1";
                DateTime Btime = Convert.ToDateTime(param.start_date);
                @params["start_date"] = Btime.ToString("yyyy-MM-dd HH:mm:ss");
                DateTime Etime = Convert.ToDateTime(param.end_date);
                @params["end_date"] = Etime.ToString("yyyy-MM-dd HH:mm:ss");
                @params["page_size"] = param.page_size;
                @params["page_no"] = param.page_no.ToString();
                @params["id"] = param.id; //订单唯一标识，默认为空
                @params["site_id"] = "";// 店铺ID
                @params["wh_code"] = param.wh_code;// 仓库编码
                Httpclient httpclient = new Httpclient();
                string json = httpclient.ZWLFRequstPost(@params, param.url, param.AppSecret);
                JObject OrderJson = JsonConvert.DeserializeObject<JObject>(json);
                string code = OrderJson["code"].ToString();
                if (code == "200")
                {
                    int total_count = Convert.ToInt32(OrderJson["data"]["total_count"].ToString()); //本次返回的订单数
                    if (total_count > 0)
                    {
                        //记录每一页获取情况
                        string Insql = string.Format(@"INSERT INTO ZWLF_T_GetOrderCondition
                                                 ([Fmethod]
                                                 ,[Fdate_type]
                                                 ,[Fstart_date]
                                                 ,[Fend_date]
                                                 ,[Fpage_size]
                                                 ,[Fpage_no]
                                                 ,[FIsSuccess]
                                                 ,[FNote]
                                                  ,FStock)
                                           VALUES
                                                 ('{0}'
                                                 ,'1'
                                                 ,'{1}'
                                                 ,'{2}'
                                                 ,{3}
                                                 ,{4}
                                                 ,'1'
                                                 ,'{5}' 
                                                 ,'{6}' )", param.method, Btime, Etime, Convert.ToInt32(param.page_size), param.page_no, json.Replace("'", ""), param.wh_code);
                        DBServiceHelper.Execute(context, Insql);
                        //其他出库单
                        string sql = string.Format(@"/*dialect*/ select  distinct FID,F_ZWLF_OUTNO ,F_ZWLF_ID from T_STK_MISDELIVERY where  F_ZWLF_OUTNO!='' and  FSTOCKORGID=165223");
                        //仓库
                        sql += string.Format(@"/*dialect*/ select FUSEORGID, FNUMBER,F_ZWLF_WAREHOUSECODE  from  ZWLF_t_Cust_StockEntry a 
                                           inner join t_BD_Stock b on a.FStockId=b.FSTOCKID where F_ZWLF_DISABLE=0  ;");
                        //物料
                        sql += string.Format(@"/*dialect*/ select FISBATCHMANAGE,FISINVENTORY,a.FUSEORGID ,FNUMBER ,FOLDNUMBER , FISBATCHMANAGE from T_BD_MATERIAL a
                                                          inner join t_BD_MaterialStock c on a.FMATERIALID=c.FMATERIALID
                                                         inner join t_BD_MaterialBase b  on a.FMATERIALID=b.FMATERIALID where a.FUSEORGID=165223");
                        DataSet ds = DBServiceHelper.ExecuteDataSet(context, sql);
                        //生成其他入库单
                        msg = GenerateK3OrderOut(OrderJson, ds, context);
                    }
                }
                else
                {
                    //记录每一页获取情况
                    string Insql = string.Format(@"INSERT INTO ZWLF_T_GetOrderCondition
                                                 ([Fmethod]
                                                 ,[Fdate_type]
                                                 ,[Fstart_date]
                                                 ,[Fend_date]
                                                 ,[Fpage_size]
                                                 ,[Fpage_no]
                                                 ,[FIsSuccess]
                                                  ,FNote
                                                  ,FStock)
                                           VALUES
                                                 ('{0}'
                                                 ,'1'
                                                 ,'{1}'
                                                 ,'{2}'
                                                 ,{3}
                                                 ,{4}
                                                 ,'0'
                                                 ,'{5}'
                                                 ,'{6}' )", param.method, param.end_date, Etime, Convert.ToInt32(param.page_size), param.page_no, OrderJson.ToString().Replace("'", ""), param.wh_code);
                    DBServiceHelper.Execute(context, Insql);
                    msg.status = false;
                }
                return msg;
            }
            catch (KDException ex)
            {
                //记录每一页获取情况
                msg.status = false;
                msg.result = ex.ToString().Substring(0, 500).Replace("'", "");
                string Insql = string.Format(@"INSERT INTO ZWLF_T_GetOrderCondition
                                                 ([Fmethod]
                                                 ,[Fdate_type]
                                                 ,[Fstart_date]
                                                 ,[Fend_date]
                                                 ,[Fpage_size]
                                                 ,[Fpage_no]
                                                 ,[FIsSuccess]
                                                 ,[FNote]
                                                 ,FStock)
                                           VALUES
                                                 ('{0}'
                                                 ,'1'
                                                 ,'{1}'
                                                 ,'{2}'
                                                 ,{3}
                                                 ,{4}
                                                 ,'0'
                                                 ,'{5}'
                                                 ,'{6}')",
                                                 param.method, param.start_date, param.end_date, Convert.ToInt32(param.page_size), param.page_no, msg.result, param.wh_code);
                DBServiceHelper.Execute(context, Insql);
                return msg;
            }
        }


        /// <summary>
        /// 生成其他出库单
        /// </summary>
        /// <param name="json"></param>
        /// <param name="ds"></param>
        /// <param name="context"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public Msg GenerateK3OrderOut(JObject json, DataSet ds, Context context)
        {
            Msg msg = new Msg();
            DataTable QtCK = ds.Tables[0];
            DataTable Ckdt = ds.Tables[1];
            DataTable Wldt = ds.Tables[2];
            int total_count = Convert.ToInt32(json["data"]["total_count"].ToString()); //本次返回的订单数
            JArray array = json["data"]["data"] as JArray;
            for (int i = 0; i < array.Count; i++)
            {
                string F_ZWLF_SOURCETYPE = "09";
                string sql = "";
                string id = array[i]["id"].ToString();
                string bil_no = array[i]["bil_no"].ToString();
                string chk_date = array[i]["chk_date"].ToString();
                string wh_code = array[i]["wh_code"].ToString();
                bool checkstatus = true;
                //先判断是否海外仓的
                DataRow[] CKRow = Ckdt.Select("F_ZWLF_WAREHOUSECODE='" + wh_code + "'");
                string CKFnumber = "";
                string FUSEORGID = "";
                string checkMsg = "";
                if (CKRow.Length > 0)
                {
                    foreach (DataRow dr in CKRow)
                    {
                        CKFnumber = dr["FNUMBER"].ToString();
                        FUSEORGID = dr["FUSEORGID"].ToString();
                    }
                }
                else
                {
                    checkstatus = false;
                    checkMsg += "仓库编码不存在：" + CKFnumber;
                }
                if (checkstatus)
                {
                    //只有海外仓才需要生成
                    if (FUSEORGID == "165223")
                    {
                        DataRow[] QTRow = QtCK.Select("F_ZWLF_OUTNO='" + bil_no + "'");
                        //不存在其他入库单才需要新增
                        if (QTRow.Length == 0)
                        {
                            //记录到日志里面
                            string Insql = string.Format(@"/*dialect*/ INSERT INTO ZWLF_T_OrderLog
                                                ([FID]
                                                ,[FBILLNO]
                                                ,[F_ZWLF_ID]
                                                ,[F_ZWLF_SITE_TRADE_ID]
                                                ,[F_ZWLF_TIME]
                                                ,F_ZWLF_SOURCETYPE)
                                              VALUES
                                               ((select case  when max(Fid) IS NULL then '1' else max(Fid)+1 end from ZWLF_T_OrderLog)
                                               ,(select case  when max(Fid) IS NULL then '1' else max(Fid)+1 end from ZWLF_T_OrderLog)
                                               ,'{0}'
                                               ,'{1}'
                                               ,GETDATE(),'{2}')", id, bil_no, F_ZWLF_SOURCETYPE);
                            DBServiceHelper.Execute(context, Insql);
                            #region 生成其他出库
                            FormMetadata meta = MetaDataServiceHelper.Load(context, "STK_MisDelivery") as FormMetadata;
                            BusinessInfo info = meta.BusinessInfo;
                            IResourceServiceProvider formServiceProvider = meta.BusinessInfo.GetForm().GetFormServiceProvider(true);
                            IBillViewService billViewService = formServiceProvider.GetService(typeof(IDynamicFormView)) as IBillViewService;
                            /******创建单据打开参数*************/
                            Form form = meta.BusinessInfo.GetForm();
                            BillOpenParameter billOpenParameter = new BillOpenParameter(form.Id, meta.GetLayoutInfo().Id);
                            billOpenParameter = new BillOpenParameter(form.Id, string.Empty);
                            billOpenParameter.Context = context;
                            billOpenParameter.ServiceName = form.FormServiceName;
                            billOpenParameter.PageId = Guid.NewGuid().ToString();
                            billOpenParameter.FormMetaData = meta;
                            billOpenParameter.LayoutId = meta.GetLayoutInfo().Id;
                            billOpenParameter.Status = OperationStatus.ADDNEW;
                            billOpenParameter.PkValue = null;
                            billOpenParameter.CreateFrom = CreateFrom.Default;
                            billOpenParameter.ParentId = 0;
                            billOpenParameter.GroupId = "";
                            billOpenParameter.DefaultBillTypeId = null;
                            billOpenParameter.DefaultBusinessFlowId = null;
                            billOpenParameter.SetCustomParameter("ShowConfirmDialogWhenChangeOrg", false);
                            List<AbstractDynamicFormPlugIn> value = form.CreateFormPlugIns();
                            billOpenParameter.SetCustomParameter(FormConst.PlugIns, value);
                            ((IDynamicFormViewService)billViewService).Initialize(billOpenParameter, formServiceProvider);
                            IBillView bill_view = (IBillView)billViewService;
                            bill_view.CreateNewModelData();
                            DynamicFormViewPlugInProxy proxy = bill_view.GetService<DynamicFormViewPlugInProxy>();
                            proxy.FireOnLoad();
                            //表头
                            #region  表头
                            bill_view.Model.SetItemValueByID("FStockOrgId", FUSEORGID, 0);//组织
                            bill_view.Model.SetItemValueByID("FPickOrgId", FUSEORGID, 0);
                            bill_view.Model.SetItemValueByID("FOwnerIdHead", FUSEORGID, 0);
                            //单据类型 
                            string dept_code = array[i]["dept_code"].ToString();
                            if (dept_code.Contains("D03008"))//售后部
                            {
                                bill_view.Model.SetItemValueByNumber("FBillTypeID", "QTCKD06_SYS", 0); //维修原料
                            }
                            else if (dept_code.Contains("D03002")) //品牌部
                            {
                                bill_view.Model.SetItemValueByNumber("FBillTypeID", "QTCKD12_SYS", 0);//品牌出库
                            }
                            else
                            {
                                dept_code = "D1004";
                                bill_view.Model.SetItemValueByNumber("FBillTypeID", "QTCKD01_SYS", 0);
                            }
                            //单据业务日期
                            bill_view.Model.SetValue("FDate", chk_date);
                            //领料部门
                            bill_view.Model.SetItemValueByNumber("FDeptId", dept_code, 0);
                            bill_view.InvokeFieldUpdateService("FDeptId", 0);
                            //表头备注
                            bill_view.Model.SetValue("FNote", "胜途其他出库单生成金蝶其他出库单" + bil_no, 0);
                            //备注
                            bill_view.Model.SetValue("F_ZWLF_Note", "胜途其他出库单生成金蝶其他出库单", 0);
                            //胜途订单唯一标识 
                            bill_view.Model.SetValue("F_ZWLF_ID", id, 0);
                            //其他出库单号
                            bill_view.Model.SetValue("F_ZWLF_OUTNO", bil_no, 0);
                            //标记推送状态
                            bill_view.Model.SetValue("F_ZWLF_PushState", 1, 0);
                            #endregion
                            #region  表体
                            //表体（一个订单可能又多个物料）
                            JArray details = array[i]["details"] as JArray;
                            for (int j = 0; j < details.Count; j++)
                            {
                                string WLFNUMBER = details[j]["prdt_code"].ToString();
                                //新旧编码
                                DataRow[] rowWL = Wldt.Select("FNUMBER='" + details[j]["prdt_code"].ToString() + "'");
                                if (rowWL.Length == 0)
                                {
                                    //新旧编码
                                    DataRow[] rowWL2 = Wldt.Select("FOLDNUMBER='" + details[j]["prdt_code"].ToString() + "'");
                                    if (rowWL2.Length > 0)
                                    {
                                        foreach (DataRow dr in rowWL2)
                                        {
                                            WLFNUMBER = dr["FNUMBER"].ToString();
                                        }
                                    }
                                }
                                //查询是否启用库存
                                string FISINVENTORY = "0";
                                string FISBATCHMANAGE = "1";
                                DataRow[] row_Wl = Wldt.Select("FNUMBER='" + WLFNUMBER + "' and FUSEORGID='" + FUSEORGID + "'");
                                if (row_Wl.Length > 0)
                                {
                                    foreach (DataRow drN in row_Wl)
                                    {
                                        FISINVENTORY = drN["FISINVENTORY"].ToString();
                                        FISBATCHMANAGE = drN["FISBATCHMANAGE"].ToString();
                                    }
                                }
                                if (FISINVENTORY == "1")
                                {
                                    //获取库存仓位
                                    string csql = string.Format(@"select top 1  FBASEQTY,FSTOCKLOCID,c.FNUMBER,isnull(d.FNUMBER,0) as PLFnumber from T_STK_INVENTORY  a  
                                                                      inner  join  T_BD_stock b on a.FSTOCKID=b.FSTOCKID 
                                                                      inner  join   T_BD_MATERIAL  w on w.FMATERIALID=a.FMATERIALID
                                                                      left  join T_BAS_FlexValuesDetail loc on a.FSTOCKLOCID = loc.FId
                                                                      left join t_BAS_FlexValuesEntry c on c.FENTRYID=loc.FF100004
                                                                      left join T_BD_LOTMASTER d on d.FLOTID=a.FLOT
                                                                      where w.FNUMBER='{0}' and b.FNUMBER='{1}' and FBASEQTY<>0 and FSTOCKORGID='{2}' order by FBASEQTY desc ",
                                                                         WLFNUMBER, CKFnumber, FUSEORGID);
                                    DataSet ds_loc = DBServiceHelper.ExecuteDataSet(context, csql);
                                    DataTable dt_loc = ds_loc.Tables[0];
                                    if (dt_loc.Rows.Count > 0)
                                    {
                                        for (int b = 0; b < dt_loc.Rows.Count; b++)
                                        {
                                            //当前库存数
                                            double FBASEQTY = Convert.ToDouble(dt_loc.Rows[b]["FBASEQTY"]);
                                            //仓位
                                            long FSTOCKLOCID = Convert.ToInt64(dt_loc.Rows[b]["FSTOCKLOCID"]);
                                            //仓位编码
                                            string FLOCFnumber = dt_loc.Rows[b]["FNUMBER"].ToString();
                                            //批号
                                            string PLFnumber = dt_loc.Rows[b]["PLFnumber"].ToString();
                                            bill_view.Model.CreateNewEntryRow("FEntity");
                                            bill_view.Model.SetItemValueByNumber("FMaterialId", WLFNUMBER, j);
                                            bill_view.InvokeFieldUpdateService("FMaterialId", j);
                                            bill_view.InvokeFieldUpdateService("FUnitID", j);
                                            bill_view.InvokeFieldUpdateService("FBaseUnitId", j);
                                            //实发数量
                                            bill_view.Model.SetValue("FQty", details[j]["qty"].ToString(), j);
                                            bill_view.InvokeFieldUpdateService("FQty", j);
                                            bill_view.InvokeFieldUpdateService("FBaseQty", j);
                                            //源单编号
                                            bill_view.Model.SetValue("FSrcBillNo", bil_no, j);
                                            //仓库111252
                                            bill_view.Model.SetItemValueByNumber("FStockId", CKFnumber, j);
                                            bill_view.InvokeFieldUpdateService("FStockId", j);
                                            //仓位
                                            bill_view.Model.SetItemValueByID("FStockLocId", FSTOCKLOCID, j);
                                            bill_view.Model.SetItemValueByNumber("$$FStockLocId__FF100001", FLOCFnumber, j);
                                            //批号
                                            if (PLFnumber != "0")
                                            {
                                                bill_view.Model.SetItemValueByNumber("FLot", PLFnumber, j);
                                                bill_view.InvokeFieldUpdateService("FLot", j);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        //不启用库存
                                        bill_view.Model.CreateNewEntryRow("FEntity");
                                        bill_view.Model.SetItemValueByNumber("FMaterialId", WLFNUMBER, j);
                                        bill_view.InvokeFieldUpdateService("FMaterialId", j);
                                        bill_view.InvokeFieldUpdateService("FUnitID", j);
                                        bill_view.InvokeFieldUpdateService("FBaseUnitId", j);
                                        //实发数量
                                        bill_view.Model.SetValue("FQty", details[j]["qty"].ToString(), j);
                                        bill_view.InvokeFieldUpdateService("FQty", j);
                                        bill_view.InvokeFieldUpdateService("FBaseQty", j);
                                        //源单编号
                                        bill_view.Model.SetValue("FSrcBillNo", bil_no, j);
                                        //仓库111252
                                        bill_view.Model.SetItemValueByNumber("FStockId", CKFnumber, j);
                                        bill_view.InvokeFieldUpdateService("FStockId", j);
                                        if (FISBATCHMANAGE == "1")
                                        {
                                            bill_view.Model.SetItemValueByNumber("FLot", "000000", j);
                                            bill_view.InvokeFieldUpdateService("FLot", j);
                                        }
                                    }
                                }
                            }
                            #endregion
                            try
                            {
                                //保存
                                IOperationResult save_result = bill_view.Model.Save();
                                if (save_result.IsSuccess)
                                {
                                    string mssg = "";
                                    string fid = string.Empty;
                                    string F_ZWLF_Fbillno = string.Empty;
                                    OperateResultCollection Collection = save_result.OperateResult;
                                    foreach (var item in Collection)
                                    {
                                        fid = item.PKValue.ToString();
                                        F_ZWLF_Fbillno = item.Number.ToString();
                                    }
                                    //记录生成订单成功
                                    sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_STKState='1',F_ZWLF_QTFbillno='{0}' 
                                                            where F_ZWLF_ID='{1}' and F_ZWLF_site_trade_id='{2}';",
                                                            F_ZWLF_Fbillno, array[i]["id"].ToString(), bil_no);
                                    DBServiceHelper.Execute(context, sql);

                                    //提交审核订单
                                    IOperationResult result = Operation(fid, meta, context);
                                    if (!result.IsSuccess)
                                    {
                                        foreach (var item in result.ValidationErrors)
                                        {
                                            mssg = mssg + item.Message;
                                        }
                                        if (!result.InteractionContext.IsNullOrEmpty())
                                        {
                                            mssg = mssg + result.InteractionContext.SimpleMessage;
                                        }
                                        msg.result = mssg;
                                        sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_STKState='0',F_ZWLF_NOTE=F_ZWLF_NOTE+'{0}' 
                                                                  where F_ZWLF_ID='{1}' and F_ZWLF_site_trade_id='{2}' ;",
                                                              mssg, array[i]["id"].ToString(), bil_no);
                                    }
                                    else
                                    {
                                        mssg = "生成其他出库单成功";
                                        msg.result = mssg;
                                        sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_STKState='1',F_ZWLF_NOTE=F_ZWLF_NOTE+'{0}' 
                                                                where F_ZWLF_ID='{1}'  and F_ZWLF_site_trade_id='{2}';",
                                                            mssg, array[i]["id"].ToString(), bil_no);
                                    }
                                    DBServiceHelper.Execute(context, sql);


                                }
                                else
                                {
                                    string mssg = "";
                                    foreach (var item in save_result.ValidationErrors)
                                    {
                                        mssg = mssg + item.Message;
                                    }
                                    if (!save_result.InteractionContext.IsNullOrEmpty())
                                    {
                                        mssg = mssg + save_result.InteractionContext.SimpleMessage;
                                    }
                                    msg.result = mssg;
                                    sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_STKState='0',F_ZWLF_NOTE=F_ZWLF_NOTE+'{0}'
                                                        where F_ZWLF_ID='{1}'  and F_ZWLF_site_trade_id='{2}';",
                                                            mssg, array[i]["id"].ToString(), bil_no);
                                    DBServiceHelper.Execute(context, sql);
                                }
                            }
                            catch (KDException ex)
                            {
                                msg.result = ex.ToString().Substring(0, 500);
                                sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_STKState='0',F_ZWLF_NOTE=F_ZWLF_NOTE+'{0}' 
                                                                          where F_ZWLF_ID='{1}' and F_ZWLF_site_trade_id='{2}';",
                                                                  msg.result, array[i]["id"].ToString(), bil_no);
                                DBServiceHelper.Execute(context, sql);
                            }
                            #endregion
                        }
                    }
                }
                else
                {
                    //记录到日志里面
                    string Insql = string.Format(@"/*dialect*/ INSERT INTO ZWLF_T_OrderLog
                                                ([FID]
                                                ,[FBILLNO]
                                                ,[F_ZWLF_ID]
                                                ,[F_ZWLF_SITE_TRADE_ID]
                                                ,[F_ZWLF_TIME]
                                                ,F_ZWLF_NOTE
                                                ,F_ZWLF_SOURCETYPE)
                                              VALUES
                                               ((select case  when max(Fid) IS NULL then '1' else max(Fid)+1 end from ZWLF_T_OrderLog)
                                               ,(select case  when max(Fid) IS NULL then '1' else max(Fid)+1 end from ZWLF_T_OrderLog)
                                               ,'{0}'
                                               ,'{1}'
                                               ,GETDATE(),'{2}','{3}')", id, bil_no, checkMsg, F_ZWLF_SOURCETYPE);
                    DBServiceHelper.Execute(context, Insql);
                }
            }
            msg.status = true;
            msg.sum = array.Count;
            return msg;
        }


        /// <summary>
        /// 提交审核
        /// </summary>
        /// <param name="FID"></param>
        /// <param name="materialmeta"></param>
        /// <returns></returns>
        private IOperationResult Operation(string FID, FormMetadata materialmeta, Context context)
        {
            OperateOption option = OperateOption.Create();
            option.SetIgnoreWarning(true);
            option.SetVariableValue("ignoreTransaction", true);
            IOperationResult result = null;
            //提交
            object[] items = { FID };
            ISubmitService submitService = Kingdee.BOS.App.ServiceHelper.GetService<ISubmitService>();
            result = submitService.Submit(context, materialmeta.BusinessInfo, items, "Submit", option);
            //审核
            IAuditService auditService = Kingdee.BOS.App.ServiceHelper.GetService<IAuditService>();
            result = auditService.Audit(context, materialmeta.BusinessInfo, items, option);
            return result;
        }

    }
}
