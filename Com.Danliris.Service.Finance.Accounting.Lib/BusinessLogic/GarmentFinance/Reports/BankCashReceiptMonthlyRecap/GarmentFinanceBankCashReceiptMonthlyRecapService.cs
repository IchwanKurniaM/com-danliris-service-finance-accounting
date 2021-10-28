using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Com.Danliris.Service.Finance.Accounting.Lib.Services.IdentityService;
using Com.Danliris.Service.Finance.Accounting.Lib.ViewModels.GarmentFinance.Report.BankCashReceiptMonthlyRecap;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Data;
using OfficeOpenXml;

namespace Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.GarmentFinance.Reports.BankCashReceiptMonthlyRecap
{
    public class GarmentFinanceBankCashReceiptMonthlyRecapService : IGarmentFinanceBankCashReceiptMonthlyRecapService
    {
        private readonly IServiceProvider _serviceProvider;
        private const string UserAgent = "finance-service";
        private readonly FinanceDbContext _dbContext;
        private readonly IIdentityService _identityService;

        public GarmentFinanceBankCashReceiptMonthlyRecapService(IServiceProvider serviceProvider, FinanceDbContext dbContext)
        {
            _dbContext = dbContext;

            _identityService = serviceProvider.GetService<IIdentityService>();
            _serviceProvider = serviceProvider;
        }

        public List<GarmentFinanceBankCashReceiptMonthlyRecapViewModel> GetReportQuery(DateTimeOffset? dateFrom, DateTimeOffset? dateTo, int offset)
        {
            DateTimeOffset dateFromFilter = (dateFrom == null ? new DateTime(1970, 1, 1) : dateFrom.Value.Date);
            DateTimeOffset dateToFilter = (dateTo == null ? DateTimeOffset.UtcNow.Date : dateTo.Value.Date);

            List<GarmentFinanceBankCashReceiptMonthlyRecapViewModel> data = new List<GarmentFinanceBankCashReceiptMonthlyRecapViewModel>();
            var headerIds = _dbContext.GarmentFinanceBankCashReceiptDetails
                        .Where(a => a.BankCashReceiptDate >= dateFromFilter && a.BankCashReceiptDate <= dateToFilter).Select(a=>a.Id).ToList();
            var headerDebit = _dbContext.GarmentFinanceBankCashReceiptDetails.Where(a => headerIds.Contains(a.Id))
                            .Select(a => new GarmentFinanceBankCashReceiptMonthlyRecapViewModel
                            {
                                AccountName = a.DebitCoaName,
                                AccountNo = a.DebitCoaCode,
                                Credit = 0,
                                Debit = a.Amount
                            });
            var first = headerDebit.ToList()
                .GroupBy(x => new { x.AccountName, x.AccountNo }, (key, group) => new GarmentFinanceBankCashReceiptMonthlyRecapViewModel
                {
                    AccountName = key.AccountName,
                    AccountNo = key.AccountNo,
                    Credit = group.Sum(a => a.Credit),
                    Debit = group.Sum(a => a.Debit)
                }).OrderByDescending(a => a.Debit).ThenBy(s => s.AccountNo);
            foreach (var i in first)
            {
                GarmentFinanceBankCashReceiptMonthlyRecapViewModel row = new GarmentFinanceBankCashReceiptMonthlyRecapViewModel
                {
                    AccountName = i.AccountName,
                    AccountNo = i.AccountNo,
                    Credit = i.Credit,
                    Debit = i.Debit
                };
                data.Add(row);
            }

            var headerCredit= _dbContext.GarmentFinanceBankCashReceiptDetails.Where(a => headerIds.Contains(a.Id))
                                .Select(a => new GarmentFinanceBankCashReceiptMonthlyRecapViewModel
                                {
                                    AccountName = a.InvoiceCoaName,
                                    AccountNo = a.InvoiceCoaCode,
                                    Credit = (_dbContext.GarmentFinanceBankCashReceiptDetailItems.Where(x=>x.BankCashReceiptDetailId==a.Id).Sum(i=>i.Amount)),
                                    Debit = 0
                                });
            var otherDetails = _dbContext.GarmentFinanceBankCashReceiptDetailOtherItems.Where(a => headerIds.Contains(a.BankCashReceiptDetailId))
                               .Select(a => new GarmentFinanceBankCashReceiptMonthlyRecapViewModel
                               {
                                   AccountName = a.ChartOfAccountName,
                                   AccountNo = a.ChartOfAccountCode,
                                   Credit = a.TypeAmount == "KREDIT" ? a.Amount : 0,
                                   Debit = a.TypeAmount == "KREDIT" ? 0 : a.Amount
                               });
            var unionQuery = headerCredit.Union(otherDetails);
            var sumQuery = unionQuery.ToList()
                .GroupBy(x => new { x.AccountName, x.AccountNo }, (key, group) => new
            {
                name = key.AccountName,
                code = key.AccountNo,
                credit = group.Sum(a => a.Credit),
                debit = group.Sum(a => a.Debit)
            }).OrderBy(s => s.code).ThenByDescending(a => a.debit); 

            foreach(var item in sumQuery)
            {
                GarmentFinanceBankCashReceiptMonthlyRecapViewModel recap = new GarmentFinanceBankCashReceiptMonthlyRecapViewModel
                {
                    AccountName = item.name,
                    AccountNo = item.code,
                    Credit = item.credit,
                    Debit = item.debit
                };
                data.Add(recap);
            }

            GarmentFinanceBankCashReceiptMonthlyRecapViewModel total = new GarmentFinanceBankCashReceiptMonthlyRecapViewModel
            {
                AccountName = "",
                AccountNo = "TOTAL",
                Credit = sumQuery.Sum(a => a.credit),
                Debit = sumQuery.Sum(a => a.debit)
            };
            data.Add(total);
            return data;
        }

        public Tuple<MemoryStream, string> GenerateExcel(DateTimeOffset? dateFrom, DateTimeOffset? dateTo, int offset)
        {
            var Query = GetReportQuery(dateFrom, dateTo, offset);

            DataTable result = new DataTable();

            result.Columns.Add(new DataColumn() { ColumnName = "No Akun", DataType = typeof(String) });
            result.Columns.Add(new DataColumn() { ColumnName = "Nama Akun", DataType = typeof(String) });
            result.Columns.Add(new DataColumn() { ColumnName = "Debet", DataType = typeof(double) });
            result.Columns.Add(new DataColumn() { ColumnName = "Kredit", DataType = typeof(double) });
            if (Query.ToArray().Count() <= 1)
                result.Rows.Add("", "", 0, 0); // to allow column name to be generated properly for empty data as template
            else
            {
                foreach (var item in Query)
                {
                    result.Rows.Add(item.AccountNo,  item.AccountName, item.Debit, item.Credit);
                }

            }
            ExcelPackage package = new ExcelPackage();
            var sheet = package.Workbook.Worksheets.Add("Rekap Memo per Bulan");
            sheet.Cells["A1"].LoadFromDataTable(result, true, OfficeOpenXml.Table.TableStyles.Light16);

            sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
            sheet.Cells[sheet.Dimension.Address].Style.WrapText = true;

            MemoryStream streamExcel = new MemoryStream();
            package.SaveAs(streamExcel);

            //Dictionary<string, string> FilterDictionary = new Dictionary<string, string>(JsonConvert.DeserializeObject<Dictionary<string, string>>(filter), StringComparer.OrdinalIgnoreCase);
            string fileName = string.Concat("Rekap Memo per Bulan ", DateTime.Now.Date, ".xlsx");

            return Tuple.Create(streamExcel, fileName);
        }

        public List<GarmentFinanceBankCashReceiptMonthlyRecapViewModel> GetMonitoring(DateTimeOffset? dateFrom, DateTimeOffset? dateTo, int offset)
        {
            var Data = GetReportQuery(dateFrom, dateTo, offset);
            return Data;
        }
    }
}
