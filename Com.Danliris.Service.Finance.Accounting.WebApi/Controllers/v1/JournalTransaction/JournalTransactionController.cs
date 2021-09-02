using AutoMapper;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Interfaces.JournalTransaction;
using Com.Danliris.Service.Finance.Accounting.Lib.Models.JournalTransaction;
using Com.Danliris.Service.Finance.Accounting.Lib.Services.IdentityService;
using Com.Danliris.Service.Finance.Accounting.Lib.Services.ValidateService;
using Com.Danliris.Service.Finance.Accounting.Lib.Utilities;
using Com.Danliris.Service.Finance.Accounting.Lib.ViewModels.JournalTransaction;
using Com.Danliris.Service.Finance.Accounting.WebApi.Utilities;
using Com.Danliris.Service.Production.WebApi.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace Com.Danliris.Service.Finance.Accounting.WebApi.Controllers.v1.JournalTransaction
{
    [Produces("application/json")]
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/journal-transactions")]
    [Authorize]
    public class JournalTransactionController : BaseController<JournalTransactionModel, JournalTransactionViewModel, IJournalTransactionService>
    {
        public JournalTransactionController(IIdentityService identityService, IValidateService validateService, IJournalTransactionService service, IMapper mapper) : base(identityService, validateService, service, mapper, "1.0.0")
        {
        }

        [HttpGet("transaction")]
        public IActionResult GetTransaction([FromQuery] DateTimeOffset? datefrom = null, [FromQuery] DateTimeOffset? dateto = null, int page = 1, int size = 25, string order = "{}", [Bind(Prefix = "Select[]")] List<string> select = null, string keyword = null, string filter = "{}")
        {
            try
            {
                VerifyUser();
                int offSet = Convert.ToInt32(Request.Headers["x-timezone-offset"]);
                //int offSet = 7;

                ReadResponse<JournalTransactionModel> read = Service.ReadByDate(datefrom, dateto, offSet, page, size, order, select, keyword, filter);

                Dictionary<string, object> Result =
                    new ResultFormatter(ApiVersion, General.OK_STATUS_CODE, General.OK_MESSAGE)
                    .Ok(null, read.Data, page, size, read.Count, read.Data.Count, read.Order, read.Selected);
                return Ok(Result);
            }
            catch (Exception e)
            {
                Dictionary<string, object> Result =
                   new ResultFormatter(ApiVersion, General.INTERNAL_ERROR_STATUS_CODE, e.Message)
                   .Fail();
                return StatusCode(General.INTERNAL_ERROR_STATUS_CODE, Result);
            }
        }

        [HttpPost("many")]
        public async Task<ActionResult> PostMany([FromBody] List<JournalTransactionViewModel> viewModels)
        {
            try
            {
                VerifyUser();
                //foreach (var viewModel in viewModels)
                //{

                //    ValidateService.Validate(viewModel);
                //}

                var models = Mapper.Map<List<JournalTransactionModel>>(viewModels);
                await Service.CreateManyAsync(models);

                Dictionary<string, object> Result =
                    new ResultFormatter(ApiVersion, General.CREATED_STATUS_CODE, General.OK_MESSAGE)
                    .Ok();
                return Created(string.Concat(Request.Path, "/", 0), Result);
            }
            catch (Exception e)
            {
                Dictionary<string, object> Result =
                    new ResultFormatter(ApiVersion, General.INTERNAL_ERROR_STATUS_CODE, e.Message)
                    .Fail();
                return StatusCode(General.INTERNAL_ERROR_STATUS_CODE, Result);
            }
        }

        [HttpGet("report")]
        public IActionResult GetReport([FromQuery] DateTimeOffset? dateFrom = null, [FromQuery] DateTimeOffset? dateTo = null, int page = 1, int size = 25)
        {
            try
            {
                VerifyUser();
                int offSet = Convert.ToInt32(Request.Headers["x-timezone-offset"]);
                //int offSet = 7;
                var data = Service.GetReport(page, size, dateFrom, dateTo, offSet);

                return Ok(new
                {
                    apiVersion = ApiVersion,
                    data = data.Item1.Data,
                    info = new
                    {
                        data.Item1.Count,
                        data.Item1.Order,
                        data.Item1.Selected,
                        TotalDebit = data.Item2,
                        TotalCredit = data.Item3
                    },
                    message = General.OK_MESSAGE,
                    statusCode = General.OK_STATUS_CODE
                });
            }
            catch (Exception e)
            {
                Dictionary<string, object> Result =
                   new ResultFormatter(ApiVersion, General.INTERNAL_ERROR_STATUS_CODE, e.Message)
                   .Fail();
                return StatusCode(General.INTERNAL_ERROR_STATUS_CODE, Result);
            }
        }

        [HttpGet("report/downloads/xls")]
        public IActionResult GetXls([FromQuery] DateTimeOffset? dateFrom = null, [FromQuery] DateTimeOffset? dateTo = null)
        {
            try
            {
                VerifyUser();
                byte[] xlsInBytes;
                int offSet = Convert.ToInt32(Request.Headers["x-timezone-offset"]);
                var xls = Service.GenerateExcel(dateFrom, dateTo, offSet);

                string fileName = "";
                if (dateFrom == null && dateTo == null)
                    fileName = string.Format("Jurnal Transaksi");
                else if (dateFrom != null && dateTo == null)
                    fileName = string.Format("Jurnal Transaksi {0}", dateFrom.Value.ToString("dd/MM/yyyy"));
                else if (dateFrom == null && dateTo != null)
                    fileName = string.Format("Jurnal Transaksi {0}", dateTo.GetValueOrDefault().ToString("dd/MM/yyyy"));
                else
                    fileName = string.Format("Jurnal Transaksi {0} - {1}", dateFrom.GetValueOrDefault().ToString("dd/MM/yyyy"), dateTo.Value.ToString("dd/MM/yyyy"));

                xlsInBytes = xls.ToArray();

                var file = File(xlsInBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                return file;
            }
            catch (Exception e)
            {
                Dictionary<string, object> Result =
                  new ResultFormatter(ApiVersion, General.INTERNAL_ERROR_STATUS_CODE, e.Message)
                  .Fail();
                return StatusCode(General.INTERNAL_ERROR_STATUS_CODE, Result);
            }
        }

        [HttpPost("reverse-transactions/{referenceNo}")]
        public async Task<ActionResult> PostReverseJournalTransaction([FromRoute] string referenceNo)
        {
            try
            {
                VerifyUser();

                await Service.ReverseJournalTransactionByReferenceNo(referenceNo);

                Dictionary<string, object> Result =
                    new ResultFormatter(ApiVersion, General.CREATED_STATUS_CODE, General.OK_MESSAGE)
                    .Ok();
                return Created(String.Concat(Request.Path, "/", 0), Result);
            }
            catch (Exception e)
            {
                Dictionary<string, object> Result =
                    new ResultFormatter(ApiVersion, General.INTERNAL_ERROR_STATUS_CODE, e.Message)
                    .Fail();
                return StatusCode(General.INTERNAL_ERROR_STATUS_CODE, Result);
            }
        }

        [HttpGet("report/sub-ledgers")]
        public async Task<ActionResult> GetSubLedgerReport([FromQuery] int? coaId, [FromQuery] int month, [FromQuery] int year)
        {
            try
            {
                VerifyUser();

                int offSet = Convert.ToInt32(Request.Headers["x-timezone-offset"]);
                var reportData = await Service.GetSubLedgerReport(coaId, month, year, offSet);

                Dictionary<string, object> Result =
                    new ResultFormatter(ApiVersion, General.OK_STATUS_CODE, General.OK_MESSAGE)
                    .Ok(Mapper, reportData);
                return Ok(Result);
            }
            catch (Exception e)
            {
                Dictionary<string, object> Result =
                    new ResultFormatter(ApiVersion, General.INTERNAL_ERROR_STATUS_CODE, e.Message)
                    .Fail();
                return StatusCode(General.INTERNAL_ERROR_STATUS_CODE, Result);
            }
        }

        [HttpGet("report/sub-ledgers/download/xls")]
        public async Task<ActionResult> GetSubLedgerReportXls([FromQuery] int? coaId, [FromQuery] int? month, [FromQuery] int? year)
        {
            try
            {
                VerifyUser();

                byte[] xlsInBytes;
                int offSet = Convert.ToInt32(Request.Headers["x-timezone-offset"]);
                var reportData = await Service.GetSubLedgerReportXls(coaId, month, year, offSet);

                string fileName = reportData.Filename;

                xlsInBytes = reportData.Result.ToArray();

                var file = File(xlsInBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                return file;
            }
            catch (Exception e)
            {
                Dictionary<string, object> Result =
                    new ResultFormatter(ApiVersion, General.INTERNAL_ERROR_STATUS_CODE, e.Message)
                    .Fail();
                return StatusCode(General.INTERNAL_ERROR_STATUS_CODE, Result);
            }
        }

        [HttpPut("posting-transaction/{id}")]
        public async Task<ActionResult> PostingTransationById([FromRoute] int id)
        {
            try
            {
                VerifyUser();

                await Service.PostTransactionAsync(id);

                return NoContent();
            }
            catch (Exception e)
            {
                Dictionary<string, object> Result =
                    new ResultFormatter(ApiVersion, General.INTERNAL_ERROR_STATUS_CODE, e.Message)
                    .Fail();
                return StatusCode(General.INTERNAL_ERROR_STATUS_CODE, Result);
            }
        }

        [HttpPut("posting-transaction-update-coa/{id}")]
        public async Task<ActionResult> PostingTransationById([FromRoute] int id, [FromBody] JournalTransactionViewModel viewModel)
        {
            try
            {
                VerifyUser();

                var model = Mapper.Map<JournalTransactionModel>(viewModel);

                await Service.PostTransactionAsync(id, model);

                return NoContent();
            }
            catch (Exception e)
            {
                Dictionary<string, object> Result =
                    new ResultFormatter(ApiVersion, General.INTERNAL_ERROR_STATUS_CODE, e.Message)
                    .Fail();
                return StatusCode(General.INTERNAL_ERROR_STATUS_CODE, Result);
            }
        }

        [HttpGet("report/sub-ledgers/options/months")]
        public IActionResult GetMonthOptions()
        {
            var monthOptions = new List<object>()
            {
                new { MonthNumber = 1, MonthName = "Januari" },
                new { MonthNumber = 2, MonthName = "Februari" },
                new { MonthNumber = 3, MonthName = "Maret" },
                new { MonthNumber = 4, MonthName = "April" },
                new { MonthNumber = 5, MonthName = "Mei" },
                new { MonthNumber = 6, MonthName = "Juni" },
                new { MonthNumber = 7, MonthName = "Juli" },
                new { MonthNumber = 8, MonthName = "Agustus" },
                new { MonthNumber = 9, MonthName = "September" },
                new { MonthNumber = 10, MonthName = "Oktober" },
                new { MonthNumber = 11, MonthName = "November" },
                new { MonthNumber = 12, MonthName = "Desember" }
            };
            Dictionary<string, object> Result =
                new ResultFormatter(ApiVersion, General.OK_STATUS_CODE, General.OK_MESSAGE)
                .Ok(Mapper, monthOptions);
            return Ok(Result);
        }

        [HttpGet("unposted-transactions")]
        public IActionResult GetUnPosted([FromQuery] string referenceNo, [FromQuery] string referenceType, [FromQuery] int month = 0, [FromQuery] int year = 0, [FromQuery] bool isVB = false)
        {
            try
            {
                VerifyUser();

                if (month.Equals(0))
                    month = DateTime.Now.Month;
                if (year.Equals(0))
                    year = DateTime.Now.Year;

                List<JournalTransactionModel> result = Service.ReadUnPostedTransactionsByPeriod(month, year, referenceNo, referenceType, isVB);

                List<JournalTransactionViewModel> dataVM = Mapper.Map<List<JournalTransactionViewModel>>(result);

                Dictionary<string, object> Result =
                    new ResultFormatter(ApiVersion, General.OK_STATUS_CODE, General.OK_MESSAGE)
                    .Ok(Mapper, dataVM);
                return Ok(Result);
            }
            catch (Exception e)
            {
                Dictionary<string, object> Result =
                    new ResultFormatter(ApiVersion, General.INTERNAL_ERROR_STATUS_CODE, e.Message)
                    .Fail();
                return StatusCode(General.INTERNAL_ERROR_STATUS_CODE, Result);
            }
        }

        [HttpGet("reference-no")]
        public IActionResult GetReferenceNo([FromQuery] string keyword, [FromQuery] bool isVB = false)
        {
            try
            {

                var result = Service.GetAllReferenceNo(keyword, isVB);

                Dictionary<string, object> Result =
                    new ResultFormatter(ApiVersion, General.OK_STATUS_CODE, General.OK_MESSAGE)
                    .Ok(Mapper, result);
                return Ok(Result);
            }
            catch (Exception e)
            {
                Dictionary<string, object> Result =
                    new ResultFormatter(ApiVersion, General.INTERNAL_ERROR_STATUS_CODE, e.Message)
                    .Fail();
                return StatusCode(General.INTERNAL_ERROR_STATUS_CODE, Result);
            }
        }

        [HttpGet("reference-type")]
        public IActionResult GetReferenceType([FromQuery] string keyword, [FromQuery] bool isVB = false)
        {
            try
            {

                var result = Service.GetAllReferenceType(keyword, isVB);

                Dictionary<string, object> Result =
                    new ResultFormatter(ApiVersion, General.OK_STATUS_CODE, General.OK_MESSAGE)
                    .Ok(Mapper, result);
                return Ok(Result);
            }
            catch (Exception e)
            {
                Dictionary<string, object> Result =
                    new ResultFormatter(ApiVersion, General.INTERNAL_ERROR_STATUS_CODE, e.Message)
                    .Fail();
                return StatusCode(General.INTERNAL_ERROR_STATUS_CODE, Result);
            }
        }

        [HttpGet("general-ledgers")]
        public async Task<IActionResult> GetGeneralLedger([FromQuery] DateTimeOffset? startDate, [FromQuery] DateTimeOffset? endDate)
        {
            try
            {
                VerifyUser();
                int timeoffset = Convert.ToInt32(Request.Headers["x-timezone-offset"]);

                var result = await Service.GetGeneralLedgerReport(startDate.GetValueOrDefault(), endDate.GetValueOrDefault(), timeoffset);

                return Ok(new
                {
                    apiVersion = "1.0.0",
                    statusCode = 200,
                    data = result
                });
            }
            catch (Exception e)
            {
                Dictionary<string, object> Result =
                    new ResultFormatter(ApiVersion, General.INTERNAL_ERROR_STATUS_CODE, e.Message)
                    .Fail();
                return StatusCode(General.INTERNAL_ERROR_STATUS_CODE, Result);
            }
        }

        [HttpGet("general-ledgers/download/xls")]
        public async Task<ActionResult> GetGeneralLedgerXls([FromQuery] DateTimeOffset? startDate, [FromQuery] DateTimeOffset? endDate)
        {
            try
            {
                VerifyUser();

                byte[] xlsInBytes;
                int offSet = Convert.ToInt32(Request.Headers["x-timezone-offset"]);
                var filestream = await Service.GetGeneralLedgerReportXls(startDate.GetValueOrDefault(), endDate.GetValueOrDefault(), offSet);

                string fileName = $"Laporan General Ledger Periode {startDate.GetValueOrDefault().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)} - {endDate.GetValueOrDefault().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}.xlsx";

                xlsInBytes = filestream.ToArray();

                var file = File(xlsInBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                return file;
            }
            catch (Exception e)
            {
                Dictionary<string, object> Result =
                    new ResultFormatter(ApiVersion, General.INTERNAL_ERROR_STATUS_CODE, e.Message)
                    .Fail();
                return StatusCode(General.INTERNAL_ERROR_STATUS_CODE, Result);
            }
        }
    }
}
