using AutoMapper;
using Com.Danliris.Service.Finance.Accounting.Lib.BusinessLogic.GarmentFinance.Reports.ExportSalesOutstanding;
using Com.Danliris.Service.Finance.Accounting.Lib.Services.IdentityService;
using Com.Danliris.Service.Finance.Accounting.Lib.Services.ValidateService;
using Com.Danliris.Service.Finance.Accounting.Lib.Utilities;
using Com.Danliris.Service.Finance.Accounting.Lib.ViewModels.GarmentFinance.Report.ExportSalesOutstanding;
using Com.Danliris.Service.Finance.Accounting.WebApi.Controllers.v1.GarmentFinance.Report.ExportOutstandingSalesReport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Net;
using System.Security.Claims;
using System.Text;
using Xunit;

namespace Com.Danliris.Service.Finance.Accounting.Test.Controllers.GarmentFinance.Report.ExportSalesOutStanding
{
    public class GarmentFinanceExportSalesOutstandingReportControllerTest
    {
        protected GarmentFinanceExportOutstandingSalesReportController GetController((Mock<IIdentityService> IdentityService, Mock<IValidateService> ValidateService, Mock<IGarmentFinanceExportSalesOutstandingReportService> Service, Mock<IMapper> Mapper) mocks)
        {
            var user = new Mock<ClaimsPrincipal>();
            var claims = new Claim[]
            {
                new Claim("username", "unittestusername")
            };
            user.Setup(u => u.Claims).Returns(claims);

            GarmentFinanceExportOutstandingSalesReportController controller = new GarmentFinanceExportOutstandingSalesReportController(mocks.IdentityService.Object, mocks.ValidateService.Object, mocks.Service.Object, mocks.Mapper.Object);
            controller.ControllerContext = new ControllerContext()
            {
                HttpContext = new DefaultHttpContext()
                {
                    User = user.Object
                }
            };
            controller.ControllerContext.HttpContext.Request.Headers["Authorization"] = "Bearer unittesttoken";
            controller.ControllerContext.HttpContext.Request.Path = new PathString("/v1/unit-test");
            return controller;
        }

        public (Mock<IIdentityService> IdentityService, Mock<IValidateService> ValidateService, Mock<IGarmentFinanceExportSalesOutstandingReportService> Service, Mock<IMapper> Mapper) GetMocks()
        {
            return (IdentityService: new Mock<IIdentityService>(), ValidateService: new Mock<IValidateService>(), Service: new Mock<IGarmentFinanceExportSalesOutstandingReportService>(), Mapper: new Mock<IMapper>());
        }

        Mock<IServiceProvider> GetServiceProvider()
        {
            Mock<IServiceProvider> serviceProvider = new Mock<IServiceProvider>();
            serviceProvider
              .Setup(s => s.GetService(typeof(IIdentityService)))
              .Returns(new IdentityService() { TimezoneOffset = 1, Token = "token", Username = "username" });

            var validateService = new Mock<IValidateService>();
            serviceProvider
              .Setup(s => s.GetService(typeof(IValidateService)))
              .Returns(validateService.Object);
            return serviceProvider;
        }

        protected int GetStatusCode(IActionResult response)
        {
            return (int)response.GetType().GetProperty("StatusCode").GetValue(response, null);
        }


        protected ServiceValidationException GetServiceValidationException(dynamic dto)
        {
            var serviceProvider = new Mock<IServiceProvider>();
            var validationResults = new List<ValidationResult>();
            System.ComponentModel.DataAnnotations.ValidationContext validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(dto, serviceProvider.Object, null);
            return new ServiceValidationException(validationContext, validationResults);
        }

        [Fact]
        public void GetMonitoring_Success()
        {
            var mock = GetMocks();
            mock.Service
               .Setup(s => s.GetMonitoring(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>()))
               .Returns(new List<GarmentFinanceExportSalesOutstandingReportViewModel>());
            //Act
            IActionResult response = GetController(mock).Get(1, 1, "code");

            //Assert
            Assert.NotNull(response);
        }

        [Fact]
        public void GetMonitoringwithError()
        {
            var mock = GetMocks();

            mock.Service
                .Setup(s => s.GetMonitoring(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>()))
                .Throws(new Exception());

            //Act
            IActionResult response = GetController(mock).Get(1, 1, "code");

            //Assert
            int statusCode = this.GetStatusCode(response);
            Assert.Equal((int)HttpStatusCode.InternalServerError, statusCode);
        }
        [Fact]
        public void Should_Success_GetXls()
        {
            var mocks = GetMocks();

            mocks.Service.Setup(f => f.GenerateExcel(It.IsAny<int>(), It.IsAny<int>(), "end",7))
               .ReturnsAsync(new MemoryStream());
            var response = GetController(mocks).GetXls(1, 1, "end");
            Assert.NotNull(response);

        }
        [Fact]
        public void Should_Error_GetXls()
        {
            var mocks = GetMocks();

            mocks.Service.Setup(f => f.GenerateExcel(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>()))
               .Throws(new Exception());
            var response = GetController(mocks).GetXls(1, 1, "a");
            Assert.NotNull(response);

        }
    }
}
