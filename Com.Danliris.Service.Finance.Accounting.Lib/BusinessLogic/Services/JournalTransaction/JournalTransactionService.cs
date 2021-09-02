using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Interfaces.JournalTransaction;
using Com.Danliris.Service.Finance.Accounting.Lib.Enums.JournalTransaction;
using Com.Danliris.Service.Finance.Accounting.Lib.Helpers;
using Com.Danliris.Service.Finance.Accounting.Lib.Models.JournalTransaction;
using Com.Danliris.Service.Finance.Accounting.Lib.Models.MasterCOA;
using Com.Danliris.Service.Finance.Accounting.Lib.Services.HttpClientService;
using Com.Danliris.Service.Finance.Accounting.Lib.Services.IdentityService;
using Com.Danliris.Service.Finance.Accounting.Lib.Utilities;
using Com.Danliris.Service.Finance.Accounting.Lib.ViewModels.JournalTransaction;
using Com.Moonlay.Models;
using Com.Moonlay.NetCore.Lib;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Services.JournalTransaction
{
    public class JournalTransactionService : IJournalTransactionService
    {
        private const string _UserAgent = "finance-service";
        protected DbSet<JournalTransactionModel> _DbSet;
        protected DbSet<JournalTransactionItemModel> _ItemDbSet;
        protected DbSet<COAModel> _COADbSet;
        private readonly DbSet<JournalTransactionNumber> _JournalTransactionNumberDbSet;
        private readonly IServiceProvider _serviceProvider;
        protected IIdentityService _IdentityService;
        public FinanceDbContext _DbContext;

        public JournalTransactionService(IServiceProvider serviceProvider, FinanceDbContext dbContext)
        {
            _DbContext = dbContext;
            _DbSet = dbContext.Set<JournalTransactionModel>();
            _ItemDbSet = dbContext.Set<JournalTransactionItemModel>();
            _COADbSet = dbContext.Set<COAModel>();
            _JournalTransactionNumberDbSet = dbContext.Set<JournalTransactionNumber>();
            _serviceProvider = serviceProvider;
            _IdentityService = serviceProvider.GetService<IIdentityService>();
            //_IdentityService = serviceProvider.GetService<IIdentityService>();
        }

        public async Task<int> CreateAsync(JournalTransactionModel model)
        {
            int created = 0;
            model.DocumentNo = GenerateDocumentNo(model);
            foreach (var item in model.Items)
            {
                var coa = _COADbSet.FirstOrDefault(f => f.Id.Equals(item.COA.Id) || f.Code.Equals(item.COA.Code));
                if (coa == null)
                {
                    CreateNonExistingCOA(item.COA.Code);
                }
            }

            //if (_DbSet.Any(d => d.ReferenceNo.Equals(model.ReferenceNo) && !d.IsDeleted && !d.IsReversed && !d.IsReverser))
            //{
            //    var errorResult = new List<ValidationResult>()
            //    {
            //        new ValidationResult("No. Referensi duplikat", new List<string> { "ReferenceNo" })
            //    };
            //    ValidationContext validationContext = new ValidationContext(model, _serviceProvider, null);
            //    throw new ServiceValidationException(validationContext, errorResult);
            //}

            if (string.IsNullOrWhiteSpace(model.Status))
                model.Status = JournalTransactionStatus.Draft;

            EntityExtension.FlagForCreate(model, _IdentityService.Username, _UserAgent);


            foreach (var item in model.Items)
            {
                var coa = _COADbSet.FirstOrDefault(f => f.Id.Equals(item.COA.Id) || f.Code.Equals(item.COA.Code));
                //if (coa == null)
                //{
                //    CreateNonExistingCOA(item.COA.Code);
                //    coa = _COADbSet.FirstOrDefault(f => f.Id.Equals(item.COA.Id) || f.Code.Equals(item.COA.Code));
                //}

                item.COAId = coa.Id;
                item.COA = null;

                EntityExtension.FlagForCreate(item, _IdentityService.Username, _UserAgent);
                //_ItemDbSet.Add(item);
            }
            _DbSet.Add(model);
            created += await _DbContext.SaveChangesAsync();
            if (model.Status == JournalTransactionStatus.Posted)
            {
                await UpdateCOABalance(model);
            }

            created += await _DbContext.SaveChangesAsync();
            return created;
        }

        private void CreateNonExistingCOA(string code)
        {
            var splittedCode = code.Split(".");
            if (splittedCode.Count().Equals(4))
            {
                var newCOA = new COAModel()
                {
                    Code = code,
                    Code1 = splittedCode[0],
                    Code2 = splittedCode[1],
                    Code3 = splittedCode[2],
                    Code4 = splittedCode[3]
                };

                EntityExtension.FlagForCreate(newCOA, _IdentityService.Username, _UserAgent);
                _COADbSet.Add(newCOA);
                _DbContext.SaveChanges();
            }
            else
            {
                throw new Exception("{COA: 'Invalid COA Code'}");
            }

        }

        private string GenerateDocumentNo(JournalTransactionModel model)
        {
            var divisionFromCOA = GetDivisionFromCOANumbers(model.Items);
            var division = JournalNumberGenerator.GetDivisionByNumber(int.Parse(divisionFromCOA));
            var numberRule = _JournalTransactionNumberDbSet.FirstOrDefault(f => f.Division == int.Parse(divisionFromCOA));

            if (numberRule == null)
            {
                numberRule = new JournalTransactionNumber()
                {
                    Division = int.Parse(divisionFromCOA),
                    Month = DateTimeOffset.Now.Month,
                    Number = 1,
                    Year = DateTimeOffset.Now.Year
                };

                EntityExtension.FlagForCreate(numberRule, _IdentityService.Username, _UserAgent);
                _JournalTransactionNumberDbSet.Add(numberRule);

                return $"{division}{numberRule.Month.ToString().PadLeft(2, '0')}{numberRule.Year}{numberRule.Number.ToString().PadLeft(4, '0')}";
            }
            else
            {
                if (numberRule.Month != DateTimeOffset.Now.Month)
                {
                    numberRule.Number = 0;

                }

                numberRule.Number += 1;
                numberRule.Month = DateTimeOffset.Now.Month;
                numberRule.Year = DateTimeOffset.Now.Year;

                EntityExtension.FlagForUpdate(numberRule, _IdentityService.Username, _UserAgent);
                _JournalTransactionNumberDbSet.Update(numberRule);
                _DbContext.SaveChanges();

                return $"{division}{numberRule.Month.ToString().PadLeft(2, '0')}{numberRule.Year}{numberRule.Number.ToString().PadLeft(4, '0')}";
            }
        }

        // private COAModel GetCOA()

        private string GetDivisionFromCOANumbers(ICollection<JournalTransactionItemModel> items)
        {
            var result = "0";
            foreach (var item in items)
            {
                if (item.COA == null)
                {
                    item.COA = _COADbSet.FirstOrDefault(f => f.Id.Equals(item.COAId));
                }
                else if (string.IsNullOrWhiteSpace(item.COA.Code))
                {
                    item.COA = _COADbSet.FirstOrDefault(f => f.Id.Equals(item.COA.Id));
                }

                var coaCompositions = item.COA.Code.Split(".");
                //item.COA = null;

                if (coaCompositions.Count() >= 4)
                {
                    if (coaCompositions[2] != "0")
                    {
                        result = coaCompositions[2];
                        break;
                    }
                }

            }

            return result;
        }

        public async Task<int> DeleteAsync(int id)
        {
            var model = await ReadByIdAsync(id);
            EntityExtension.FlagForDelete(model, _IdentityService.Username, _UserAgent);
            foreach (var item in model.Items)
            {
                EntityExtension.FlagForDelete(item, _IdentityService.Username, _UserAgent);
                _ItemDbSet.Update(item);
            }

            _DbSet.Update(model);
            return await _DbContext.SaveChangesAsync();
        }

        public ReadResponse<JournalTransactionModel> Read(int page, int size, string order, List<string> select, string keyword, string filter)
        {
            IQueryable<JournalTransactionModel> Query = _DbSet;

            Query = Query
                .Select(s => new JournalTransactionModel
                {
                    Id = s.Id,
                    CreatedUtc = s.CreatedUtc,
                    CreatedBy = s.CreatedBy,
                    DocumentNo = s.DocumentNo,
                    Date = s.Date,
                    Description = s.Description,
                    ReferenceNo = s.ReferenceNo,
                    LastModifiedUtc = s.LastModifiedUtc,
                    Status = s.Status
                });

            List<string> searchAttributes = new List<string>()
            {
                "DocumentNo", "ReferenceNo", "Description", "Status"
            };

            Query = QueryHelper<JournalTransactionModel>.Search(Query, searchAttributes, keyword);

            Dictionary<string, object> FilterDictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(filter);
            Query = QueryHelper<JournalTransactionModel>.Filter(Query, FilterDictionary);

            Dictionary<string, string> OrderDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(order);
            Query = QueryHelper<JournalTransactionModel>.Order(Query, OrderDictionary);

            Pageable<JournalTransactionModel> pageable = new Pageable<JournalTransactionModel>(Query, page - 1, size);
            List<JournalTransactionModel> Data = pageable.Data.ToList();

            List<JournalTransactionModel> list = new List<JournalTransactionModel>();
            list.AddRange(
               Data.Select(s => new JournalTransactionModel
               {
                   Id = s.Id,
                   CreatedUtc = s.CreatedUtc,
                   CreatedBy = s.CreatedBy,
                   DocumentNo = s.DocumentNo,
                   Date = s.Date,
                   Description = s.Description,
                   ReferenceNo = s.ReferenceNo,
                   LastModifiedUtc = s.LastModifiedUtc,
                   Status = s.Status
               }).ToList()
            );

            int TotalData = pageable.TotalCount;

            return new ReadResponse<JournalTransactionModel>(list, TotalData, OrderDictionary, new List<string>());
        }

        public async Task<JournalTransactionModel> ReadByIdAsync(int id)
        {
            var result = await _DbSet.FirstOrDefaultAsync(d => d.Id.Equals(id) && !d.IsDeleted);
            result.Items = await _ItemDbSet.Where(w => w.JournalTransactionId.Equals(id) && !w.IsDeleted).ToListAsync();
            foreach (var item in result.Items)
            {
                var COA = await _COADbSet.FirstOrDefaultAsync(c => c.Id.Equals(item.COAId) && !c.IsDeleted);
                item.COA = COA;
            }
            return result;
        }

        public ReadResponse<JournalTransactionModel> ReadByDate(DateTimeOffset? dateFrom, DateTimeOffset? dateTo, int offSet, int page, int size, string order, List<string> select, string keyword, string filter)
        {
            IQueryable<JournalTransactionModel> Query = _DbSet;

            Query = Query
                .Select(s => new JournalTransactionModel
                {
                    Id = s.Id,
                    CreatedUtc = s.CreatedUtc,
                    CreatedBy = s.CreatedBy,
                    DocumentNo = s.DocumentNo,
                    Date = s.Date,
                    Description = s.Description,
                    ReferenceNo = s.ReferenceNo,
                    LastModifiedUtc = s.LastModifiedUtc,
                    Status = s.Status
                })
                .Where(x => x.Date >= dateFrom.GetValueOrDefault().AddHours(-1 * offSet))
                .Where(x => x.Date <= dateTo.GetValueOrDefault().AddHours(24 - offSet));

            List<string> searchAttributes = new List<string>()
            {
                "DocumentNo", "ReferenceNo", "Description", "Status"
            };

            Query = QueryHelper<JournalTransactionModel>.Search(Query, searchAttributes, keyword);

            Dictionary<string, object> FilterDictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(filter);
            Query = QueryHelper<JournalTransactionModel>.Filter(Query, FilterDictionary);

            Dictionary<string, string> OrderDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(order);
            Query = QueryHelper<JournalTransactionModel>.Order(Query, OrderDictionary);

            Pageable<JournalTransactionModel> pageable = new Pageable<JournalTransactionModel>(Query, page - 1, size);
            List<JournalTransactionModel> Data = pageable.Data.ToList();

            List<JournalTransactionModel> list = new List<JournalTransactionModel>();
            list.AddRange(
               Data.Select(s => new JournalTransactionModel
               {
                   Id = s.Id,
                   CreatedUtc = s.CreatedUtc,
                   CreatedBy = s.CreatedBy,
                   DocumentNo = s.DocumentNo,
                   Date = s.Date,
                   Description = s.Description,
                   ReferenceNo = s.ReferenceNo,
                   LastModifiedUtc = s.LastModifiedUtc,
                   Status = s.Status
               }).ToList()
            );

            int TotalData = pageable.TotalCount;

            return new ReadResponse<JournalTransactionModel>(list, TotalData, OrderDictionary, new List<string>());
        }

        public List<JournalTransactionModel> ReadUnPostedTransactionsByPeriod(int month, int year, string referenceNo, string referenceType, bool isVB)
        {
            var query = _DbSet.Where(w => w.Date.AddHours(_IdentityService.TimezoneOffset).Month.Equals(month) && w.Date.AddHours(_IdentityService.TimezoneOffset).Year.Equals(year) && w.Status.Equals("DRAFT"));

            if (isVB)
                query = query.Where(entity => entity.Description.Contains("VB"));

            if (!string.IsNullOrWhiteSpace(referenceNo))
                query = query.Where(entity => entity.ReferenceNo.Contains(referenceNo));

            if (!string.IsNullOrWhiteSpace(referenceType))
                query = query.Where(entity => entity.Description.Contains(referenceType));

            var result = query.ToList();
            var transactionIds = result.Select(s => s.Id).ToList();

            var transactionItems = (from transactionItem in _ItemDbSet
                                    join coa in _COADbSet on transactionItem.COAId equals coa.Id
                                    where transactionIds.Contains(transactionItem.JournalTransactionId)
                                    select new JournalTransactionItemModel()
                                    {
                                        Active = transactionItem.Active,
                                        COA = coa,
                                        COAId = coa.Id,
                                        CreatedAgent = transactionItem.CreatedAgent,
                                        CreatedBy = transactionItem.CreatedBy,
                                        CreatedUtc = transactionItem.CreatedUtc,
                                        Credit = transactionItem.Credit,
                                        Debit = transactionItem.Debit,
                                        DeletedAgent = transactionItem.DeletedAgent,
                                        DeletedBy = transactionItem.DeletedBy,
                                        DeletedUtc = transactionItem.DeletedUtc,
                                        Id = transactionItem.Id,
                                        IsDeleted = transactionItem.IsDeleted,
                                        //JournalTransaction = tra
                                        JournalTransactionId = transactionItem.JournalTransactionId,
                                        LastModifiedAgent = transactionItem.LastModifiedAgent,
                                        LastModifiedBy = transactionItem.LastModifiedBy,
                                        LastModifiedUtc = transactionItem.LastModifiedUtc,
                                        Remark = transactionItem.Remark
                                    }).ToList();

            foreach (var transaction in result)
            {
                transaction.Items = transactionItems.Where(w => w.JournalTransactionId.Equals(transaction.Id)).ToList();
            }



            return result.OrderBy(element => element.Date).ToList();
        }

        public async Task<int> UpdateAsync(int id, JournalTransactionModel model)
        {
            EntityExtension.FlagForUpdate(model, _IdentityService.Username, _UserAgent);
            List<int> journalItemIds = await _ItemDbSet.Where(w => w.JournalTransactionId.Equals(id) && !w.IsDeleted).Select(s => s.Id).ToListAsync();

            foreach (var journalItemId in journalItemIds)
            {
                var item = model.Items.FirstOrDefault(f => f.Id.Equals(journalItemId));
                if (item == null)
                {
                    var itemToDelete = await _ItemDbSet.FirstOrDefaultAsync(f => f.Id.Equals(journalItemId));
                    itemToDelete.COA = null;
                    EntityExtension.FlagForDelete(itemToDelete, _IdentityService.Username, _UserAgent);
                    _ItemDbSet.Update(itemToDelete);
                }
                else
                {
                    var coa = _COADbSet.FirstOrDefault(f => f.Id.Equals(item.COA.Id) || f.Code.Equals(item.COA.Code));
                    item.COAId = coa.Id;
                    item.COA = null;
                    EntityExtension.FlagForUpdate(item, _IdentityService.Username, _UserAgent);
                    _ItemDbSet.Update(item);
                }
            }

            foreach (var item in model.Items)
            {
                if (item.Id <= 0)
                {
                    item.COAId = item.COA.Id;
                    item.COA = null;
                    EntityExtension.FlagForCreate(item, _IdentityService.Username, _UserAgent);
                    _ItemDbSet.Add(item);
                }
            }

            _DbSet.Update(model);
            return await _DbContext.SaveChangesAsync();
        }

        private (List<JournalTransactionReportViewModel>, decimal, decimal) GetReport(DateTimeOffset? dateFrom, DateTimeOffset? dateTo, int offSet)
        {
            _DbContext.ChartsOfAccounts.Load();
            IQueryable<JournalTransactionItemModel> query = _DbContext.JournalTransactionItems
                .Include(x => x.JournalTransaction);

            if (dateFrom == null && dateTo == null)
            {
                query = query
                    .Where(x => DateTimeOffset.UtcNow.AddDays(-30).Date <= x.JournalTransaction.Date.AddHours(offSet).Date
                        && x.JournalTransaction.Date.AddHours(offSet).Date <= DateTime.UtcNow.Date);
            }
            else if (dateFrom == null && dateTo != null)
            {
                query = query
                    .Where(x => dateTo.Value.AddDays(-30).Date <= x.JournalTransaction.Date.AddHours(offSet).Date
                        && x.JournalTransaction.Date.AddHours(offSet).Date <= dateTo.Value.Date);
            }
            else if (dateTo == null && dateFrom != null)
            {
                query = query
                    .Where(x => dateFrom.Value.Date <= x.JournalTransaction.Date.AddHours(offSet).Date
                        && x.JournalTransaction.Date.AddHours(offSet).Date <= dateFrom.Value.AddDays(30).Date);
            }
            else
            {
                query = query
                    .Where(x => dateFrom.Value.Date <= x.JournalTransaction.Date.AddHours(offSet).Date
                        && x.JournalTransaction.Date.AddHours(offSet).Date <= dateTo.Value.Date);
            }

            List<JournalTransactionReportViewModel> result = new List<JournalTransactionReportViewModel>();
            foreach (var item in query.OrderBy(x => x.JournalTransaction.Date).ToList())
            {
                JournalTransactionReportViewModel vm = new JournalTransactionReportViewModel
                {
                    Credit = item.Credit,
                    Date = item.JournalTransaction.Date,
                    Debit = item.Debit,
                    Remark = item.Remark,
                    Description = item.JournalTransaction.Description,
                    ReferenceNo = item.JournalTransaction.ReferenceNo,
                    IsReverser = item.JournalTransaction.IsReverser,
                    IsReversed = item.JournalTransaction.IsReversed,
                    HeaderRemark = item.JournalTransaction.Remark
                };

                if (item.COA != null)
                {
                    vm.COACode = item.COA.Code;
                    vm.COAName = item.COA.Name;
                }
                result.Add(vm);

            }

            return (result, result.Sum(x => x.Debit.GetValueOrDefault()), result.Sum(x => x.Credit.GetValueOrDefault()));
        }

        public (ReadResponse<JournalTransactionReportHeaderViewModel>, decimal, decimal) GetReport(int page, int size, DateTimeOffset? dateFrom, DateTimeOffset? dateTo, int offSet)
        {
            var queries = GetReport(dateFrom, dateTo, offSet);

            List<JournalTransactionReportHeaderViewModel> result = new List<JournalTransactionReportHeaderViewModel>();

            foreach (var item in queries.Item1.GroupBy(x => new { x.Date, x.ReferenceNo, x.Description, x.Remark }))
            {
                result.Add(new JournalTransactionReportHeaderViewModel()
                {
                    Description = item.Key.Description,
                    ReferenceNo = item.Key.ReferenceNo,
                    HeaderRemark = item.Key.Remark,
                    Items = item.ToList()
                });
            }

            Pageable<JournalTransactionReportViewModel> pageable = new Pageable<JournalTransactionReportViewModel>(queries.Item1, page - 1, size);
            List<JournalTransactionReportViewModel> data = pageable.Data.ToList();

            return (new ReadResponse<JournalTransactionReportHeaderViewModel>(result, queries.Item1.Count, new Dictionary<string, string>(), new List<string>()), queries.Item2, queries.Item3);
        }

        public MemoryStream GenerateExcel(DateTimeOffset? dateFrom, DateTimeOffset? dateTo, int offSet)
        {
            var data = GetReport(dateFrom, dateTo, offSet);

            DataTable dt = new DataTable();

            dt.Columns.Add(new DataColumn() { ColumnName = "", DataType = typeof(string) });
            dt.Columns.Add(new DataColumn() { ColumnName = "Deskripsi", DataType = typeof(string) });
            dt.Columns.Add(new DataColumn() { ColumnName = "No Referensi", DataType = typeof(string) });
            dt.Columns.Add(new DataColumn() { ColumnName = "Remark", DataType = typeof(string) });
            dt.Columns.Add(new DataColumn() { ColumnName = "Date", DataType = typeof(string) });
            dt.Columns.Add(new DataColumn() { ColumnName = "Nama Akun", DataType = typeof(string) });
            dt.Columns.Add(new DataColumn() { ColumnName = "No Akun", DataType = typeof(string) });
            dt.Columns.Add(new DataColumn() { ColumnName = "Keterangan", DataType = typeof(string) });
            dt.Columns.Add(new DataColumn() { ColumnName = "Debit", DataType = typeof(string) });
            dt.Columns.Add(new DataColumn() { ColumnName = "Kredit", DataType = typeof(string) });

            if (data.Item1.Count == 0)
            {
                dt.Rows.Add("", "", "", "", "", "", "", "", "", "");
            }
            else
            {
                foreach (var item in data.Item1)
                {
                    dt.Rows.Add(item.IsReverser ? "Pembalik" : item.IsReversed ? "Dibalik" : "", string.IsNullOrEmpty(item.Description) ? "-" : item.Description, string.IsNullOrEmpty(item.ReferenceNo) ? "-" : item.ReferenceNo, string.IsNullOrEmpty(item.HeaderRemark) ? "-" : item.HeaderRemark, item.Date.AddHours(offSet).ToString("dd MMM yyyy"), string.IsNullOrEmpty(item.COAName) ? "-" : item.COAName, string.IsNullOrEmpty(item.COACode) ? "-" : item.COACode,
                        string.IsNullOrEmpty(item.Remark) ? "-" : item.Remark, item.Debit.HasValue ? item.Debit.Value.ToString("#,##0.###0") : "0", item.Credit.HasValue ? item.Credit.Value.ToString("#,##0.###0") : "0");

                }
                dt.Rows.Add("", "", "", "", "", "", "", "TOTAL", data.Item2.ToString("#,##0.###0"), data.Item3.ToString("#,##0.###0"));
            }

            return Excel.CreateExcelJournalTransaction(new List<KeyValuePair<DataTable, string>>() { new KeyValuePair<DataTable, string>(dt, "Jurnal Transaksi") }, dateFrom.GetValueOrDefault(), dateTo.GetValueOrDefault(), true);
        }

        public async Task<int> ReverseJournalTransactionByReferenceNo(string referenceNo)
        {
            var transactionsToReverse = _DbSet.Where(entity => entity.ReferenceNo.Equals(referenceNo) && !entity.IsReversed && !entity.IsReverser && !entity.IsDeleted).ToList();

            if (!transactionsToReverse.Any())
            {
                return await _DbContext.SaveChangesAsync();
            }

            foreach (var transactionToReverse in transactionsToReverse)
            {
                transactionToReverse.IsReversed = true;
                _DbSet.Update(transactionToReverse);

                var transactionToReverseItems = _ItemDbSet.Where(entity => entity.JournalTransactionId.Equals(transactionToReverse.Id)).ToList();
                var reversingItems = new List<JournalTransactionItemModel>();
                foreach (var transactionToReverseItem in transactionToReverseItems)
                {
                    var reversingItem = new JournalTransactionItemModel()
                    {
                        COAId = transactionToReverseItem.COAId,
                        Credit = transactionToReverseItem.Debit,
                        Debit = transactionToReverseItem.Credit,
                        Remark = transactionToReverseItem.Remark
                    };
                    EntityExtension.FlagForCreate(reversingItem, _IdentityService.Username, _UserAgent);
                    reversingItems.Add(reversingItem);
                }

                var reversingJournalTransaction = new JournalTransactionModel()
                {
                    Date = transactionToReverse.Date,
                    Items = reversingItems,
                    ReferenceNo = transactionToReverse.ReferenceNo,
                    Status = transactionToReverse.Status,
                    Description = $"Jurnal Pembalik {transactionToReverse.DocumentNo}"
                };

                await UpdateCOABalance(reversingJournalTransaction);

                reversingJournalTransaction.DocumentNo = GenerateDocumentNo(reversingJournalTransaction);
                foreach (var item in reversingJournalTransaction.Items)
                {
                    item.COA = null;
                }
                //do
                //{
                //    reversingJournalTransaction.DocumentNo = CodeGenerator.Generate();
                //}
                //while (_DbSet.Any(d => d.DocumentNo.Equals(reversingJournalTransaction.DocumentNo)));
                reversingJournalTransaction.IsReverser = true;
                EntityExtension.FlagForCreate(reversingJournalTransaction, _IdentityService.Username, _UserAgent);
                _DbSet.Add(reversingJournalTransaction);
                await _DbContext.SaveChangesAsync();
            }

            return transactionsToReverse.Count;
        }

        public async Task<SubLedgerReportViewModel> GetSubLedgerReport(int? coaId, int month, int year, int timeoffset)
        {
            var dataResult = await GetSubLedgerReportData(coaId, month, year, timeoffset);

            // ui purpose only
            var result = new SubLedgerReportViewModel()
            {
                ClosingBalance = dataResult.ClosingBalance,
                InitialBalance = dataResult.InitialBalance,
                Others = new List<SubLedgerReport>()
            };

            result.Others.AddRange(dataResult.GarmentImports);
            result.Others.AddRange(dataResult.GarmentLokals);
            result.Others.AddRange(dataResult.Others);
            result.Others.AddRange(dataResult.TextileImports);
            result.Others.AddRange(dataResult.TextileLokals);

            result.Others = result.Others.OrderBy(order => order.SortingDate).ToList();

            return result;
        }

        private async Task<SubLedgerReportViewModel> GetSubLedgerReportData(int? coaId, int? month, int? year, int timeoffset)
        {

            List<string> textiles = new List<string>() { "1", "2", "3" };
            List<string> garments = new List<string>() { "4" };
            var bankPayments = await GetBankPayments(month.GetValueOrDefault(), year.GetValueOrDefault(), timeoffset);
            if (coaId.HasValue)
            {
                var postedJournals = _DbSet.Where(w => (!string.IsNullOrWhiteSpace(w.Status) && w.Status.Equals(JournalTransactionStatus.Posted)) && w.Date.AddHours(timeoffset).Month.Equals(month.GetValueOrDefault()) && w.Date.AddHours(timeoffset).Year.Equals(year.GetValueOrDefault())).Select(s => new { s.Id, s.Date, s.ReferenceNo, s.DocumentNo }).ToList();
                var postedIds = postedJournals.Select(s => s.Id).ToList();
                var postedReferenceNos = postedJournals.Select(s => s.ReferenceNo).ToList();

                var entries = _ItemDbSet.Include(x => x.COA).Where(w => postedIds.Contains(w.JournalTransactionId) && w.COAId.Equals(coaId.GetValueOrDefault())).ToList();
                var closingDebitBalance = entries.Sum(s => s.Debit);
                var closingCreditBalance = entries.Sum(s => s.Credit);

                var initialDate = new DateTime(year.GetValueOrDefault(), month.GetValueOrDefault(), 1);
                var previousPostedJournalIds = _DbSet.Where(w => (!string.IsNullOrWhiteSpace(w.Status) && w.Status.Equals(JournalTransactionStatus.Posted)) && w.Date < initialDate).Select(s => s.Id).ToList();
                var previousDebitBalance = _ItemDbSet.Where(w => previousPostedJournalIds.Contains(w.JournalTransactionId) && w.COAId.Equals(coaId.GetValueOrDefault())).Sum(s => s.Debit);
                var previousCreditBalance = _ItemDbSet.Where(w => previousPostedJournalIds.Contains(w.JournalTransactionId) && w.COAId.Equals(coaId.GetValueOrDefault())).Sum(s => s.Credit);

                var result = new SubLedgerReportViewModel
                {
                    InitialBalance = (decimal)previousDebitBalance - (decimal)previousCreditBalance,
                    ClosingBalance = ((decimal)previousDebitBalance - (decimal)previousCreditBalance) + ((decimal)closingDebitBalance - (decimal)closingCreditBalance)
                };

                var unitReceiptNotes = await GetUnitReceiptNote(postedReferenceNos);
                foreach (var entry in entries)
                {
                    var header = postedJournals.FirstOrDefault(f => f.Id.Equals(entry.JournalTransactionId));
                    var bankPayment = bankPayments.FirstOrDefault(f => f.DocumentNo.Equals(header?.ReferenceNo));
                    var unitReceiptNote = unitReceiptNotes.FirstOrDefault(f => f.URNNo == header.ReferenceNo);
                    var data = new SubLedgerReport()
                    {
                        BankName = bankPayment?.BankName,
                        BGCheck = bankPayment?.BGCheckNumber,
                        Credit = (decimal)entry.Credit,
                        Debit = (decimal)entry.Debit,
                        //Date = unitReceiptNote != null && unitReceiptNote.URNDate.HasValue ? unitReceiptNote.URNDate.Value.AddHours(timeoffset).ToString("dd MMMM yyyy", CultureInfo.InvariantCulture)
                        //        : header.Date.AddHours(timeoffset).ToString("dd MMMM yyyy", CultureInfo.InvariantCulture),
                        No = header.DocumentNo,
                        Date = header.Date.AddHours(timeoffset).ToString("dd MMMM yyyy", CultureInfo.InvariantCulture),
                        Remark = entry.Remark,
                        COACode = entry.COA?.Code,
                        COAName = entry.COA?.Name,
                        URNNo = header.ReferenceNo,
                        Supplier = unitReceiptNote?.Supplier,
                        UPONo = unitReceiptNote?.UPONo,
                        JournalId = entry.JournalTransactionId,
                        JournalItemId = entry.Id,
                        SortingDate = header.Date
                    };

                    if (!string.IsNullOrEmpty(data.COACode) && !string.IsNullOrEmpty(data.URNNo)
                                                        && textiles.Contains(data.COACode.Split('.')[2]) && data.URNNo.Split('-')[2].LastOrDefault() == 'L')
                        result.TextileLokals.Add(data);
                    else if (!string.IsNullOrEmpty(data.COACode) && !string.IsNullOrEmpty(data.URNNo)
                                                        && textiles.Contains(data.COACode.Split('.')[2]) && data.URNNo.Split('-')[2].LastOrDefault() == 'I')
                        result.TextileImports.Add(data);
                    else if (!string.IsNullOrEmpty(data.COACode) && !string.IsNullOrEmpty(data.URNNo)
                                                        && garments.Contains(data.COACode.Split('.')[2]) && data.URNNo.Split('-')[2].LastOrDefault() == 'L')
                        result.GarmentLokals.Add(data);
                    else if (!string.IsNullOrEmpty(data.COACode) && !string.IsNullOrEmpty(data.URNNo)
                                                        && garments.Contains(data.COACode.Split('.')[2]) && data.URNNo.Split('-')[2].LastOrDefault() == 'I')
                        result.GarmentImports.Add(data);
                    else
                        result.Others.Add(data);
                }


                return result;
            }
            else
            {
                var postedJournals = _DbSet.Where(w => (!string.IsNullOrWhiteSpace(w.Status) && w.Status.Equals(JournalTransactionStatus.Posted)) && w.Date.AddHours(timeoffset).Month.Equals(month.GetValueOrDefault()) && w.Date.AddHours(timeoffset).Year.Equals(year.GetValueOrDefault())).Select(s => new { s.Id, s.Date, s.ReferenceNo, s.DocumentNo }).ToList();
                var postedIds = postedJournals.Select(s => s.Id).ToList();
                var postedReferenceNos = postedJournals.Select(s => s.ReferenceNo).ToList();

                var entries = _ItemDbSet.Include(x => x.COA).Where(w => postedIds.Contains(w.JournalTransactionId)).ToList();
                var closingDebitBalance = entries.Sum(s => s.Debit);
                var closingCreditBalance = entries.Sum(s => s.Credit);

                var initialDate = new DateTime(year.GetValueOrDefault(), month.GetValueOrDefault(), 1);
                var previousPostedJournalIds = _DbSet.Where(w => (!string.IsNullOrWhiteSpace(w.Status) && w.Status.Equals(JournalTransactionStatus.Posted)) && w.Date < initialDate).Select(s => s.Id).ToList();
                var previousDebitBalance = _ItemDbSet.Where(w => previousPostedJournalIds.Contains(w.JournalTransactionId)).Sum(s => s.Debit);
                var previousCreditBalance = _ItemDbSet.Where(w => previousPostedJournalIds.Contains(w.JournalTransactionId)).Sum(s => s.Credit);

                var result = new SubLedgerReportViewModel
                {
                    InitialBalance = (decimal)previousDebitBalance - (decimal)previousCreditBalance,
                    ClosingBalance = ((decimal)previousDebitBalance - (decimal)previousCreditBalance) + ((decimal)closingDebitBalance - (decimal)closingCreditBalance)
                };

                //foreach (var entry in entries)
                //{
                //    var header = postedJournals.FirstOrDefault(f => f.Id.Equals(entry.JournalTransactionId));
                //    var bankPayment = bankPayments.FirstOrDefault(f => f.DocumentNo.Equals(header?.ReferenceNo));
                //    var data = new SubLedgerReport()
                //    {
                //        BankName = bankPayment?.BankName,
                //        BGCheck = bankPayment?.BGCheckNumber,
                //        Credit = (decimal)entry.Credit,
                //        Debit = (decimal)entry.Debit,
                //        Date = header.Date.AddHours(timeoffset).ToString("dd MMMM yyyy", CultureInfo.InvariantCulture),
                //        No = header.DocumentNo,
                //        Remark = entry.Remark
                //    };

                //    result.Info.Add(data);
                //}
                var unitReceiptNotes = await GetUnitReceiptNote(postedReferenceNos);
                foreach (var entry in entries)
                {
                    var header = postedJournals.FirstOrDefault(f => f.Id.Equals(entry.JournalTransactionId));
                    //var bankPayment = bankPayments.FirstOrDefault(f => f.DocumentNo.Equals(header?.ReferenceNo));
                    var unitReceiptNote = unitReceiptNotes.FirstOrDefault(f => f.URNNo == header.ReferenceNo);
                    var data = new SubLedgerReport()
                    {
                        //BankName = bankPayment?.BankName,
                        //BGCheck = bankPayment?.BGCheckNumber,
                        Credit = (decimal)entry.Credit,
                        Debit = (decimal)entry.Debit,
                        //Date = unitReceiptNote != null && unitReceiptNote.URNDate.HasValue ? unitReceiptNote.URNDate.Value.AddHours(timeoffset).ToString("dd MMMM yyyy", CultureInfo.InvariantCulture)
                        //        : header.Date.AddHours(timeoffset).ToString("dd MMMM yyyy", CultureInfo.InvariantCulture),
                        //No = header.DocumentNo,
                        Date = header.Date.AddHours(timeoffset).ToString("dd MMMM yyyy", CultureInfo.InvariantCulture),
                        Remark = entry.Remark,
                        COACode = entry.COA?.Code,
                        COAName = entry.COA?.Name,
                        URNNo = header.ReferenceNo,
                        Supplier = unitReceiptNote?.Supplier,
                        UPONo = unitReceiptNote?.UPONo,
                        JournalId = entry.JournalTransactionId,
                        JournalItemId = entry.Id
                    };

                    if (!string.IsNullOrEmpty(data.COACode) && !string.IsNullOrEmpty(data.URNNo)
                                                        && textiles.Contains(data.COACode.Split('.')[2]) && data.URNNo.Split('-')[2].LastOrDefault() == 'L')
                        result.TextileLokals.Add(data);
                    else if (!string.IsNullOrEmpty(data.COACode) && !string.IsNullOrEmpty(data.URNNo)
                                                        && textiles.Contains(data.COACode.Split('.')[2]) && data.URNNo.Split('-')[2].LastOrDefault() == 'I')
                        result.TextileImports.Add(data);
                    else if (!string.IsNullOrEmpty(data.COACode) && !string.IsNullOrEmpty(data.URNNo)
                                                        && garments.Contains(data.COACode.Split('.')[2]) && data.URNNo.Split('-')[2].LastOrDefault() == 'L')
                        result.GarmentLokals.Add(data);
                    else if (!string.IsNullOrEmpty(data.COACode) && !string.IsNullOrEmpty(data.URNNo)
                                                        && garments.Contains(data.COACode.Split('.')[2]) && data.URNNo.Split('-')[2].LastOrDefault() == 'I')
                        result.GarmentImports.Add(data);
                    else
                        result.Others.Add(data);
                }
                return result;
            }


        }

        private async Task<List<BankPayment>> GetBankPayments(int month, int year, int timeoffset)
        {
            var uri = APIEndpoint.Purchasing + $"bank-expenditure-notes/get-documentno/by-period?month={month}&year={year}&timeoffset={timeoffset}";

            IHttpClientService _httpClientService = (IHttpClientService)_serviceProvider.GetService(typeof(IHttpClientService));
            var response = await _httpClientService.GetAsync(uri);

            var result = new BankPaymentResult();
            if (response.IsSuccessStatusCode)
            {
                result = JsonConvert.DeserializeObject<BankPaymentResult>(await response.Content.ReadAsStringAsync());
            }
            return result.data.ToList();
        }

        private async Task<List<UnitReceiptNote>> GetUnitReceiptNote(List<string> urnNo)
        {
            var uri = APIEndpoint.Purchasing + $"unit-receipt-notes/all/subledger";
            string noJson = JsonConvert.SerializeObject(urnNo);
            IHttpClientService _httpClientService = (IHttpClientService)_serviceProvider.GetService(typeof(IHttpClientService));
            var response = await _httpClientService.PostAsync(uri, new StringContent(noJson, Encoding.UTF8, General.JsonMediaType));

            var result = new UnitReceiptNoteResult();
            if (response.IsSuccessStatusCode)
            {
                result = JsonConvert.DeserializeObject<UnitReceiptNoteResult>(await response.Content.ReadAsStringAsync());
            }
            return result.data.ToList();
        }

        public async Task<SubLedgerXlsFormat> GetSubLedgerReportXls(int? coaId, int? month, int? year, int timeoffset)
        {
            var result = new SubLedgerXlsFormat();
            SubLedgerReportViewModel data = new SubLedgerReportViewModel();
            if (!coaId.HasValue)
            {
                data = await GetSubLedgerReportData(coaId, month, year, timeoffset);

                if (month.HasValue && year.HasValue)
                {
                    var monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(month.GetValueOrDefault());
                    result.Filename = $"Laporan Sub Ledger Periode {monthName} {year.GetValueOrDefault()}";
                }
                else
                {
                    result.Filename = $"Laporan Sub Ledger";
                }

            }
            else
            {
                var coa = _DbContext.ChartsOfAccounts.FirstOrDefault(f => f.Id.Equals(coaId.GetValueOrDefault()));

                data = await GetSubLedgerReportData(coaId, month, year, timeoffset);
                var monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(month.GetValueOrDefault());
                result.Filename = $"Laporan Sub Ledger {coa.Name} Periode {monthName} {year.GetValueOrDefault()}";
            }
            List<string> textiles = new List<string>() { "1", "2", "3" };
            List<string> garments = new List<string>() { "4" };
            DataTable dt = new DataTable();
            dt.Columns.Add(new DataColumn() { ColumnName = "No.", DataType = typeof(string) });
            dt.Columns.Add(new DataColumn() { ColumnName = "Tgl. BP", DataType = typeof(string) });
            dt.Columns.Add(new DataColumn() { ColumnName = "Keterangan", DataType = typeof(string) });
            dt.Columns.Add(new DataColumn() { ColumnName = "Supplier", DataType = typeof(string) });
            dt.Columns.Add(new DataColumn() { ColumnName = "No. BP.", DataType = typeof(string) });
            dt.Columns.Add(new DataColumn() { ColumnName = "No. NI/SPB", DataType = typeof(string) });
            dt.Columns.Add(new DataColumn() { ColumnName = "Keterangan Rek.", DataType = typeof(string) });
            dt.Columns.Add(new DataColumn() { ColumnName = "No. Rek", DataType = typeof(string) });
            dt.Columns.Add(new DataColumn() { ColumnName = "Debit", DataType = typeof(decimal) });
            dt.Columns.Add(new DataColumn() { ColumnName = "Kredit", DataType = typeof(decimal) });
            int index = 1;
            if (data.TextileImports.Count == 0 && data.TextileLokals.Count == 0 && data.GarmentImports.Count == 0 &&
                data.GarmentLokals.Count == 0 && data.Others.Count == 0)
            {
                dt.Rows.Add(0, "", "", "", "", "", "", "", 0, 0);
            }
            else
            {
                dt.Rows.Add();
                dt.Rows.Add("Pembelian Textile Lokal");

                foreach (var item in data.TextileLokals.OrderBy(x => x.Date).ThenBy(x => x.JournalId).ThenBy(x => x.JournalItemId))
                {
                    var date = DateTime.Parse(item.Date);
                    dt.Rows.Add(index++, date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), item.Remark, item.Supplier, item.URNNo, item.UPONo,
                        item.COAName, item.COACode, item.Debit, item.Credit);
                }

                dt.Rows.Add();
                dt.Rows.Add("Pembelian Textile Import");

                foreach (var item in data.TextileImports.OrderBy(x => x.Date).ThenBy(x => x.JournalId).ThenBy(x => x.JournalItemId))
                {
                    var date = DateTime.Parse(item.Date);
                    dt.Rows.Add(index++, date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), item.Remark, item.Supplier, item.URNNo, item.UPONo,
                        item.COAName, item.COACode, item.Debit, item.Credit);
                }

                dt.Rows.Add();
                dt.Rows.Add("Pembelian Garment Lokal");

                foreach (var item in data.GarmentLokals.OrderBy(x => x.Date).ThenBy(x => x.JournalId).ThenBy(x => x.JournalItemId))
                {
                    var date = DateTime.Parse(item.Date);
                    dt.Rows.Add(index++, date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), item.Remark, item.Supplier, item.URNNo, item.UPONo,
                        item.COAName, item.COACode, item.Debit, item.Credit);
                }

                dt.Rows.Add();
                dt.Rows.Add("Pembelian Garment Import");

                foreach (var item in data.GarmentImports.OrderBy(x => x.Date).ThenBy(x => x.JournalId).ThenBy(x => x.JournalItemId))
                {
                    var date = DateTime.Parse(item.Date);
                    dt.Rows.Add(index++, date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), item.Remark, item.Supplier, item.URNNo, item.UPONo,
                        item.COAName, item.COACode, item.Debit, item.Credit);
                }

                dt.Rows.Add();
                dt.Rows.Add("Lain - Lain");

                foreach (var item in data.Others.OrderBy(x => x.Date).ThenBy(x => x.JournalId).ThenBy(x => x.JournalItemId))
                {
                    var date = DateTime.Parse(item.Date);
                    dt.Rows.Add(index++, date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), item.Remark, item.Supplier, item.URNNo, item.UPONo,
                        item.COAName, item.COACode, item.Debit, item.Credit);
                }


                //foreach (var item in data.Info)
                //{
                //    var date = DateTime.Parse(item.Date);
                //    dt.Rows.Add(index++, date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), item.Remark, item.Supplier, item.URNNo, item.UPONo,
                //        item.COAName, item.COACode, item.Debit, item.Credit);
                //}
            }


            result.Result = Excel.CreateExcelNoFilters(new List<KeyValuePair<DataTable, string>>() { new KeyValuePair<DataTable, string>(dt, "Sub Ledger") }, true);

            return result;
        }

        public async Task<int> PostTransactionAsync(int id)
        {
            var model = _DbSet.Include(x => x.Items).FirstOrDefault(f => f.Id.Equals(id));

            var debit = model.Items.Where(item => item.Debit > 0).Sum(item => item.Debit);
            var credit = model.Items.Where(item => item.Credit > 0).Sum(item => item.Credit);
            if (debit != credit)
            {
                var errorResult = new List<ValidationResult>()
                {
                    new ValidationResult("Total Debit dan Kredit harus sama", new List<string> { "Differences" })
                };
                ValidationContext validationContext = new ValidationContext(model, _serviceProvider, null);
                throw new ServiceValidationException(validationContext, errorResult);
            }

            if (model.Status != JournalTransactionStatus.Posted)
            {
                await UpdateCOABalance(model);
            }

            model.Status = JournalTransactionStatus.Posted;
            EntityExtension.FlagForUpdate(model, _IdentityService.Username, _UserAgent);
            _DbSet.Update(model);
            return await _DbContext.SaveChangesAsync();
        }

        public async Task<int> CreateManyAsync(List<JournalTransactionModel> models)
        {
            var result = 0;
            foreach (var model in models)
            {
                result += await CreateAsync(model);
            }
            return result;
        }

        public async Task<int> PostTransactionAsync(int id, JournalTransactionModel model)
        {
            if (model.Status != JournalTransactionStatus.Posted)
            {
                await UpdateCOABalance(model);
            }
            model.Status = "POSTED";

            foreach (var item in model.Items)
            {
                item.COAId = item.COA.Id;
                item.COA = null;
                EntityExtension.FlagForUpdate(item, _IdentityService.Username, _UserAgent);
                _ItemDbSet.Update(item);
            }

            EntityExtension.FlagForUpdate(model, _IdentityService.Username, _UserAgent);
            _DbSet.Update(model);
            return await _DbContext.SaveChangesAsync();
        }

        public async Task<List<GeneralLedgerWrapperReportViewModel>> GetGeneralLedgerReport(DateTimeOffset startDate, DateTimeOffset endDate, int timezoneoffset)
        {
            var query = GetGeneralLedgerReportQueryByPeriod(startDate, endDate, timezoneoffset);
            var coaIds = await query.Select(s => s.COAId).Distinct().ToListAsync();
            var coaCodes1 = _COADbSet.Where(w => coaIds.Contains(w.Id)).Select(s => s.Code1).Distinct().ToList();
            var coaCodes2 = _COADbSet.Where(w => coaIds.Contains(w.Id)).Select(s => s.Code2).Distinct().ToList();
            var coaHeaderCodes = _COADbSet.Where(w => coaCodes1.Contains(w.Code1) && coaCodes2.Contains(w.Code2) && w.Code3.Equals("0") && w.Code4.Equals("00")).OrderBy(s => s.Code).Select(s => new { s.Code, s.Code1, s.Code2, s.Name }).Distinct().ToList();

            var journalquery = query
                .Join(
                _DbSet,
                item => item.JournalTransactionId,
                header => header.Id,
                (item, header) => new
                {
                    Credit = Math.Round(item.Credit, 4),
                    header.Date,
                    Debit = Math.Round(item.Debit, 4),
                    item.COAId
                }).AsQueryable();

            var journalResult = await journalquery.Join(_COADbSet, item => item.COAId, coa => coa.Id, (item, coa) => new
            {
                item.Credit,
                item.Date,
                item.Debit,
                Description = coa.Name,
                COACode = coa.Code,
                COACode1 = coa.Code1,
                COACode2 = coa.Code2
            }).ToListAsync();

            var result = new List<GeneralLedgerWrapperReportViewModel>();

            foreach (var coaHeaderCode in coaHeaderCodes)
            {
                var filteredCOAIds = _COADbSet.Where(w => w.Code1.Equals(coaHeaderCode.Code1) && w.Code2.Equals(coaHeaderCode.Code2)).Select(s => s.Id).ToList();
                var initialBalance = _ItemDbSet.Join(_DbSet, item => item.JournalTransactionId, header => header.Id, (item, header) => new { item.Debit, item.COAId, item.Credit, header.Date, header.Status }).Where(w => w.Date < startDate && filteredCOAIds.Contains(w.COAId) && w.Status == "POSTED").Sum(sum => sum.Debit - sum.Credit);

                var filteredJournalResult = journalResult.Where(w => coaHeaderCode.Code1.Equals(w.COACode1) && coaHeaderCode.Code2.Equals(w.COACode2)).Select(s => new GeneralLedgerReportViewModel()
                {
                    COACode = s.COACode,
                    Credit = s.Credit,
                    Date = s.Date,
                    Debit = s.Debit,
                    Description = s.Description
                }).ToList();

                result.Add(new GeneralLedgerWrapperReportViewModel()
                {
                    COACode = coaHeaderCode.Code,
                    COAName = coaHeaderCode.Name,
                    Items = GetGeneralLedgerItems(filteredJournalResult.OrderBy(o => o.Date).ToList(), Math.Round(initialBalance, 4)),
                    Summary = Math.Round(initialBalance, 4) + filteredJournalResult.Sum(s => s.Debit) - filteredJournalResult.Sum(s => s.Credit),
                    InitialBalance = Math.Round(initialBalance, 4)
                });
            }

            return result.Where(w => w.Items.Count > 0).ToList();
        }

        private List<GeneralLedgerReportViewModel> GetGeneralLedgerItems(List<GeneralLedgerReportViewModel> list, decimal initialBalance)
        {
            var result = new List<GeneralLedgerReportViewModel>();

            var remainingBalance = initialBalance;
            foreach (var item in list)
            {
                remainingBalance += item.Debit - item.Credit;
                result.Add(new GeneralLedgerReportViewModel()
                {
                    COACode = item.COACode,
                    Credit = item.Credit,
                    Date = item.Date,
                    Debit = item.Debit,
                    Description = item.Description,
                    RemainingBalance = Math.Round(remainingBalance, 4)
                });
            }

            return result;
        }

        private IQueryable<JournalTransactionItemModel> GetGeneralLedgerReportQueryByPeriod(DateTimeOffset startDate, DateTimeOffset endDate, int timezoneoffset)
        {
            var journalTransactionIds = _DbSet.Where(journalDocument => journalDocument.Date.AddHours(timezoneoffset) >= startDate && journalDocument.Date.AddHours(timezoneoffset) <= endDate && journalDocument.Status == "POSTED").Select(s => s.Id).ToList();
            return _ItemDbSet.Where(item => journalTransactionIds.Contains(item.JournalTransactionId));
        }

        public async Task<MemoryStream> GetGeneralLedgerReportXls(DateTimeOffset startDate, DateTimeOffset endDate, int timezoneoffset)
        {
            var queryResult = await GetGeneralLedgerReport(startDate, endDate, timezoneoffset);

            DataTable dt = new DataTable();
            dt.Columns.Add(new DataColumn() { ColumnName = "Tanggal", DataType = typeof(string) });
            dt.Columns.Add(new DataColumn() { ColumnName = "Jurnal", DataType = typeof(string) });
            dt.Columns.Add(new DataColumn() { ColumnName = "Nomor Akun", DataType = typeof(string) });
            dt.Columns.Add(new DataColumn() { ColumnName = "Debit", DataType = typeof(double) });
            dt.Columns.Add(new DataColumn() { ColumnName = "Kredit", DataType = typeof(double) });
            dt.Columns.Add(new DataColumn() { ColumnName = "Sisa Saldo", DataType = typeof(double) });

            if (queryResult.Count == 0)
            {
                dt.Rows.Add("", "", "", 0, 0, 0);
            }
            else
                foreach (var header in queryResult)
                {
                    dt.Rows.Add("", "Saldo Awal", "", 0, 0, header.InitialBalance);

                    foreach (var item in header.Items)
                    {
                        dt.Rows.Add(item.Date.AddHours(timezoneoffset).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), item.Description, item.COACode, item.Debit, item.Credit, item.RemainingBalance);
                    }

                    dt.Rows.Add("", "Saldo Akhir", "", 0, 0, header.Summary);
                    dt.Rows.Add();
                }

            return Excel.CreateExcelNoFilters(new List<KeyValuePair<DataTable, string>>() { new KeyValuePair<DataTable, string>(dt, "General Ledgers") }, true);
        }

        private async Task UpdateCOABalance(JournalTransactionModel model)
        {


            foreach (var item in model.Items)
            {
                List<COAModel> leafCoas = new List<COAModel>();
                List<COAModel> firstParentCoas = new List<COAModel>();
                List<COAModel> secondParentCoas = new List<COAModel>();
                List<COAModel> thirdParentCoas = new List<COAModel>();

                var saldo = item.Debit - item.Credit;

                COAModel leafCoa = await _COADbSet.FirstOrDefaultAsync(x => x.Id == item.COAId);

                if (leafCoa != null)
                {
                    leafCoa.Balance += saldo;
                    leafCoas.Add(leafCoa);
                }


                COAModel existedCoaCode3 = await _COADbSet.FirstOrDefaultAsync(x => x.Code1 == leafCoa.Code1 && x.Code2 == leafCoa.Code2 && x.Code3 == leafCoa.Code3 && x.Code4 == "00");

                if (existedCoaCode3 != null && !leafCoas.Any(x => x.Id == existedCoaCode3.Id)
                    && !secondParentCoas.Any(x => x.Id == existedCoaCode3.Id) && !thirdParentCoas.Any(x => x.Id == existedCoaCode3.Id))
                {
                    existedCoaCode3.Balance += saldo;
                    firstParentCoas.Add(existedCoaCode3);
                }


                COAModel existedCoaCode2 = await _COADbSet.FirstOrDefaultAsync(x => x.Code1 == leafCoa.Code1 && x.Code2 == leafCoa.Code2 && x.Code3 == "0" && x.Code4 == "00");

                if (existedCoaCode2 != null && !leafCoas.Any(x => x.Id == existedCoaCode2.Id)
                    && !firstParentCoas.Any(x => x.Id == existedCoaCode2.Id) && !thirdParentCoas.Any(x => x.Id == existedCoaCode2.Id))
                {
                    existedCoaCode2.Balance += saldo;
                    secondParentCoas.Add(existedCoaCode2);
                }



                COAModel existedCoaCode1 = await _COADbSet.FirstOrDefaultAsync(x => x.Code1 == leafCoa.Code1 && x.Code2 == "00" && x.Code3 == "0" && x.Code4 == "00");

                if (existedCoaCode1 != null && !leafCoas.Any(x => x.Id == existedCoaCode1.Id)
                    && !firstParentCoas.Any(x => x.Id == existedCoaCode1.Id) && !secondParentCoas.Any(x => x.Id == existedCoaCode1.Id))
                {
                    existedCoaCode1.Balance += saldo;
                    thirdParentCoas.Add(existedCoaCode1);
                }



            }

        }

        public List<string> GetAllReferenceNo(string keyword, bool isVB)
        {
            var query = _DbContext.JournalTransactions.AsQueryable();
            if (isVB)
                query = query.Where(entity => entity.Description.Contains("VB"));

            return query.Select(entity => entity.ReferenceNo).Distinct().Where(entity => !string.IsNullOrWhiteSpace(entity) && entity.Contains(keyword)).ToList();
        }

        public List<string> GetAllReferenceType(string keyword, bool isVB)
        {
            var query = _DbContext.JournalTransactions.AsQueryable();
            if (isVB)
                query = query.Where(entity => entity.Description.Contains("VB"));

            return query.Select(entity => entity.Description).Distinct().Where(entity => !string.IsNullOrWhiteSpace(entity) && entity.Contains(keyword)).ToList();
        }
    }

    public class BankPaymentResult
    {
        public BankPaymentResult()
        {
            data = new List<BankPayment>();
        }
        public IList<BankPayment> data { get; set; }
    }

    public class BankPayment
    {
        public string DocumentNo { get; set; }
        public string BankName { get; set; }
        public string BGCheckNumber { get; set; }
    }

    public class UnitReceiptNoteResult
    {
        public UnitReceiptNoteResult()
        {
            data = new List<UnitReceiptNote>();
        }
        public IList<UnitReceiptNote> data { get; set; }
    }

    public class UnitReceiptNote
    {
        public DateTimeOffset? URNDate { get; set; }
        public string URNNo { get; set; }
        public string Supplier { get; set; }
        public string UPONo { get; set; }
    }
}
