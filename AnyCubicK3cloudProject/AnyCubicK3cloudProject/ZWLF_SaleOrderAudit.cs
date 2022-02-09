using Kingdee.BOS;
using Kingdee.BOS.Contracts;
using Kingdee.BOS.Core.DynamicForm;
using Kingdee.BOS.Core.DynamicForm.Operation;
using Kingdee.BOS.Core.DynamicForm.PlugIn;
using Kingdee.BOS.Core.DynamicForm.PlugIn.Args;
using Kingdee.BOS.Core.Interaction;
using Kingdee.BOS.Core.List;
using Kingdee.BOS.Core.Metadata;
using Kingdee.BOS.Core.Metadata.ConvertElement.ServiceArgs;
using Kingdee.BOS.Orm;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.ServiceHelper;
using Kingdee.BOS.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnyCubicK3cloudProject
{
    [Description("销售订单审核自动下推生成销售出库单")]
    [Kingdee.BOS.Util.HotUpdate]
    public class ZWLF_SaleOrderAudit : AbstractOperationServicePlugIn
    {
        /// <summary>
        /// 定制加载指定字段到实体里<p>
        /// </summary>
        /// <param name="e">事件对象</param>
        public override void OnPreparePropertys(Kingdee.BOS.Core.DynamicForm.PlugIn.Args.PreparePropertysEventArgs e)
        {
            base.OnPreparePropertys(e);
            e.FieldKeys.Add("F_ZWLF_SourceType");//源单类型
            e.FieldKeys.Add("FBillTypeID");//单据类型 
            e.FieldKeys.Add("F_ZWLF_sal_bilno");
            e.FieldKeys.Add("F_ZWLF_site_trade_id");//单据类型
            e.FieldKeys.Add("F_ZWLF_id");
        }
        public override void EndOperationTransaction(EndOperationTransactionArgs e)
        {
            string msgg = "";
            try
            {
                string sql = string.Empty;
                string upsql = string.Empty;
                if (e.DataEntitys != null && e.DataEntitys.Count<DynamicObject>() > 0)
                {
                    foreach (DynamicObject item in e.DataEntitys)
                    {
                        //源单类型
                        string F_ZWLF_SourceType = item["F_ZWLF_SourceType"].ToString();
                        //单据类型
                        string FBillTypeID = item["BillTypeId_Id"].ToString();
                        string F_ZWLF_id= item["F_ZWLF_id"].ToString();
                        string F_ZWLF_sal_bilno = item["F_ZWLF_sal_bilno"].ToString();
                        string F_ZWLF_site_trade_id = item["F_ZWLF_site_trade_id"].ToString();
                        string Id = item["Id"].ToString();
                        if (F_ZWLF_SourceType=="02"|| F_ZWLF_SourceType == "01")
                        {
                            Msg msg = SaleOrderOuStockPush(Id);
                        }
                        if(F_ZWLF_SourceType=="04"&& FBillTypeID== "a300e2620037435492aed9842875b451")
                        {
                            Msg msg = SaleOrderPushReturn(Id);
                        }
                    }
                }
            }
            catch (KDException ex)
            {
                throw new KDException("hls", msgg + ex.ToString());
            }
        }
      
        /// <summary>
        /// 销售订单下推销售出库单
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        public Msg SaleOrderOuStockPush(string Id)
        {
            Msg msg = new Msg();
            try
            {
                //单据转换
                string srcFormId = "SAL_SaleOrder"; //销售订单
                string destFormId = "SAL_OUTSTOCK"; //销售出库单
                IMetaDataService mService = Kingdee.BOS.App.ServiceHelper.GetService<IMetaDataService>();
                IViewService vService = Kingdee.BOS.App.ServiceHelper.GetService<IViewService>();
                FormMetadata destmeta = mService.Load(Context, destFormId) as FormMetadata;
                //转换规则的唯一标识
                //string ruleKey = "OutStock_InStock";
                var rules = ConvertServiceHelper.GetConvertRules(Context, srcFormId, destFormId);
                var rule = rules.FirstOrDefault(t => t.IsDefault);
                // ConvertRuleElement rule = GetDefaultConvertRule(ctx, srcFormId, destFormId, ruleKey);
                List<ListSelectedRow> lstRows = new List<ListSelectedRow>();
                string strsql = string.Format(@"select  FENTRYID from T_SAL_ORDERENTRY where FID='{0}'", Id);
                DataSet ds = DBServiceHelper.ExecuteDataSet(Context, strsql);
                for (int j = 0; j < ds.Tables[0].Rows.Count; j++)
                {
                    long entryId = Convert.ToInt64(ds.Tables[0].Rows[j]["FENTRYID"]);
                    //单据标识
                    ListSelectedRow row = new ListSelectedRow(Id, entryId.ToString(), 0, "SAL_SaleOrder");
                    //源单单据体标识
                    row.EntryEntityKey = "FSaleOrderEntry";
                    lstRows.Add(row);
                }
                PushArgs pargs = new PushArgs(rule, lstRows.ToArray());
                IConvertService cvtService = Kingdee.BOS.App.ServiceHelper.GetService<IConvertService>();
                OperateOption option = OperateOption.Create();
                option.SetIgnoreWarning(true);
                option.SetVariableValue("ignoreTransaction", false);
                option.SetIgnoreInteractionFlag(true);
                ConvertOperationResult cvtResult = cvtService.Push(Context, pargs, option, false);
                if (cvtResult.IsSuccess)
                {
                    string StockOrgId_Id = ""; 
                    DynamicObject[] dylist = (from p in cvtResult.TargetDataEntities select p.DataEntity).ToArray();
                    for (int K = 0; K < dylist.Length; K++)
                    {
                        StockOrgId_Id = dylist[K]["StockOrgId_Id"].ToString();
                    }
                    IOperationResult result =null ;
                    if (StockOrgId_Id == "165223")
                    {
                        result = Operation2(dylist, destmeta);
                    }
                    else
                    {
                       result = Operation(dylist, destmeta);
                    }
                    if (!result.IsSuccess)
                    {
                        string mssg = "";
                        foreach (var item in result.ValidationErrors)
                        {
                            mssg = mssg + item.Message;
                        }
                        if (!result.InteractionContext.IsNullOrEmpty())
                        {
                            mssg = mssg + result.InteractionContext.SimpleMessage;
                        }
                        msg.status = false;
                        msg.result = mssg;
                        return msg;
                    }
                    else
                    {
                        msg.status = true;
                        msg.result = "生成销售出库单成功！";
                        return msg;
                    }

                }
                else
                {
                    msg.status = false;
                    msg.result = "下推生成销售出库单失败!";
                    return msg;
                }
            }
            catch(KDException ex)
            {
                msg.status = false;
                msg.result = ex.ToString().Substring(0,200);
                return msg;

            }
        }

        /// <summary>
        /// 销售订单下推销售退货单
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        public Msg SaleOrderPushReturn(string Id)
        {
            string strsql = "";
            Msg msg = new Msg();
            try
            {
                //单据转换
                string srcFormId = "SAL_SaleOrder"; //销售订单
                string destFormId = "SAL_RETURNSTOCK"; //销售退货单
                IMetaDataService mService = Kingdee.BOS.App.ServiceHelper.GetService<IMetaDataService>();
                IViewService vService = Kingdee.BOS.App.ServiceHelper.GetService<IViewService>();
                FormMetadata destmeta = mService.Load(this.Context, destFormId) as FormMetadata;
                //转换规则的唯一标识
                //string ruleKey = "OutStock_InStock";
                var rules = ConvertServiceHelper.GetConvertRules(Context, srcFormId, destFormId);
                var rule = rules.FirstOrDefault(t => t.IsDefault);
                // ConvertRuleElement rule = GetDefaultConvertRule(ctx, srcFormId, destFormId, ruleKey);
                List<ListSelectedRow> lstRows = new List<ListSelectedRow>();
                strsql = string.Format(@"/*dialect*/ select  soe.FENTRYID,soe.FID from T_SAL_ORDER so 
                                            inner join T_SAL_ORDERENTRY soe on so.FID=soe.FID
                                            where so.FID='{0}'", Id);
                DataSet ds = DBServiceHelper.ExecuteDataSet(Context, strsql);
                for (int j = 0; j < ds.Tables[0].Rows.Count; j++)
                {
                    long entryId = Convert.ToInt64(ds.Tables[0].Rows[j]["FENTRYID"]);
                    string SoFID = ds.Tables[0].Rows[j]["FID"].ToString();
                    //单据标识
                    ListSelectedRow row = new ListSelectedRow(SoFID, entryId.ToString(), 0, "SAL_SaleOrder");
                    //源单单据体标识
                    row.EntryEntityKey = "FSaleOrderEntry";
                    lstRows.Add(row);
                }
                PushArgs pargs = new PushArgs(rule, lstRows.ToArray());
                IConvertService cvtService = Kingdee.BOS.App.ServiceHelper.GetService<IConvertService>();
                OperateOption option = OperateOption.Create();
                option.SetIgnoreWarning(true);
                option.SetVariableValue("ignoreTransaction", false);
                option.SetIgnoreInteractionFlag(true);
                ConvertOperationResult cvtResult = cvtService.Push(Context, pargs, option, false);
                if (cvtResult.IsSuccess)
                {
                    string mssg = "";
                    string StockOrgId_Id = "";
                    DynamicObject[] dylist = (from p in cvtResult.TargetDataEntities select p.DataEntity).ToArray();
                    for (int K = 0; K < dylist.Length; K++)
                    {
                        StockOrgId_Id = dylist[K]["StockOrgId_Id"].ToString();
                    }
                    //生成销售退货单
                    IOperationResult result = null;
                    if (StockOrgId_Id == "165223")
                    {
                        //忽略负库存
                        result = Operation2(dylist, destmeta);
                    }
                    else
                    {
                        //不能能忽略
                        result = Operation(dylist, destmeta);
                    }

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
                        msg.status = false;
                    }
                    else
                    {
                        OperateResultCollection operateResults = result.OperateResult;
                        msg.result = operateResults[0].Number;
                        msg.status = true;
                    }
                }
            }
            catch (KDException ex)
            {
                msg.status = false;
                msg.result = "销售订单生成退货单失败:" + ex.ToString().Substring(0, 300);
            }
            return msg;
        }

        /// <summary>
        ///保存
        /// </summary>
        /// <param name="newdlm"></param>
        /// <param name="materialmeta"></param>
        /// <returns></returns>
        private IOperationResult Operation(DynamicObject[] dylist, FormMetadata materialmeta)
        {
            OperateOption option = OperateOption.Create();
            option.SetIgnoreWarning(false);
            option.SetVariableValue("ignoreTransaction", false);
            IOperationResult result = null;
            string mssg = "";
            //保存
            ISaveService saveService = Kingdee.BOS.App.ServiceHelper.GetService<ISaveService>();
            result = saveService.Save(Context, materialmeta.BusinessInfo, dylist, option);
            if (!result.IsSuccess)
            {
                return result;
            }
             //提交
             object[] items = dylist.Select(p => p["Id"]).ToArray();
            ISubmitService submitService = Kingdee.BOS.App.ServiceHelper.GetService<ISubmitService>();
            result = submitService.Submit(Context, materialmeta.BusinessInfo, items, "Submit", option);
            if (!result.IsSuccess)
            {
                return result;
            }
            //审核
            IAuditService auditService = Kingdee.BOS.App.ServiceHelper.GetService<IAuditService>();
            result = auditService.Audit(Context, materialmeta.BusinessInfo, items, option);
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
            }
            return result;
        }
        /// <summary>
        ///保存忽略负库存
        /// </summary>
        /// <param name="newdlm"></param>
        /// <param name="materialmeta"></param>
        /// <returns></returns>
        private IOperationResult Operation2(DynamicObject[] dylist, FormMetadata materialmeta)
        {
            OperateOption option = OperateOption.Create();
            option.SetIgnoreWarning(true);
            option.SetVariableValue("ignoreTransaction", true);
            IOperationResult result = null;
            string mssg = "";
            //保存
            ISaveService saveService = Kingdee.BOS.App.ServiceHelper.GetService<ISaveService>();
            result = saveService.Save(Context, materialmeta.BusinessInfo, dylist, option);
            if (!result.IsSuccess)
            {
                return result;
            }
            //提交
            object[] items = dylist.Select(p => p["Id"]).ToArray();
            ISubmitService submitService = Kingdee.BOS.App.ServiceHelper.GetService<ISubmitService>();
            result = submitService.Submit(Context, materialmeta.BusinessInfo, items, "Submit", option);
            if (!result.IsSuccess)
            {
                return result;
            }
            //审核
            IAuditService auditService = Kingdee.BOS.App.ServiceHelper.GetService<IAuditService>();
            result = auditService.Audit(Context, materialmeta.BusinessInfo, items, option);
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
            }
            return result;
        }
    }
}
