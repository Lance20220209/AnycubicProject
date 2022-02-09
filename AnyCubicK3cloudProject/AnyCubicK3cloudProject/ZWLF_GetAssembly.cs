using Kingdee.BOS;
using Kingdee.BOS.Contracts;
using Kingdee.BOS.Core;
using Kingdee.BOS.Core.Bill;
using Kingdee.BOS.Core.DynamicForm;
using Kingdee.BOS.Core.DynamicForm.PlugIn;
using Kingdee.BOS.Core.Metadata;
using Kingdee.BOS.Core.Metadata.FormElement;
using Kingdee.BOS.Orm;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.ServiceHelper;
using Kingdee.BOS.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnyCubicK3cloudProject
{
    public class ZWLF_GetAssembly
    {
        /// <summary>
        /// 调用胜途组织拆卸单
        /// </summary>
        /// <param name="param"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public Msg GetAssembly(Parameters param, Context context)
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
                @params["bil_status"] = "2";
                //@params["wh_code"] = param.wh_code;// 仓库编码
                Httpclient httpclient = new Httpclient();
                string json = httpclient.ZWLFRequstPost(@params, param.url, param.AppSecret);
                JObject OrderJson = JsonConvert.DeserializeObject<JObject>(json);
                string code = OrderJson["code"].ToString();
                if (code == "200")
                {
                    int total_count = Convert.ToInt32(OrderJson["data"]["total_count"].ToString()); //本次返回的订单数
                    if (total_count > 0)
                    {
                        //组装单
                        string sql = string.Format(@"/*dialect*/ select distinct FID,F_ZWLF_ASSEMBLYID,F_ZWLF_ASSEMBLYNO from T_STK_ASSEMBLY  
                                             where FCANCELSTATUS='A' and  F_ZWLF_ASSEMBLYID!='' and F_ZWLF_ASSEMBLYNO!='' and FSTOCKORGID=165223;");
                        //仓库映射
                        sql += string.Format(@"/*dialect*/ select FUSEORGID, FNUMBER,F_ZWLF_WAREHOUSECODE  from  ZWLF_t_Cust_StockEntry a 
                                           inner join t_BD_Stock b on a.FStockId=b.FSTOCKID where F_ZWLF_DISABLE=0  ;");
                        //物料
                        sql += string.Format(@"/*dialect*/ select FISINVENTORY,a.FUSEORGID ,a.FNUMBER ,FOLDNUMBER , FISBATCHMANAGE,u.FNUMBER as DWFnumber from T_BD_MATERIAL a
                                                          inner join t_BD_MaterialStock c on a.FMATERIALID=c.FMATERIALID
                                                         inner join t_BD_MaterialBase b  on a.FMATERIALID=b.FMATERIALID
														 left  join T_BD_UNIT u on u.FUNITID=b.FBASEUNITID
                                                         where a.FUSEORGID='165223'and a.FDOCUMENTSTATUS='C' and  a.FFORBIDSTATUS='A'");
                        DataSet ds = DBServiceHelper.ExecuteDataSet(context, sql);
                        //生成其他出入库单
                        msg = GenerateOhter(OrderJson, context, ds);
                    }
                }
                else
                {
                    msg.status = false;
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
        /// <summary>
        /// 生成其他出入库单
        /// </summary>
        /// <param name="json"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public Msg GenerateOhter(JObject json, Context context, DataSet ds)
        {
            Msg msg = new Msg();
            DataTable dt_zz = ds.Tables[0];
            //仓库
            DataTable dt_ck = ds.Tables[1];
            //物料编码
            DataTable WLdt = ds.Tables[2];
            string FstockNumber = "";
            string FStockOrgId = "";//库存组织
            string sql = "";
            bool checkstatus = true;
            string checkMsg = "";
            int total_count = Convert.ToInt32(json["data"]["total_count"].ToString());
            JArray array = json["data"]["data"] as JArray;
            for (int i = 0; i < array.Count; i++)
            {
                //组装单id
                string id = array[i]["id"].ToString();
                //组装单单号
                string bil_no = array[i]["bil_no"].ToString();
                //审核时间
                string chk_date = array[i]["chk_date"].ToString();
                //部门编码
                string dept_code = array[i]["dept_code"].ToString();
                if (string.IsNullOrEmpty(dept_code))
                {
                    dept_code = "D1004";
                }
                //数量
                decimal qty = Convert.ToDecimal(array[i]["qty"].ToString());
                //仓库编码
                string wh_code = array[i]["wh_code"].ToString();
                //母件编码
                string prdt_code = array[i]["prdt_code"].ToString();
                //获取仓库
                DataRow[] rowCK = dt_ck.Select("F_ZWLF_WAREHOUSECODE='" + wh_code + "'");
                if (rowCK.Length > 0)
                {
                    foreach (DataRow dr in rowCK)
                    {
                        //获取仓库
                        FstockNumber = dr["FNUMBER"].ToString();
                        //库存组织
                        FStockOrgId = dr["FUSEORGID"].ToString();
                    }
                }
                else
                {
                    checkMsg += "找不到对应仓库信息";
                    checkstatus = false;
                }

                #region 生成金蝶组装单
                //判断是否已经存在 F_ZWLF_ASSEMBLYID,F_ZWLF_ASSEMBLYNO
                DataRow[] rowin = dt_zz.Select("F_ZWLF_ASSEMBLYID='" + id + "' and F_ZWLF_ASSEMBLYNO='" + bil_no + "' ");
                if (rowin.Length == 0)
                {
                    //把前订单记录到日志里面
                    sql = string.Format(@"/*dialect*/ INSERT INTO ZWLF_T_OrderLog
                                                  ([FID]
                                                  ,[FBILLNO]
                                                  ,[F_ZWLF_ID]
                                                  ,[F_ZWLF_SITE_TRADE_ID]
                                                  ,[F_ZWLF_TIME]
                                                  ,F_ZWLF_NOTE,F_ZWLF_SOURCETYPE)
                                                VALUES
                                                 ((select case  when max(Fid) IS NULL then '1' else max(Fid)+1 end from ZWLF_T_OrderLog)
                                                 ,(select case  when max(Fid) IS NULL then '1' else max(Fid)+1 end from ZWLF_T_OrderLog)
                                                 ,'{0}'
                                                 ,'{1}'
                                                 ,GETDATE(),'{2}','10')", id, bil_no, "胜途组装单生成金蝶的组装单");
                    DBServiceHelper.Execute(context, sql);
                    FormMetadata meta = MetaDataServiceHelper.Load(context, "STK_AssembledApp") as FormMetadata;
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

                    #region  表头
                    //库存组织
                    bill_view.Model.SetItemValueByID("FStockOrgId", FStockOrgId, 0);
                    bill_view.InvokeFieldUpdateService("FStockOrgId", 0);
                    // 单据类型 
                    bill_view.Model.SetItemValueByNumber("FBillTypeID", "ZZCX01_SYS", 0);
                    //单据业务日期//发货日期
                    bill_view.Model.SetValue("FDate", chk_date);
                    //部门
                    bill_view.Model.SetItemValueByNumber("FDeptID", dept_code, 0);
                    bill_view.InvokeFieldUpdateService("FDeptID", 0);
                    //组织单id
                    bill_view.Model.SetValue("F_ZWLF_AssemblyId", id, 0);
                    //组装单编号
                    bill_view.Model.SetValue("F_ZWLF_AssemblyNo", bil_no, 0);
                    //设置成功
                    bill_view.Model.SetValue("F_ZWLF_PushState", "1", 0);
                    #endregion
                    #region  成品表
                    //新旧编码
                    DataRow[] rowWL = WLdt.Select("FNUMBER='" + prdt_code + "'");
                    string DWFnumber = "";
                    if (rowWL.Length == 0)
                    {
                        //新旧编码
                        DataRow[] rowWL2 = WLdt.Select("FOLDNUMBER='" + prdt_code + "'");
                        if (rowWL2.Length > 0)
                        {
                            foreach (DataRow dr in rowWL2)
                            {
                                prdt_code = dr["FNUMBER"].ToString();
                                DWFnumber = dr["DWFnumber"].ToString();
                            }
                        }
                    }
                    else
                    {
                        foreach (DataRow dr in rowWL)
                        {
                            DWFnumber = dr["DWFnumber"].ToString();
                        }
                    }
                    //成品
                    // bill_view.Model.CreateNewEntryRow("FEntity");
                    bill_view.Model.SetItemValueByNumber("FMaterialID", prdt_code, 0);
                    bill_view.InvokeFieldUpdateService("FMaterialID", 0);
                    //基本单位
                    bill_view.InvokeFieldUpdateService("FBaseUnitID", 0);
                    bill_view.Model.SetItemValueByNumber("FUnitID", DWFnumber, 0);
                    bill_view.InvokeFieldUpdateService("FUnitID", 0);
                    // 销售数量
                    bill_view.Model.SetValue("FQty", qty, 0);
                    bill_view.InvokeFieldUpdateService("FQty", 0);
                    //
                    bill_view.InvokeFieldUpdateService("FBaseQty", 0);
                    //仓库
                    bill_view.Model.SetItemValueByNumber("FStockID", FstockNumber, 0);
                    bill_view.InvokeFieldUpdateService("FStockID", 0);
                    //批号
                    bill_view.Model.SetItemValueByNumber("FLOT", "000000", 0);
                    bill_view.InvokeFieldUpdateService("FLOT", 0);
                    #endregion
                    #region 循环表体
                    JArray details = array[i]["details"] as JArray;
                    for (int j = 0; j < details.Count; j++)
                    {
                        //子项商品编码
                        string zWLFNUMBER = details[j]["prdt_code"].ToString();
                        //子项商品数量
                        decimal Zqty = Convert.ToDecimal(details[j]["qty"].ToString());
                        //子项仓库
                        string Zwh_code = details[j]["wh_code"].ToString();
                        string zFstockNumber = "";
                        string zFStockOrgId = "";
                        string zDWFnumber = "";
                        //获取仓库
                        DataRow[] ZrowCK = dt_ck.Select("F_ZWLF_WAREHOUSECODE='" + Zwh_code + "'");
                        if (ZrowCK.Length > 0)
                        {
                            foreach (DataRow dr in rowCK)
                            {
                                //获取仓库
                                zFstockNumber = dr["FNUMBER"].ToString();
                                //库存组织
                                zFStockOrgId = dr["FUSEORGID"].ToString();
                            }
                        }
                        else
                        {
                            checkMsg += "找不到对应仓库信息";
                            checkstatus = false;
                        }
                        //新旧编码
                        DataRow[] zrowWL = WLdt.Select("FNUMBER='" + zWLFNUMBER + "'");
                        if (zrowWL.Length == 0)
                        {
                            //新旧编码
                            DataRow[] zrowWL2 = WLdt.Select("FOLDNUMBER='" + zWLFNUMBER + "'");
                            if (zrowWL2.Length > 0)
                            {
                                foreach (DataRow dr in zrowWL2)
                                {
                                    zWLFNUMBER = dr["FNUMBER"].ToString();
                                    zDWFnumber = dr["DWFnumber"].ToString();
                                }
                            }
                            else //物料不存在
                            {
                                checkMsg += "物料编码：" + zWLFNUMBER + "不存在或者未审核！";
                                checkstatus = false;
                                break;
                            }
                        }
                        else
                        {
                            foreach (DataRow dr in zrowWL)
                            {
                                zDWFnumber = dr["DWFnumber"].ToString();
                            }
                        }
                        //子件表
                        bill_view.Model.CreateNewEntryRow("FSubEntity");
                        bill_view.Model.SetItemValueByNumber("FMaterialIDSETY", zWLFNUMBER, j);
                        bill_view.InvokeFieldUpdateService("FMaterialIDSETY", j);
                        bill_view.InvokeFieldUpdateService("FBaseUnitIDSETY", j);
                        //单位
                        bill_view.Model.SetItemValueByNumber("FUnitIDSETY", zDWFnumber, j);
                        bill_view.InvokeFieldUpdateService("FUnitIDSETY", j);
                        //数量
                        bill_view.Model.SetValue("FQtySETY", Zqty, j);
                        bill_view.InvokeFieldUpdateService("FQtySETY", j);
                        bill_view.InvokeFieldUpdateService("FBaseQtySETY", j);
                        //仓库
                        bill_view.Model.SetItemValueByNumber("FStockIDSETY", zFstockNumber, j);
                        bill_view.InvokeFieldUpdateService("FStockIDSETY", j);
                        //库存状态
                        bill_view.Model.SetItemValueByNumber("FStockStatusIDSETY", "KCZT01_SYS", j);
                        bill_view.InvokeFieldUpdateService("FStockStatusIDSETY", j);
                        bill_view.Model.SetItemValueByNumber("FLOTSETY", "000000", j);
                        bill_view.InvokeFieldUpdateService("FLOTSETY", j);


                    }
                    #endregion
                    if (checkstatus)
                    {
                        //删除空白行
                        DynamicObjectCollection ProductEntitycollection = bill_view.Model.DataObject["ProductEntity"] as DynamicObjectCollection;
                        foreach (var dynamic in ProductEntitycollection)
                        {
                            //子件
                            DynamicObjectCollection STK_ASSEMBLYSUBITEMCollection = dynamic["STK_ASSEMBLYSUBITEM"] as DynamicObjectCollection;
                            for (int a = STK_ASSEMBLYSUBITEMCollection.Count; a > 0; a--)
                            {
                                string MaterialIDSETY = STK_ASSEMBLYSUBITEMCollection[a - 1]["MaterialIDSETY_Id"].ToString();
                                if (MaterialIDSETY == "0")
                                {
                                    STK_ASSEMBLYSUBITEMCollection.RemoveAt(a - 1);
                                }
                            }
                        }
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
                            sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_AssemblyState='1',F_ZWLF_AssemblyNo='{0}' 
                                                                   where F_ZWLF_ID='{1}' and F_ZWLF_site_trade_id='{2}';",
                                                               F_ZWLF_Fbillno, id, bil_no);
                            DBServiceHelper.Execute(context, sql);
                            try
                            {
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
                                    if (mssg.Contains("更新库存时出现可以忽略的异常数据，是否继续"))
                                    {
                                        mssg = "组装单生成其他入库成功：";
                                        sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_AssemblyState='1',F_ZWLF_NOTE='{0}' 
                                                                  where F_ZWLF_ID='{1}' and F_ZWLF_site_trade_id='{2}';",
                                                                  mssg, id, bil_no);
                                        DBServiceHelper.Execute(context, sql);
                                    }
                                    else
                                    {
                                        mssg = "组装单生成其他入库失败：";
                                        sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_AssemblyState='0',F_ZWLF_NOTE='{0}' 
                                                                where F_ZWLF_ID='{1}'  and F_ZWLF_site_trade_id='{2}';",
                                                                mssg, id, bil_no);
                                        DBServiceHelper.Execute(context, sql);
                                    }
                                }
                                else
                                {
                                    mssg = "组装单生成其他入库成功：";
                                    sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_AssemblyState='1',F_ZWLF_NOTE='{0}' 
                                                                  where F_ZWLF_ID='{1}' and F_ZWLF_site_trade_id='{2}';",
                                                              mssg, id, bil_no);
                                    DBServiceHelper.Execute(context, sql);
                                }
                            }
                            catch (KDException ex)
                            {
                                msg.result = ex.ToString().Substring(0, 500);
                                sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_AssemblyState='0',F_ZWLF_NOTE='{0}'
                                                            where F_ZWLF_ID='{1}' and F_ZWLF_site_trade_id='{2}';",
                                                        msg.result, id, bil_no);
                                DBServiceHelper.Execute(context, sql);
                            }
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
                            msg.result = "胜途组装单生成金蝶其他入库单失败：" + mssg;
                            sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_AssemblyState='0',F_ZWLF_NOTE='{0}' 
                                                                where F_ZWLF_ID='{1}'  and F_ZWLF_site_trade_id='{2}';",
                                                                mssg, id, bil_no);
                            DBServiceHelper.Execute(context, sql);
                        }

                    }
                    else
                    {
                        msg.result = "胜途组装单生成金蝶其他入库单失败：" + checkMsg;
                        sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_AssemblyState='0',F_ZWLF_NOTE='{0}' 
                                                                where F_ZWLF_ID='{1}'  and F_ZWLF_site_trade_id='{2}';",
                                                            checkMsg, id, bil_no);
                        DBServiceHelper.Execute(context, sql);
                    }


                }
                #endregion
            }
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
