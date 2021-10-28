using AutoMapper;
using Com.Danliris.Service.Finance.Accounting.Lib;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.BudgetCashflow;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.DPPVATBankExpenditureNote;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.GarmentPurchasingExpedition;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.GarmentPurchasingExpedition.Reports;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Interfaces.CashierApproval;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Interfaces.ClearaceVB;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Interfaces.CreditBalance;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Interfaces.CreditorAccount;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Interfaces.DailyBankTransaction;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Interfaces.GarmentInvoicePayment;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Interfaces.JournalTransaction;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Interfaces.LockTransaction;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Interfaces.Master;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Interfaces.PaymentDispositionNote;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Interfaces.PaymentDispositionNotVerifiedReport;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Interfaces.PurchasingDispositionExpedition;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Interfaces.SalesReceipt;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Interfaces.VBExpeditionRealizationReport;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Interfaces.VBRealizationDocumentNonPO;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Interfaces.VBRequestAll;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Interfaces.VBStatusReport;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Services.CashierApproval;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Services.ClearaceVB;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Services.CreditBalance;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Services.CreditorAccount;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Services.DailyBankTransaction;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Services.DownPayment;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Services.GarmentInvoicePayment;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Services.JournalTransaction;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Services.LockTransaction;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Services.Master;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Services.Memo;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Services.PaymentDispositionNote;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Services.PaymentDispositionNotVerifiedReport;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Services.PurchasingDispositionExpedition;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Services.RealizationVBNonPO;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Services.SalesReceipt;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Services.VBExpeditionRealizationReport;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Services.VbNonPORequest;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Services.VBRealizationDocumentNonPO;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Services.VBRequestAll;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Services.VBStatusReport;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Services.VBVerification;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.VBRealizationDocument;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.VBRealizationDocumentExpedition;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.VBRequestDocument;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.VbWIthPORequest;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.GarmentPurchasingPphBankExpenditureNote;
using Com.Danliris.Service.Finance.Accounting.Lib.Services.CacheService;
using Com.Danliris.Service.Finance.Accounting.Lib.Services.HttpClientService;
using Com.Danliris.Service.Finance.Accounting.Lib.Services.IdentityService;
using Com.Danliris.Service.Finance.Accounting.Lib.Services.OthersExpenditureProofDocument;
using Com.Danliris.Service.Finance.Accounting.Lib.Services.ValidateService;
using Com.Danliris.Service.Finance.Accounting.Lib.Utilities;
using Com.Danliris.Service.Finance.Accounting.WebApi.Controllers.v1.RealizationVBNonPO;
using Com.Danliris.Service.Finance.Accounting.WebApi.Utilities;
using IdentityServer4.AccessTokenValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Serialization;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.GarmentDebtBalance;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.GarmentDispositionExpedition;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.GarmentInvoicePurchasingDisposition;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.GarmentDispositionPaymentReport;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Services.AccountingBook;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Interfaces.MemoGarmentPurchasing;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Services.MemoGarmentPurchasing;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Services.MemoDetailGarmentPurchasing;
using Microsoft.ApplicationInsights.AspNetCore;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Interfaces.MemoGarmentPurchasingReport;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Services.MemoGarmentPurchasingReport;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.GarmentDownPaymentReport;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Interfaces.GarmentFinance.BankCashReceipt;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Services.GarmentFinance.BankCashReceipt;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.PurchasingMemoDetailTextile;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Reports.ExportSalesDebtorReport;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Reports;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Interfaces.GarmentFinance.BankCashReceiptDetail;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Services.GarmentFinance.BankCashReceiptDetail;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.PurchasingMemoTextile;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.GarmentFinance.Memorial;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.GarmentFinance.MemorialDetail;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Interfaces.GarmentFinance.BankCashReceiptDetailLocal;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Services.GarmentFinance.BankCashReceiptDetailLocal;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Interfaces.GarmentFinance.MemorialDetailLocal;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.Services.GarmentFinance.MemorialDetailLocal;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.GarmentFinance.Reports.DebtorCard;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.GarmentFinance.Reports.BankCashReceiptMonthlyRecap;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.GarmentFinance.Reports.ExportSalesOutstanding;

namespace Com.Danliris.Service.Finance.Accounting.WebApi
{
    public class Startup
    {
        private readonly string[] EXPOSED_HEADERS = new string[] { "Content-Disposition", "api-version", "content-length", "content-md5", "content-type", "date", "request-id", "response-time" };
        private readonly string FINANCE_POLICY = "FinancePolicy";

        public IConfiguration Configuration { get; }
        public bool HasAppInsight => !string.IsNullOrEmpty(Configuration.GetValue<string>("APPINSIGHTS_INSTRUMENTATIONKEY") ?? Configuration.GetValue<string>("ApplicationInsights:InstrumentationKey"));
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void RegisterEndpoint()
        {
            APIEndpoint.Core = Configuration.GetValue<string>("CoreEndpoint") ?? Configuration["CoreEndpoint"];
            APIEndpoint.Inventory = Configuration.GetValue<string>("InventoryEndpoint") ?? Configuration["InventoryEndpoint"];
            APIEndpoint.Purchasing = Configuration.GetValue<string>("PurchasingEndpoint") ?? Configuration["PurchasingEndpoint"];
            APIEndpoint.Finishing = Configuration.GetValue<string>("FinishingEndpoint") ?? Configuration["FinishingEndpoint"];
            APIEndpoint.Finance = Configuration.GetValue<string>("FinanceEndpoint") ?? Configuration["FinanceEndpoint"];
            APIEndpoint.Sales = Configuration.GetValue<string>("SalesEndpoint") ?? Configuration["SalesEndpoint"];
            APIEndpoint.PackingInventory = Configuration.GetValue<string>("PackingInventoryEndpoint") ?? Configuration["PackingInventoryEndpoint"];
        }

        private void RegisterServices(IServiceCollection services)
        {
            services
                .AddScoped<IdentityService>()
                .AddScoped<ValidateService>()
                .AddScoped<IHttpClientService, HttpClientService>()
                .AddScoped<IIdentityService, IdentityService>()
                .AddScoped<IValidateService, ValidateService>();

        }

        private void RegisterBusinessServices(IServiceCollection services)
        {
            services
                .AddTransient<ICOAService, COAService>()
                .AddTransient<ICreditorAccountService, CreditorAccountService>()
                .AddTransient<IJournalTransactionService, JournalTransactionService>()
                .AddTransient<ILockTransactionService, LockTransactionService>()
                .AddTransient<ICreditBalanceService, CreditBalanceService>()
                .AddTransient<IDailyBankTransactionService, DailyBankTransactionService>()
                .AddTransient<IPurchasingDispositionExpeditionService, PurchasingDispositionExpeditionService>()
                .AddTransient<IPaymentDispositionNoteService, PaymentDispositionNoteService>()
                .AddTransient<IPaymentDispositionNotVerifiedReport, PaymentDispositionNotVerifiedReportService>()
                .AddTransient<IOthersExpenditureProofDocumentService, OthersExpenditureProofDocumentService>()
                .AddTransient<IAutoJournalService, AutoJournalService>()
                .AddTransient<IMasterCOAService, MasterCOAService>()
                .AddTransient<IAutoDailyBankTransactionService, AutoDailyBankTransactionService>()
                .AddTransient<IMemoService, MemoService>()
                .AddTransient<ISalesReceiptService, SalesReceiptService>()
                .AddTransient<ICashierAprovalService, CashierApprovalService>()
                .AddTransient<IVBStatusReportService, VBStatusReportService>()
                .AddTransient<IVBExpeditionRealizationReportService, VBExpeditionRealizationReportService>()
                .AddTransient<IClearaceVBService, ClearaceVBService>()
                .AddTransient<IVBRequestAllService, VBRequestAllService>()
                .AddTransient<IDownPaymentService, DownPaymentService>()
                .AddTransient<IVbNonPORequestService, VbNonPORequestService>()
                .AddTransient<IVbWithPORequestService, VbWithPORequestService>()
                .AddTransient<IRealizationVbWithPOService, RealizationVbWithPOService>()
                .AddTransient<IRealizationVbNonPOService, RealizationVbNonPOService>()
                .AddTransient<IVbVerificationService, VbVerificationService>()
                .AddTransient<IVBRequestDocumentService, VBRequestDocumentService>()
                .AddTransient<IGarmentInvoicePaymentService, GarmentInvoicePaymentService>()
                .AddTransient<IGarmentPurchasingExpeditionService, GarmentPurchasingExpeditionService>()
                .AddTransient<IGarmentDispositionExpeditionService, GarmentDispositionExpeditionService>()
                .AddTransient<IGarmentPurchasingExpeditionReportService, GarmentPurchasingExpeditionReportService>()
                .AddTransient<IVBRealizationDocumentNonPOService, VBRealizationDocumentNonPOService>()
                .AddTransient<IVBRealizationWithPOService, VBRealizationWithPOService>()
                .AddTransient<IVBRealizationService, VBRealizationService>()
                .AddTransient<IVBRealizationDocumentExpeditionService, VBRealizationDocumentExpeditionService>()
                .AddTransient<IBudgetCashflowService, BudgetCashflowService>()
                .AddTransient<IDPPVATBankExpenditureNoteService, DPPVATBankExpenditureNoteService>()
                .AddTransient<IGarmentPurchasingPphBankExpenditureNoteService, GarmentPurchasingPphBankExpenditureNoteService>()
                .AddTransient<IGarmentDebtBalanceService, GarmentDebtBalanceService>()
                .AddTransient<IGarmentDispositionPaymentReportService, GarmentDispositionPaymentReportService>()
                .AddTransient<IGarmentInvoicePurchasingDispositionService, GarmentInvoicePurchasingDispositionService>()
                .AddTransient<IAccountingBookService, AccountingBookService>()
                .AddTransient<IMemoGarmentPurchasingService, MemoGarmentPurchasingService>()
                .AddTransient<IMemoGarmentPurchasingReportService, MemoGarmentPurchasingReportService>()
                .AddTransient<IMemoDetailGarmentPurchasingService, MemoDetailGarmentPurchasingService>()
                .AddTransient<IGarmentDownPaymentReportService, GarmentDownPaymentReportService>()
                .AddTransient<IBankCashReceiptService, BankCashReceiptService>()
                .AddTransient<IPurchasingMemoDetailTextileService, PurchasingMemoDetailTextileService>()
                .AddTransient<IGarmentDownPaymentReportService, GarmentDownPaymentReportService>()
                .AddTransient<IExportSalesDebtorReportService, ExportSalesDebtorReportService>()            
                .AddTransient<IBankCashReceiptDetailService, BankCashReceiptDetailService>()
                .AddTransient<IPurchasingMemoTextileService, PurchasingMemoTextileService>()
                .AddTransient<IGarmentDownPaymentReportService, GarmentDownPaymentReportService>()
                .AddTransient<IGarmentFinanceMemorialService, GarmentFinanceMemorialService>()
                .AddTransient<IGarmentFinanceMemorialDetailService, GarmentFinanceMemorialDetailService>()
                .AddTransient<IGarmentFinanceDebtorCardReportService, GarmentFinanceDebtorCardReportService>()
                .AddTransient<IGarmentFinanceBankCashReceiptMonthlyRecapService, GarmentFinanceBankCashReceiptMonthlyRecapService>()
                .AddTransient<IGarmentFinanceBankCashReceiptDetailLocalService, GarmentFinanceBankCashReceiptDetailLocalService>()
                .AddTransient<IGarmentFinanceMemorialDetailLocalService, GarmentFinanceMemorialDetailLocalService>()
                .AddTransient<IGarmentFinanceDebtorCardReportService, GarmentFinanceDebtorCardReportService>()
                .AddTransient<IGarmentFinanceExportSalesOutstandingReportService, GarmentFinanceExportSalesOutstandingReportService>();    

        }


        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            string connectionString = Configuration.GetConnectionString(Constant.DEFAULT_CONNECTION) ?? Configuration[Constant.DEFAULT_CONNECTION];
            string env = Configuration.GetValue<string>(Constant.ASPNETCORE_ENVIRONMENT);
            services.AddDbContext<FinanceDbContext>(options => options.UseSqlServer(connectionString, c => c.CommandTimeout(60)));

            #region Register
            RegisterServices(services);

            RegisterBusinessServices(services);

            RegisterEndpoint();

            services.AddAutoMapper();

            #endregion

            #region Authentication
            string Secret = Configuration.GetValue<string>(Constant.SECRET) ?? Configuration[Constant.SECRET];
            SymmetricSecurityKey Key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(Secret));

            services.AddAuthentication(IdentityServerAuthenticationDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateAudience = false,
                        ValidateIssuer = false,
                        ValidateLifetime = false,
                        IssuerSigningKey = Key
                    };
                });
            #endregion

            #region CORS

            services.AddCors(options => options.AddPolicy(FINANCE_POLICY, builder =>
            {
                builder.AllowAnyOrigin()
                       .AllowAnyMethod()
                       .AllowAnyHeader()
                       .WithExposedHeaders(EXPOSED_HEADERS);
            }));

            #endregion

            services.AddDistributedRedisCache(options =>
            {
                options.Configuration = Configuration.GetValue<string>("RedisConnection") ?? Configuration["RedisConnection"];
                options.InstanceName = Configuration.GetValue<string>("RedisConnectionName") ?? Configuration["RedisConnectionName"];
            });
            services.AddSingleton<ICacheService, CacheService>();

            #region API

            services
                .AddApiVersioning(options => options.DefaultApiVersion = new ApiVersion(1, 0))
                .AddMvcCore()
                .AddApiExplorer()
                .AddAuthorization()
                .AddJsonFormatters()
                .AddJsonOptions(options => options.SerializerSettings.ContractResolver = new DefaultContractResolver());

            #endregion

            #region Swagger
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Info() { Title = "My API", Version = "v1" });
                c.AddSecurityDefinition("Bearer", new ApiKeyScheme()
                {
                    In = "header",
                    Description = "Please enter into field the word 'Bearer' following by space and JWT",
                    Name = "Authorization",
                    Type = "apiKey",
                });
                c.AddSecurityRequirement(new Dictionary<string, IEnumerable<string>>()
                {
                    {
                        "Bearer",
                        Enumerable.Empty<string>()
                    }
                });
                c.OperationFilter<ResponseHeaderFilter>();
                c.CustomSchemaIds(i => i.FullName);
            });
            services.AddApplicationInsightsTelemetry();
            #endregion

            // App Insight
            if (HasAppInsight)
            {
                services.AddApplicationInsightsTelemetry();
                services.AddAppInsightRequestBodyLogging();
            }
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            using (var serviceScope = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>().CreateScope())
            {
                var context = serviceScope.ServiceProvider.GetService<FinanceDbContext>();
                context.Database.Migrate();
            }

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "API V1");
            });
            app.UseAuthentication();
            app.UseCors(FINANCE_POLICY);
            app.UseMvc();

            if (HasAppInsight)
            {
                app.UseAppInsightRequestBodyLogging();
                app.UseAppInsightResponseBodyLogging();
            }
        }
    }

    public class ResponseHeaderFilter : IOperationFilter
    {
        public void Apply(Operation operation, OperationFilterContext context)
        {
            // Get all response header declarations for a given operation
            if (operation.Parameters == null)
                operation.Parameters = new List<IParameter>();

            operation.Parameters.Add(new NonBodyParameter
            {
                Name = "x-timezone-offset",
                In = "header",
                Type = "string",
                Required = true
            });
        }
    }
}
