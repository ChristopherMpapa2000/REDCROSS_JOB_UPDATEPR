using Job_UpdatePR.Item;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WolfApprove.API2.Controllers.Utils;

namespace Job_UpdatePR
{
    class Program
    {
        public static void Log(String iText)
        {
            string pathlog = ItemConfig.LogFile;
            String logFolderPath = System.IO.Path.Combine(pathlog, DateTime.Now.ToString("yyyyMMdd"));

            if (!System.IO.Directory.Exists(logFolderPath))
            {
                System.IO.Directory.CreateDirectory(logFolderPath);
            }
            String logFilePath = System.IO.Path.Combine(logFolderPath, DateTime.Now.ToString("yyyyMMdd") + ".txt");

            try
            {
                using (System.IO.StreamWriter outfile = new System.IO.StreamWriter(logFilePath, true))
                {
                    System.Text.StringBuilder sbLog = new System.Text.StringBuilder();

                    String[] listText = iText.Split('|').ToArray();

                    foreach (String s in listText)
                    {
                        sbLog.AppendLine($"[{DateTime.Now:HH:mm:ss}] {s}");
                    }

                    outfile.WriteLine(sbLog.ToString());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing log file: {ex.Message}");
            }
        }
        public static void LogError(String iText)
        {

            string pathlog = ItemConfig.LogFile;
            String logFolderPath = System.IO.Path.Combine(pathlog, DateTime.Now.ToString("yyyyMMdd"));

            if (!System.IO.Directory.Exists(logFolderPath))
            {
                System.IO.Directory.CreateDirectory(logFolderPath);
            }
            String logFilePath = System.IO.Path.Combine(logFolderPath, DateTime.Now.ToString("yyyyMMdd") + "LogError.txt");

            try
            {
                using (System.IO.StreamWriter outfile = new System.IO.StreamWriter(logFilePath, true))
                {
                    System.Text.StringBuilder sbLog = new System.Text.StringBuilder();

                    String[] listText = iText.Split('|').ToArray();

                    foreach (String s in listText)
                    {
                        sbLog.AppendLine($"[{DateTime.Now:HH:mm:ss}] {s}");
                    }

                    outfile.WriteLine(sbLog.ToString());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing log file: {ex.Message}");
            }
        }
        static void Main(string[] args)
        {
            try
            {
                Log("====== Start Process ====== : " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                Log(string.Format("Run batch as :{0}", System.Security.Principal.WindowsIdentity.GetCurrent().Name));
                DataconDataContext db = new DataconDataContext(ItemConfig.dbConnectionString);
                if (db.Connection.State == ConnectionState.Open)
                {
                    db.Connection.Close();
                    db.Connection.Open();
                }
                db.Connection.Open();
                db.CommandTimeout = 0;

                var lstmemo = Getdata_Update(db);
                Log("lstmemo: " + lstmemo.Count());
                Console.WriteLine("lstmemo: " + lstmemo.Count());
                if (lstmemo != null)
                {
                    UpdateData(lstmemo, db);
                }
                Log("Successfully: " + lstmemo.Count());
                Console.WriteLine("Successfully: " + lstmemo.Count());
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR");
                Console.WriteLine("Exit ERROR");
                LogError("ERROR");
                LogError("message: " + ex.Message);
                LogError("Exit ERROR");
            }
            finally
            {
                Log("====== End Process Process ====== : " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
            }
        }
        public static List<TRNMemo> Getdata_Update(DataconDataContext db)
        {
            List<TRNMemo> memos = new List<TRNMemo>();
            if (ItemConfig.Docno.ToLower().Contains("all") || ItemConfig.Docno.ToLower().Contains("feb"))
            {
                memos = db.TRNMemos.Where(x => x.TemplateId == Convert.ToInt32(ItemConfig.TemplateId) && x.ModifiedDate >= DateTime.Now.AddDays(ItemConfig.IntervalTimeDay)).ToList();
            }
            else
            {
                var splitmemo = ItemConfig.Docno.Split('|').ToList();
                foreach (var item in splitmemo)
                {
                    var memo = db.TRNMemos.Where(x => x.DocumentNo == item.Trim()).FirstOrDefault();
                    if (memo != null)
                    {
                        memos.Add(memo);
                    }
                }
            }
            return memos;
        }
        public static void UpdateData(List<TRNMemo> lstmemo, DataconDataContext db)
        {
            foreach (var item in lstmemo)
            {
                try
                {
                    #region Getdata
                    List<object> Ordered_ProductList = new List<object>();
                    JObject jsonAdvanceForm = JsonUtils.createJsonObject(item.MAdvancveForm);
                    JArray itemsArray = (JArray)jsonAdvanceForm["items"];
                    foreach (JObject jItems in itemsArray)
                    {
                        JArray jLayoutArray = (JArray)jItems["layout"];
                        if (jLayoutArray.Count >= 1)
                        {
                            JObject jTemplateL = (JObject)jLayoutArray[0]["template"];
                            JObject jData = (JObject)jLayoutArray[0]["data"];
                            if ((String)jTemplateL["label"] == "รายการสินค้าที่ขอซื้อ")
                            {
                                foreach (JArray row in jData["row"])
                                {
                                    // ตรวจสอบว่าตำแหน่งที่ 0 (index = 0) ของ row มีค่าหรือไม่
                                    if (row.Count > 1 && row[0]?["value"] != null && !string.IsNullOrEmpty(row[0]["value"].ToString()))
                                    {
                                        List<object> rowObject = new List<object>();
                                        foreach (JObject items in row)
                                        {
                                            rowObject.Add(items["value"].ToString());
                                        }
                                        Ordered_ProductList.Add(rowObject);
                                    }
                                }    
                            }
                        }
                    }
                    #endregion
                    #region Updatedata
                    if (Ordered_ProductList.Count > 0)
                    {
                        string Value = string.Empty;
                        decimal SumtotalPrice = 0;
                        decimal SumPriceBeforeTax = 0;
                        for (int i = 0; i < Ordered_ProductList.Count; i++)
                        {
                            dynamic uitem = Ordered_ProductList[i];
                            string aa = uitem[0];
                            string bb = uitem[1];
                            string cc = uitem[2];
                            string dd = uitem[3];
                            string ee = uitem[4];
                            decimal ff = 0;
                            if (ItemConfig.Docno.ToLower().Contains("feb"))
                            {
                                ff = decimal.TryParse(uitem[3]?.ToString(), out decimal result3) ? Math.Round(result3, 2) : 0;
                                ff = GetdataMSTCatagolyItemStock(dd, db, ff, item);
                                if (ff == 0)
                                {
                                    LogError("---- MSTCatagolyItemStock Not have (ราคาสินค้าต่อหน่วย) ----");
                                    LogError("ProductCode : " + dd + "|BeforePrice : " + ff + "|DocumentNo : " + item.DocumentNo + "|Memoid : " + item.MemoId);
                                    continue;
                                }
                            }
                            else
                            {
                                ff = decimal.TryParse(uitem[5]?.ToString(), out decimal result5) ? Math.Round(result5, 2) : 0;
                            }
                            decimal gg = decimal.TryParse(uitem[6]?.ToString(), out decimal result6) ? Math.Round(result6, 2) : 0;
                            decimal hh = decimal.TryParse(uitem[7]?.ToString(), out decimal result7) ? Math.Round(result7, 2) : 0;
                            string ii = uitem[8];
                            string jj = uitem[9];
                            decimal kk = decimal.TryParse(uitem[10]?.ToString(), out decimal result10) ? Math.Round(result10, 2) : 0;

                            //calculator
                            int tpii = int.TryParse(ii, out int aaa) ? aaa : 0;

                            decimal xhh = Math.Round(ff / 1.07m, 2, MidpointRounding.AwayFromZero);
                            decimal xgg = Math.Round(ff - xhh, 2, MidpointRounding.AwayFromZero);
                            decimal xkk = Math.Round((xhh * tpii) * 1.07m, 2, MidpointRounding.AwayFromZero);

                            //รวมเป็นเงินทั้งสิ้น
                            SumtotalPrice += xkk;

                            //ราคาสินค้า (ก่อนภาษี)
                            decimal xPriceBeforeTax = Math.Round(xhh * tpii, 2, MidpointRounding.AwayFromZero);
                            SumPriceBeforeTax += xPriceBeforeTax;

                            if (i > 0) { Value += ","; }
                            Value += $"[{{\"value\": \"{aa.Replace("\"", "\\\"")}\"}},{{\"value\": \"{bb.Replace("\"", "\\\"")}\"}},{{\"value\": \"{cc.Replace("\"", "\\\"")}\"}},{{\"value\": \"{dd.Replace("\"", "\\\"")}\"}}" +
                                $",{{\"value\": \"{ee.Replace("\"", "\\\"")}\"}},{{\"value\": \"{ff.ToString("0.00", CultureInfo.InvariantCulture)}\"}},{{\"value\": \"{xgg.ToString("0.00", CultureInfo.InvariantCulture)}\"}},{{\"value\": \"{xhh.ToString("0.00", CultureInfo.InvariantCulture)}\"}}" +
                                $",{{\"value\": \"{ii.Replace("\"", "\\\"")}\"}},{{\"value\": \"{jj.Replace("\"", "\\\"")}\"}},{{\"value\": \"{xkk.ToString("0.00", CultureInfo.InvariantCulture)}\"}}]";
                        }
                        //ภาษีมูลค่าเพิ่ม , ภาษีมูลค่าเพิ่ม 7%
                        decimal SumVat = Math.Round(SumtotalPrice - SumPriceBeforeTax, 2, MidpointRounding.AwayFromZero);

                        foreach (JObject jItems in itemsArray)
                        {
                            string loglabel = string.Empty;
                            string logValue = string.Empty;
                            try
                            {
                                JArray jLayoutArray = (JArray)jItems["layout"];
                                if (jLayoutArray.Count >= 1)
                                {
                                    JObject jTemplateL = (JObject)jLayoutArray[0]["template"];
                                    JObject UpdatejDataL = (JObject)jLayoutArray[0]["data"];
                                    if ((String)jTemplateL["label"] == "รายการสินค้าที่ขอซื้อ")
                                    {
                                        loglabel = "รายการสินค้าที่ขอซื้อ";
                                        Value = $"[{Value}]";
                                        logValue = Value;
                                        UpdatejDataL.Remove("row");
                                        UpdatejDataL.Add("row", JArray.Parse(Value));
                                    }
                                    if (jLayoutArray.Count > 1)
                                    {
                                        JObject jTemplateR = (JObject)jLayoutArray[1]["template"];
                                        JObject UpdatejData = (JObject)jLayoutArray[1]["data"];
                                        if ((String)jTemplateR["label"] == "ราคารวมสินค้า")
                                        {
                                            loglabel = "ราคารวมสินค้า";
                                            UpdatejData["value"] = SumtotalPrice.ToString("0.00", CultureInfo.InvariantCulture);
                                        }
                                        if ((String)jTemplateR["label"] == "ภาษีมูลค่าเพิ่ม")
                                        {
                                            loglabel = "ภาษีมูลค่าเพิ่ม";
                                            UpdatejData["value"] = SumVat.ToString("0.00", CultureInfo.InvariantCulture);

                                        }
                                        if ((String)jTemplateR["label"] == "ราคาสินค้า (ก่อนภาษี)")
                                        {
                                            loglabel = "ราคาสินค้า (ก่อนภาษี)";
                                            UpdatejData["value"] = SumPriceBeforeTax.ToString("0.00", CultureInfo.InvariantCulture);
                                        }
                                        if ((String)jTemplateR["label"] == "ภาษีมูลค่าเพิ่ม 7%")
                                        {
                                            loglabel = "ภาษีมูลค่าเพิ่ม 7%";
                                            UpdatejData["value"] = SumVat.ToString("0.00", CultureInfo.InvariantCulture);
                                        }
                                        if ((String)jTemplateR["label"] == "รวมเป็นเงินทั้งสิ้น")
                                        {
                                            loglabel = "รวมเป็นเงินทั้งสิ้น";
                                            UpdatejData["value"] = SumtotalPrice.ToString("0.00", CultureInfo.InvariantCulture);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                if (!string.IsNullOrEmpty(logValue))
                                {
                                    LogError("Error Json : " + ex.Message + "|Docno : " + item.DocumentNo + "|Label : " + loglabel + "|Value : " + logValue);
                                }
                                else
                                {
                                    LogError("Error Json : " + ex.Message + "|Docno : " + item.DocumentNo + "|Label : " + loglabel);
                                }
                                continue;
                            }
                        }
                        string MAdvancveform = JsonConvert.SerializeObject(jsonAdvanceForm);
                        if (!string.IsNullOrEmpty(MAdvancveform))
                        {
                            TRNMemo objMemo = db.TRNMemos.Where(x => x.MemoId == item.MemoId).FirstOrDefault();
                            objMemo.MAdvancveForm = MAdvancveform;
                            db.SubmitChanges();
                            Log("UpdateData ♥♥ Done ♥♥ : " + item.DocumentNo);
                            Console.WriteLine("UpdateData ♥♥ Done ♥♥ : " + item.DocumentNo);
                            Log("------------------------------------------------------");
                        }
                    }
                    #endregion
                }
                catch (Exception ex)
                {
                    LogError("Error UpdateData : " + ex.StackTrace + "|DocNo : " + item.DocumentNo);
                    LogError("------------------------------------------------------------------");
                    continue;
                }
            }
        }
        public static decimal GetdataMSTCatagolyItemStock(string productCode, DataconDataContext db, decimal unitPrice, TRNMemo item)
        {
            var checkunitPrice = db.MSTCatagolyItemStocks.Where(x => x.ItemID == productCode).FirstOrDefault();
            if (checkunitPrice != null)
            {
                Log("---- MSTCatagolyItemStock have (ราคาสินค้าต่อหน่วย) ----");
                Log("ProductCode : " + productCode + "|BeforePrice : " + unitPrice);
                unitPrice = Math.Round(checkunitPrice.ItemPrice, 2);
                Log("AfterPrice : " + unitPrice + "|DocumentNo : " + item.DocumentNo + "|Memoid : " + item.MemoId);
                return unitPrice;
            }
            return 0;
        }
    }
}
