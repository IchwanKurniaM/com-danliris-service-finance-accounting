using Com.Danliris.Service.Finance.Accounting.Lib.ViewModels.GarmentFinance.Report.BankCashReceiptMonthlyRecap;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.GarmentFinance.Reports.BankCashReceiptMonthlyRecap
{
    public interface IGarmentFinanceBankCashReceiptMonthlyRecapService
    {
        List<GarmentFinanceBankCashReceiptMonthlyRecapViewModel> GetMonitoring(DateTimeOffset? dateFrom, DateTimeOffset? dateTo, int offset);
        Tuple<MemoryStream, string> GenerateExcel(DateTimeOffset? dateFrom, DateTimeOffset? dateTo, int offset);
    }
}
