using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Interfaces.CreditBalance;
using Com.Danliris.Service.Finance.Accounting.Lib.Helpers;
using Com.Danliris.Service.Finance.Accounting.Lib.Models.CreditorAccount;
using Com.Danliris.Service.Finance.Accounting.Lib.Services.IdentityService;
using Com.Danliris.Service.Finance.Accounting.Lib.Utilities;
using Com.Danliris.Service.Finance.Accounting.Lib.ViewModels.CreditBalance;
using Com.Moonlay.NetCore.Lib;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using OfficeOpenXml;
using Com.Danliris.Service.Finance.Accounting.Lib.Services.HttpClientService;
using OfficeOpenXml.Style;

namespace Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Services.CreditBalance
{
    public class CreditBalanceService : ICreditBalanceService
    {
        private const string UserAgent = "finance-service";
        protected DbSet<CreditorAccountModel> DbSet;
        protected IIdentityService IdentityService;
        public FinanceDbContext DbContext;

        public CreditBalanceService(IServiceProvider serviceProvider, FinanceDbContext dbContext)
        {
            DbContext = dbContext;
            DbSet = dbContext.Set<CreditorAccountModel>();
            IdentityService = serviceProvider.GetService<IIdentityService>();
        }

        public List<CreditBalanceViewModel> GetReportv2(bool isImport, string suplierName, int month, int year, int offSet, bool isForeignCurrency, int divisionId)
        {
            var query = DbContext.CreditorAccounts.Where(entity => entity.SupplierIsImport == isImport);

            if (isForeignCurrency)
                query = query.Where(entity => entity.CurrencyCode != "IDR");
            else if (!isImport && !isForeignCurrency)
                query = query.Where(entity => entity.CurrencyCode == "IDR");

            DateTimeOffset firstDayOfMonth = new DateTime(year, month, 1);
            DateTimeOffset lastDayOfMonth = firstDayOfMonth.AddMonths(1);
            query = query.Where(entity => (entity.UnitReceiptNoteDate.HasValue && entity.UnitReceiptNoteDate.GetValueOrDefault().AddHours(offSet).DateTime < lastDayOfMonth.DateTime) || (entity.MemoDate.HasValue && entity.MemoDate.GetValueOrDefault().AddHours(offSet).DateTime < lastDayOfMonth.DateTime) || (entity.UnitPaymentCorrectionDate.HasValue && entity.UnitPaymentCorrectionDate.GetValueOrDefault().AddHours(offSet).DateTime < lastDayOfMonth.DateTime) || (entity.BankExpenditureNoteDate.HasValue && entity.BankExpenditureNoteDate.GetValueOrDefault().AddHours(offSet).DateTime < lastDayOfMonth.DateTime));

            if (divisionId > 0)
                query = query.Where(entity => entity.DivisionId == divisionId);

            if (!string.IsNullOrWhiteSpace(suplierName))
                query = query.Where(entity => entity.SupplierName == suplierName);

            var queryResult = query.OrderBy(entity => entity.SupplierName).ToList();

            var result = new List<CreditBalanceViewModel>();

            //var initialBalance = 
            foreach (var item in queryResult)
            {
                var itemResult = new CreditBalanceViewModel()
                {
                    Currency = item.CurrencyCode,
                    CurrencyRate = item.CurrencyRate,
                    DivisionId = item.DivisionId,
                    DivisionName = item.DivisionName,
                    Products = item.Products,
                    SupplierCode = item.SupplierCode,
                    SupplierName = item.SupplierName
                };

                if (item.UnitPaymentCorrectionId == 0)
                {
                    if (item.UnitReceiptNoteDate.HasValue && item.UnitReceiptNoteDate.GetValueOrDefault().AddHours(offSet).DateTime < firstDayOfMonth.DateTime)
                    {
                        itemResult.StartBalance = item.UnitReceiptNoteDPP - item.IncomeTaxAmount;
                        itemResult.StartBalanceCurrency = item.DPPCurrency - (item.IncomeTaxAmount / item.CurrencyRate);
                    }

                    if (item.UnitReceiptNoteDate.HasValue && item.UnitReceiptNoteDate.GetValueOrDefault().AddHours(offSet).Year == year && item.UnitReceiptNoteDate.GetValueOrDefault().AddHours(offSet).Month == month)
                    {
                        itemResult.Purchase = item.UnitReceiptNoteDPP - item.IncomeTaxAmount;
                        itemResult.PurchaseCurrency = item.DPPCurrency - (item.IncomeTaxAmount / item.CurrencyRate);
                    }

                    if (item.MemoDate.HasValue && item.MemoDate.GetValueOrDefault().AddHours(offSet).DateTime < firstDayOfMonth.DateTime)
                    {
                        itemResult.StartBalance += item.UnitReceiptNotePPN;
                        itemResult.StartBalanceCurrency += item.VATAmount;
                    }

                    if (item.MemoDate.HasValue && item.MemoDate.GetValueOrDefault().AddHours(offSet).Year == year && item.MemoDate.GetValueOrDefault().AddHours(offSet).Month == month)
                    {
                        itemResult.Purchase += item.UnitReceiptNotePPN;
                        itemResult.PurchaseCurrency += item.VATAmount;
                    }

                    if (item.BankExpenditureNoteDate.HasValue && item.BankExpenditureNoteDate.GetValueOrDefault().AddHours(offSet).DateTime < firstDayOfMonth.DateTime)
                    {
                        itemResult.StartBalance -= item.BankExpenditureNoteMutation;
                        //itemResult.StartBalance -= item.UnitReceiptNoteDPP;
                        itemResult.StartBalanceCurrency -= item.DPPCurrency;
                        itemResult.StartBalanceCurrency -= item.VATAmount;
                    }

                    if (item.BankExpenditureNoteDate.HasValue && item.BankExpenditureNoteDate.GetValueOrDefault().AddHours(offSet).Year == year && item.BankExpenditureNoteDate.GetValueOrDefault().AddHours(offSet).Month == month)
                    {
                        itemResult.Payment = item.BankExpenditureNoteMutation;
                        itemResult.PaymentCurrency = item.DPPCurrency + item.VATAmount - (item.IncomeTaxAmount / item.CurrencyRate);
                    }
                }
                else
                {
                    if (item.UnitPaymentCorrectionDate.HasValue && item.UnitPaymentCorrectionDate.GetValueOrDefault().AddHours(offSet).DateTime < firstDayOfMonth.DateTime)
                    {
                        itemResult.StartBalance += (item.UnitPaymentCorrectionMutation);
                        itemResult.StartBalanceCurrency += (item.UnitPaymentCorrectionMutation / item.CurrencyRate);
                    }

                    if (item.UnitPaymentCorrectionDate.HasValue && item.UnitPaymentCorrectionDate.GetValueOrDefault().AddHours(offSet).Year == year && item.UnitPaymentCorrectionDate.GetValueOrDefault().AddHours(offSet).Month == month)
                    {
                        itemResult.Purchase = (item.UnitPaymentCorrectionMutation);
                        itemResult.PurchaseCurrency = (item.UnitPaymentCorrectionMutation / item.CurrencyRate);
                    }
                }

                result.Add(itemResult);
            }

            if (result.Count > 0)
            {
                result = result
                    .GroupBy(element => new { element.SupplierName, element.DivisionId, element.Currency })
                    .Select(element => new CreditBalanceViewModel()
                    {
                        Currency = element.Key.Currency,
                        CurrencyRate = element.FirstOrDefault().CurrencyRate,
                        DivisionId = element.Key.DivisionId,
                        DivisionName = element.FirstOrDefault().DivisionName,
                        FinalBalance = element.Sum(sum => sum.StartBalance + sum.Purchase - sum.Payment),
                        FinalBalanceCurrency = element.Sum(sum => sum.StartBalanceCurrency + sum.PurchaseCurrency - sum.PaymentCurrency),
                        PaidAmount = element.Sum(sum => sum.PaidAmount),
                        PaidAmountCurrency = element.Sum(sum => sum.PaidAmountCurrency),
                        Payment = element.Sum(sum => sum.Payment),
                        PaymentCurrency = element.Sum(sum => sum.PaymentCurrency),
                        Products = string.Join('\n', element.Select(select => select.Products)),
                        Purchase = element.Sum(sum => sum.Purchase),
                        PurchaseCurrency = element.Sum(sum => sum.PurchaseCurrency),
                        StartBalance = element.Sum(sum => sum.StartBalance),
                        StartBalanceCurrency = element.Sum(sum => sum.StartBalanceCurrency),
                        SupplierCode = element.FirstOrDefault().SupplierCode,
                        SupplierName = element.FirstOrDefault().SupplierName
                    })
                    .ToList();
            }

            return result.OrderBy(element => element.Currency).ThenBy(element => element.SupplierName).ToList();
        }

        public List<CreditBalanceDetailViewModel> GetReportDetailData(bool isImport, string supplierCode, int month, int year, int offSet, bool isForeignCurrency, int divisionId)
        {
            var query = DbContext.CreditorAccounts.Where(entity => entity.SupplierIsImport == isImport);

            if (isForeignCurrency)
                query = query.Where(entity => entity.CurrencyCode != "IDR");
            else if (!isImport && !isForeignCurrency)
                query = query.Where(entity => entity.CurrencyCode == "IDR");

            DateTimeOffset firstDayOfMonth = new DateTime(year, month, 1);
            DateTimeOffset lastDayOfMonth = firstDayOfMonth.AddMonths(1);
            query = query.Where(entity => (entity.UnitReceiptNoteDate.HasValue && entity.UnitReceiptNoteDate.GetValueOrDefault().AddHours(offSet).DateTime < lastDayOfMonth.DateTime) || (entity.MemoDate.HasValue && entity.MemoDate.GetValueOrDefault().AddHours(offSet).DateTime < lastDayOfMonth.DateTime) || (entity.UnitPaymentCorrectionDate.HasValue && entity.UnitPaymentCorrectionDate.GetValueOrDefault().AddHours(offSet).DateTime < lastDayOfMonth.DateTime) || (entity.BankExpenditureNoteDate.HasValue && entity.BankExpenditureNoteDate.GetValueOrDefault().AddHours(offSet).DateTime < lastDayOfMonth.DateTime));

            if (divisionId > 0)
                query = query.Where(entity => entity.DivisionId == divisionId);

            if (!string.IsNullOrWhiteSpace(supplierCode))
                query = query.Where(entity => entity.SupplierCode == supplierCode);

            var queryResult = query.OrderBy(entity => entity.SupplierName).ToList();

            var result = new List<CreditBalanceDetailViewModel>();

            //var initialBalance = 
            foreach (var item in queryResult)
            {
                var itemResult = new CreditBalanceDetailViewModel()
                {
                    Currency = item.CurrencyCode,
                    CurrencyRate = item.CurrencyRate,
                    DivisionId = item.DivisionId,
                    DivisionName = item.DivisionName,
                    Products = item.Products,
                    SupplierCode = item.SupplierCode,
                    SupplierName = item.SupplierName
                };

                if (item.UnitPaymentCorrectionId == 0)
                {
                    if (item.UnitReceiptNoteDate.HasValue && item.UnitReceiptNoteDate.GetValueOrDefault().AddHours(offSet).DateTime < lastDayOfMonth.DateTime)
                    {
                        itemResult.Purchase = item.DPPCurrency - (item.IncomeTaxAmount / item.CurrencyRate);
                        itemResult.UnitReceiptNoteNo = item.UnitReceiptNoteNo;
                        itemResult.Date = item.UnitReceiptNoteDate;
                        itemResult.IncomeTaxNo = item.IncomeTaxNo;
                    }

                    if (item.MemoDate.HasValue && item.MemoDate.GetValueOrDefault().AddHours(offSet).DateTime < lastDayOfMonth.DateTime)
                    {
                        itemResult.Purchase += item.VATAmount;
                        itemResult.UnitPaymentOrderNo = item.MemoNo;
                        itemResult.InvoiceNo = item.InvoiceNo;
                    }

                    if (item.BankExpenditureNoteDate.HasValue && item.BankExpenditureNoteDate.GetValueOrDefault().AddHours(offSet).DateTime < lastDayOfMonth.DateTime)
                    {
                        itemResult.BankExpenditureNoteNo = item.BankExpenditureNoteNo;
                        itemResult.Payment = item.BankExpenditureNoteMutation / item.CurrencyRate;
                    }
                }
                else
                {
                    if (item.UnitPaymentCorrectionDate.HasValue && item.UnitPaymentCorrectionDate.GetValueOrDefault().AddHours(offSet).DateTime < lastDayOfMonth.DateTime)
                    {
                        itemResult.Purchase = item.UnitPaymentCorrectionMutation / item.CurrencyRate;
                    }
                }

                result.Add(itemResult);
            }
            //else
            //    queryRemainingBalance = query.Where(x => x.UnitReceiptNoteDate.HasValue && x.UnitReceiptNoteDate.Value.DateTime < firstDayOfMonth);

            //var result = query.Select(entity => new CreditBalanceDetailViewModel()
            //{
            //    //StartBalance = DbSet
            //    //.AsQueryable()
            //    //.Where(x => x.SupplierCode == item.Key.SupplierCode && x.DivisionCode == item.Key.DivisionCode && x.CurrencyCode == item.Key.CurrencyCode && x.UnitReceiptNoteDate.HasValue && x.UnitReceiptNoteDate.Value.DateTime < firstDayOfMonth)
            //    //.ToList().Sum(x => x.UnitReceiptMutation - x.BankExpenditureNoteMutation),
            //    //StartBalance = joinSumBalanceStart
            //    //    .Where(x => x.DivisionCode == item.Key.DivisionCode && x.CurrencyCode == item.Key.CurrencyCode && x.UnitReceiptNoteDate.HasValue && x.UnitReceiptNoteDate.Value.DateTime < firstDayOfMonth)
            //    //    .Sum(x => x.UnitReceiptMutation - x.BankExpenditureNoteMutation),
            //    Products = entity.Products,
            //    Purchase = entity.UnitReceiptMutation,
            //    DPPAmount = entity.UnitReceiptMutation,
            //    IncomeTaxAmount = entity.IncomeTaxAmount,
            //    VATAmount = entity.VATAmount,
            //    Payment = entity.BankExpenditureNoteMutation,
            //    SupplierName = entity.SupplierName,
            //    Currency = entity.CurrencyCode,
            //    CurrencyRate = entity.CurrencyRate,
            //    DivisionName = entity.DivisionName,
            //    Date = entity.UnitReceiptNoteDate,
            //    UnitPaymentOrderNo = entity.MemoNo,
            //    UnitReceiptNoteNo = entity.UnitReceiptNoteNo,
            //    PaidAmount = entity.PurchasingMemoAmount,
            //    InvoiceNo = entity.InvoiceNo

            //}).ToList();

            return result.Where(element => element.Total != 0).OrderBy(x => x.Currency).ThenBy(x => x.Products).ThenBy(x => x.SupplierName).ToList();
        }

        //public List<CreditBalanceDetailViewModel> GetReportDetailData(bool isImport, string supplierCode, int month, int year, int offSet, bool isForeignCurrency, int divisionId)
        //{
        //    var firstDayOfMonth = new DateTime(year, month, 1);

        //    IQueryable<CreditorAccountModel> query = DbContext.CreditorAccounts.Where(x => x.SupplierIsImport == isImport).AsQueryable();


        //    List<CreditBalanceDetailViewModel> result = new List<CreditBalanceDetailViewModel>();
        //    int previousMonth = month - 1;
        //    int previousYear = year;

        //    if (previousMonth == 0)
        //    {
        //        previousMonth = 12;
        //        previousYear = year - 1;
        //    }


        //    if (isForeignCurrency)
        //        query = query.Where(entity => entity.CurrencyCode != "IDR");
        //    //else

        //    if (!isImport && !isForeignCurrency)
        //        query = query.Where(entity => entity.CurrencyCode == "IDR");

        //    if (divisionId > 0)
        //        query = query.Where(entity => entity.DivisionId == divisionId);

        //    var queryRemainingBalance = query;
        //    if (!string.IsNullOrEmpty(supplierCode))
        //        query = query.Where(x => x.SupplierCode == supplierCode);
        //    else
        //        queryRemainingBalance = query.Where(x => x.UnitReceiptNoteDate.HasValue && x.UnitReceiptNoteDate.Value.DateTime < firstDayOfMonth);

        //    //query = query.Where(x => x.UnitReceiptNoteDate.HasValue && x.UnitReceiptNoteDate.Value.Month == month && x.UnitReceiptNoteDate.Value.Year == year);

        //    var data = query.Select(item => new CreditBalanceAccountViewModel(item)).ToList();

        //    if (string.IsNullOrEmpty(supplierCode))
        //        data.AddRange(queryRemainingBalance.Select(item => new CreditBalanceAccountViewModel(item)).ToList());

        //    var grouppedData = data.GroupBy(x => new { x.DivisionCode, x.CurrencyCode }).ToList();
        //    var grouppedDataKey = grouppedData.Select(s => s.Key).ToList();
        //    var joinSumBalanceStart = DbSet.Where(x => x.UnitReceiptNoteDate.HasValue && x.UnitReceiptNoteDate.Value.DateTime < firstDayOfMonth).Join(grouppedDataKey,
        //        grp => new { grp.DivisionCode, grp.CurrencyCode },
        //        sum => new { sum.DivisionCode, sum.CurrencyCode },
        //        (grp, sum) => new
        //        {
        //            grp.SupplierCode,
        //            grp.CurrencyCode,
        //            grp.DivisionCode,
        //            grp.UnitReceiptNoteDate,
        //            grp.UnitReceiptNoteNo,
        //            grp.UnitReceiptMutation,
        //            grp.BankExpenditureNoteMutation,
        //            grp.PurchasingMemoAmount
        //        }
        //        )
        //            .AsQueryable()
        //            //.Where(x => x.SupplierCode == item.Key.SupplierCode && x.DivisionCode == item.Key.DivisionCode && x.CurrencyCode == item.Key.CurrencyCode && x.UnitReceiptNoteDate.HasValue && x.UnitReceiptNoteDate.Value.DateTime < firstDayOfMonth)
        //            .ToList();
        //    foreach (var item in grouppedData)
        //    {
        //        var productsUnion = string.Join("\n", item.Where(x => x.UnitReceiptNoteDate.HasValue && x.UnitReceiptNoteDate.Value.Month == month && x.UnitReceiptNoteDate.Value.Year == year).Select(x => x.Products).ToList());
        //        var uniqueProducts = string.Join("\n", productsUnion.Split("\n").Distinct());
        //        //var now = DateTimeOffset.Now;

        //        var creditBalance = new CreditBalanceDetailViewModel()
        //        {
        //            //StartBalance = DbSet
        //            //.AsQueryable()
        //            //.Where(x => x.SupplierCode == item.Key.SupplierCode && x.DivisionCode == item.Key.DivisionCode && x.CurrencyCode == item.Key.CurrencyCode && x.UnitReceiptNoteDate.HasValue && x.UnitReceiptNoteDate.Value.DateTime < firstDayOfMonth)
        //            //.ToList().Sum(x => x.UnitReceiptMutation - x.BankExpenditureNoteMutation),
        //            StartBalance = joinSumBalanceStart
        //            .Where(x => x.DivisionCode == item.Key.DivisionCode && x.CurrencyCode == item.Key.CurrencyCode && x.UnitReceiptNoteDate.HasValue && x.UnitReceiptNoteDate.Value.DateTime < firstDayOfMonth)
        //            .Sum(x => x.UnitReceiptMutation - x.BankExpenditureNoteMutation),
        //            Products = uniqueProducts,
        //            Purchase = item.Where(x => x.UnitReceiptNoteDate.HasValue && x.UnitReceiptNoteDate.Value.Month == month && x.UnitReceiptNoteDate.Value.Year == year).Sum(x => x.UnitReceiptMutation),
        //            DPPAmount = item.Where(x => x.UnitReceiptNoteDate.HasValue && x.UnitReceiptNoteDate.Value.Month == month && x.UnitReceiptNoteDate.Value.Year == year).Sum(x => x.UnitReceiptMutation),
        //            IncomeTaxAmount = item.Where(x => x.UnitReceiptNoteDate.HasValue && x.UnitReceiptNoteDate.Value.Month == month && x.UnitReceiptNoteDate.Value.Year == year).Sum(x => x.IncomeTaxAmount),
        //            VATAmount = item.Where(x => x.UnitReceiptNoteDate.HasValue && x.UnitReceiptNoteDate.Value.Month == month && x.UnitReceiptNoteDate.Value.Year == year).Sum(x => x.VATAmount),
        //            Payment = item.Where(x => x.UnitReceiptNoteDate.HasValue && x.UnitReceiptNoteDate.Value.Month == month && x.UnitReceiptNoteDate.Value.Year == year).Sum(x => x.BankExpenditureNoteMutation),
        //            FinalBalance = item.Sum(x => x.FinalBalance),
        //            SupplierName = item.FirstOrDefault() == null ? "" : item.FirstOrDefault().SupplierName ?? "",
        //            Currency = item.FirstOrDefault() == null ? "" : item.FirstOrDefault().CurrencyCode ?? "",
        //            CurrencyRate = item.FirstOrDefault() == null ? 1 : item.FirstOrDefault().CurrencyRate,
        //            DivisionName = item.FirstOrDefault() == null ? "" : item.FirstOrDefault().DivisionName ?? "",
        //            Date = item.Where(x => x.UnitReceiptNoteDate.HasValue && x.UnitReceiptNoteDate.Value.Month == month && x.UnitReceiptNoteDate.Value.Year == year).FirstOrDefault() == null ? null : item.Where(x => x.UnitReceiptNoteDate.HasValue && x.UnitReceiptNoteDate.Value.Month == month && x.UnitReceiptNoteDate.Value.Year == year).FirstOrDefault().UnitReceiptNoteDate ?? null,
        //            UnitPaymentOrderNo = item.Where(x => x.UnitReceiptNoteDate.HasValue && x.UnitReceiptNoteDate.Value.Month == month && x.UnitReceiptNoteDate.Value.Year == year).FirstOrDefault() == null ? null : item.Where(x => x.UnitReceiptNoteDate.HasValue && x.UnitReceiptNoteDate.Value.Month == month && x.UnitReceiptNoteDate.Value.Year == year).FirstOrDefault().UnitPaymentOrderNo ?? null,
        //            UnitReceiptNoteNo = item.Where(x => x.UnitReceiptNoteDate.HasValue && x.UnitReceiptNoteDate.Value.Month == month && x.UnitReceiptNoteDate.Value.Year == year).FirstOrDefault() == null ? null : item.Where(x => x.UnitReceiptNoteDate.HasValue && x.UnitReceiptNoteDate.Value.Month == month && x.UnitReceiptNoteDate.Value.Year == year).FirstOrDefault().UnitReceiptNoteNo ?? null,
        //            PaidAmount = item.Where(x => x.UnitReceiptNoteDate.HasValue && x.UnitReceiptNoteDate.Value.Month == month && x.UnitReceiptNoteDate.Value.Year == year).Sum(x => x.PaidAmount)

        //        };

        //        creditBalance.FinalBalance = creditBalance.StartBalance + creditBalance.Purchase - creditBalance.Payment;
        //        result.Add(creditBalance);
        //    }

        //    return result.OrderBy(x => x.Currency).ThenBy(x => x.Products).ThenBy(x => x.SupplierName).ToList();
        //}


        public MemoryStream GenerateExcel(bool isImport, string suplierName, int month, int year, int offSet, bool isForeignCurrency, int divisionId)
        {
            var data = GetReportv2(isImport, suplierName, month, year, offSet, isForeignCurrency, divisionId).OrderBy(element => element.SupplierName).ToList();

            var divisionName = "SEMUA DIVISI";

            if (divisionId > 0)
            {
                var summary = data.FirstOrDefault();
                if (summary != null)
                {
                    divisionName = $"DIVISI {summary.DivisionName}";
                }
            }

            DataTable dt = new DataTable();

            // v1
            //dt.Columns.Add(new DataColumn() { ColumnName = "Mata Uang", DataType = typeof(string) });
            //dt.Columns.Add(new DataColumn() { ColumnName = "Supplier", DataType = typeof(string) });
            //dt.Columns.Add(new DataColumn() { ColumnName = "Saldo Awal", DataType = typeof(string) });
            //dt.Columns.Add(new DataColumn() { ColumnName = "Pembelian", DataType = typeof(string) });
            //dt.Columns.Add(new DataColumn() { ColumnName = "Pembayaran", DataType = typeof(string) });
            //dt.Columns.Add(new DataColumn() { ColumnName = "Saldo Akhir", DataType = typeof(string) });

            //v2 
            dt.Columns.Add(new DataColumn() { ColumnName = "Supplier", DataType = typeof(string) });
            dt.Columns.Add(new DataColumn() { ColumnName = "Divisi", DataType = typeof(string) });
            dt.Columns.Add(new DataColumn() { ColumnName = "Mata Uang", DataType = typeof(string) });
            dt.Columns.Add(new DataColumn() { ColumnName = "Saldo Awal", DataType = typeof(string) });
            dt.Columns.Add(new DataColumn() { ColumnName = "Pembelian", DataType = typeof(string) });
            dt.Columns.Add(new DataColumn() { ColumnName = "Pembayaran", DataType = typeof(string) });
            dt.Columns.Add(new DataColumn() { ColumnName = "Pelunasan", DataType = typeof(string) });
            dt.Columns.Add(new DataColumn() { ColumnName = "Saldo Akhir", DataType = typeof(string) });

            if (isImport)
            {
                dt.Columns.Add(new DataColumn() { ColumnName = "Saldo Awal (IDR)", DataType = typeof(string) });
                dt.Columns.Add(new DataColumn() { ColumnName = "Pembelian (IDR)", DataType = typeof(string) });
                dt.Columns.Add(new DataColumn() { ColumnName = "Pembayaran (IDR)", DataType = typeof(string) });
                dt.Columns.Add(new DataColumn() { ColumnName = "Saldo Akhir (IDR)", DataType = typeof(string) });
            }

            int index = 0;
            if (data.Count == 0)
            {
                if (isImport)
                {
                    dt.Rows.Add("", "", "", "", "", "", "", "", "", "", "", "");
                }
                else
                {
                    dt.Rows.Add("", "", "", "", "", "", "", "");
                }
            }
            else
            {
                foreach (var item in data)
                {
                    // v1
                    //dt.Rows.Add(item.Currency, item.SupplierName, item.StartBalance.ToString("#,##0"), item.Purchase.ToString("#,##0"),
                    //    item.Payment.ToString("#,##0"), item.FinalBalance.ToString("#,##0"));

                    // v2

                    if (isImport)
                    {
                        dt.Rows.Add(item.SupplierName, item.DivisionName, item.Currency, item.StartBalanceCurrency.ToString("#,##0.#0"), item.PurchaseCurrency.ToString("#,##0.#0"),
                                item.PaymentCurrency.ToString("#,##0.#0"), item.PaidAmountCurrency.ToString("#,##0.#0"), (item.FinalBalanceCurrency).ToString("#,##0.#0"),
                                (item.StartBalance).ToString("#,##0.#0"), (item.Purchase).ToString("#,##0.#0"), (item.Payment).ToString("#,##0.#0"),
                                (item.FinalBalance).ToString("#,##0.#0"));
                        index++;
                    }
                    else
                    {
                        dt.Rows.Add(item.SupplierName, item.DivisionName, item.Currency, item.StartBalance.ToString("#,##0.#0"), item.Purchase.ToString("#,##0.#0"),
                                item.Payment.ToString("#,##0.#0"), item.PaidAmount.ToString("#,##0.#0"), item.FinalBalance.ToString("#,##0.#0"));
                        index++;
                    }
                }
            }

            return CreateExcel(isImport, month, year, divisionName, new List<KeyValuePair<DataTable, string>>() { new KeyValuePair<DataTable, string>(dt, "Saldo Hutang") }, true);
        }


        public List<CreditBalanceViewModel> GeneratePdf(bool isImport, string suplierName, int month, int year, int offSet, bool isForeignCurrency, int divisionId)
        {
            var data = GetReportv2(isImport, suplierName, month, year, offSet, isForeignCurrency, divisionId).OrderBy(element => element.SupplierName).ToList();

            return data;
        }

        public ReadResponse<CreditBalanceViewModel> GetReport(bool isImport, int page, int size, string suplierName, int month, int year, int offSet, bool isForeignCurrency, int divisionId)
        {
            var queries = GetReportv2(isImport, suplierName, month, year, offSet, isForeignCurrency, divisionId);

            Pageable<CreditBalanceViewModel> pageable = new Pageable<CreditBalanceViewModel>(queries, page - 1, size);
            List<CreditBalanceViewModel> data = pageable.Data.ToList();

            return new ReadResponse<CreditBalanceViewModel>(queries.OrderBy(element => element.SupplierName).ToList(), pageable.TotalCount, new Dictionary<string, string>(), new List<string>());
        }

        private MemoryStream CreateExcel(bool isImport, int month, int year, string divisionName, List<KeyValuePair<DataTable, string>> dtSourceList, bool styling = false, int index = 0)
        {
            ExcelPackage package = new ExcelPackage();
            foreach (KeyValuePair<DataTable, string> item in dtSourceList)
            {
                var sheet = package.Workbook.Worksheets.Add(item.Value);

                var lastDate = new DateTime(year, month, DateTime.DaysInMonth(year, month));

                sheet.Cells["A1:B4"].Style.Font.Size = 12;
                sheet.Cells["A1:B4"].Style.Font.Bold = true;
                sheet.Cells["A1:B1"].Merge = true;
                sheet.Cells["A2:B2"].Merge = true;
                sheet.Cells["A3:B3"].Merge = true;
                sheet.Cells["A4:B4"].Merge = true;
                sheet.Cells["A1"].Value = "PT DANLIRIS";
                sheet.Cells["A4"].Value = divisionName;

                sheet.Cells["A3"].Value = "PER " + lastDate.ToString("dd MMMM yyyy").ToUpper();
                sheet.Cells["A5"].LoadFromDataTable(item.Key, true, (styling == true) ? OfficeOpenXml.Table.TableStyles.Light16 : OfficeOpenXml.Table.TableStyles.None);
                sheet.Cells[sheet.Dimension.Address].AutoFitColumns();

                //int cells = 6;
                if (isImport)
                {
                    sheet.Cells["A2"].Value = "LAPORAN SALDO HUTANG IMPOR";
                    //sheet.Cells[$"C{cells}:K{cells + index}"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                }
                else
                {
                    sheet.Cells["A2"].Value = "LAPORAN SALDO HUTANG LOKAL";
                    //sheet.Cells[$"C{cells}:G{cells + index}"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                }
            }
            MemoryStream stream = new MemoryStream();
            package.SaveAs(stream);
            return stream;
        }

        public ReadResponse<CreditBalanceDetailViewModel> GetReportDetail(bool isImport, string supplierCode, int month, int year, int offSet, bool isForeignCurrency, int divisionId)
        {
            var data = GetReportDetailData(isImport, supplierCode, month, year, offSet, isForeignCurrency, divisionId);

            return new ReadResponse<CreditBalanceDetailViewModel>(data.OrderBy(element => element.SupplierName).ThenBy(element => element.Date).ToList(), data.Count, new Dictionary<string, string>(), new List<string>());
        }

        public MemoryStream GenerateExcelDetail(ReadResponse<CreditBalanceDetailViewModel> data, int divisionId, int month, int year)
        {
            var divisionName = "SEMUA DIVISI";

            if (divisionId > 0)
            {
                var summary = data.Data.FirstOrDefault();
                if (summary != null)
                {
                    divisionName = $"DIVISI {summary.DivisionName}";
                }
            }

            DataTable dt = new DataTable();

            // v1
            //dt.Columns.Add(new DataColumn() { ColumnName = "Mata Uang", DataType = typeof(string) });
            //dt.Columns.Add(new DataColumn() { ColumnName = "Supplier", DataType = typeof(string) });
            //dt.Columns.Add(new DataColumn() { ColumnName = "Saldo Awal", DataType = typeof(string) });
            //dt.Columns.Add(new DataColumn() { ColumnName = "Pembelian", DataType = typeof(string) });
            //dt.Columns.Add(new DataColumn() { ColumnName = "Pembayaran", DataType = typeof(string) });
            //dt.Columns.Add(new DataColumn() { ColumnName = "Saldo Akhir", DataType = typeof(string) });

            //v2 
            dt.Columns.Add(new DataColumn() { ColumnName = "Tanggal", DataType = typeof(string) });
            //dt.Columns.Add(new DataColumn() { ColumnName = "No PO", DataType = typeof(string) });
            dt.Columns.Add(new DataColumn() { ColumnName = "Nomor Bon Penerimaan", DataType = typeof(string) });
            dt.Columns.Add(new DataColumn() { ColumnName = "Supplier", DataType = typeof(string) });
            dt.Columns.Add(new DataColumn() { ColumnName = "No Faktur Pajak", DataType = typeof(string) });
            dt.Columns.Add(new DataColumn() { ColumnName = "No Invoice", DataType = typeof(string) });
            dt.Columns.Add(new DataColumn() { ColumnName = "No SPB/NI", DataType = typeof(string) });
            //dt.Columns.Add(new DataColumn() { ColumnName = "DPP", DataType = typeof(string) });
            //dt.Columns.Add(new DataColumn() { ColumnName = "PPN", DataType = typeof(string) });
            //dt.Columns.Add(new DataColumn() { ColumnName = "PPh", DataType = typeof(string) });
            dt.Columns.Add(new DataColumn() { ColumnName = "Total", DataType = typeof(string) });
            //dt.Columns.Add(new DataColumn() { ColumnName = "Pembelian", DataType = typeof(string) });
            //dt.Columns.Add(new DataColumn() { ColumnName = "Pembayaran", DataType = typeof(string) });
            //dt.Columns.Add(new DataColumn() { ColumnName = "Nomor Pembayaran", DataType = typeof(string) });

            //int index = 0;
            if (data.Count == 0)
            {
                dt.Rows.Add("", "", "", "", "", "", "");
            }
            else
            {
                foreach (var item in data.Data)
                {
                    // v1
                    //dt.Rows.Add(item.Currency, item.SupplierName, item.StartBalance.ToString("#,##0"), item.Purchase.ToString("#,##0"),
                    //    item.Payment.ToString("#,##0"), item.FinalBalance.ToString("#,##0"));

                    // v2

                    dt.Rows.Add(item.Date?.ToString("dd/MM/yyyy"), item.UnitReceiptNoteNo, item.SupplierName, item.IncomeTaxNo, item.InvoiceNo, item.UnitPaymentOrderNo, item.Total.ToString("#,##0.#0"));
                }
            }


            return CreateExcelDetail(month, year, divisionName, new List<KeyValuePair<DataTable, string>>() { new KeyValuePair<DataTable, string>(dt, "Rincian Saldo Hutang") }, true);
        }

        private MemoryStream CreateExcelDetail(int month, int year, string divisionName, List<KeyValuePair<DataTable, string>> dtSourceList, bool styling = false, int index = 0)
        {
            ExcelPackage package = new ExcelPackage();
            foreach (KeyValuePair<DataTable, string> item in dtSourceList)
            {
                var sheet = package.Workbook.Worksheets.Add(item.Value);

                var lastDate = new DateTime(year, month, DateTime.DaysInMonth(year, month));

                sheet.Cells["A1:B4"].Style.Font.Size = 12;
                sheet.Cells["A1:B4"].Style.Font.Bold = true;
                sheet.Cells["A1:B1"].Merge = true;
                sheet.Cells["A2:B2"].Merge = true;
                sheet.Cells["A3:B3"].Merge = true;
                sheet.Cells["A4:B4"].Merge = true;
                sheet.Cells["A1"].Value = "PT DANLIRIS";
                sheet.Cells["A4"].Value = divisionName;

                sheet.Cells["A3"].Value = "PER " + lastDate.ToString("dd MMMM yyyy").ToUpper();
                sheet.Cells["A5"].LoadFromDataTable(item.Key, true, (styling == true) ? OfficeOpenXml.Table.TableStyles.Light16 : OfficeOpenXml.Table.TableStyles.None);
                sheet.Cells[sheet.Dimension.Address].AutoFitColumns();

                //int cells = 6;
                sheet.Cells["A2"].Value = "RINCIAN SALDO HUTANG";
            }
            MemoryStream stream = new MemoryStream();
            package.SaveAs(stream);
            return stream;
        }
    }
}
