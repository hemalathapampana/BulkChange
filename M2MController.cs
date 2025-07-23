using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Configuration;
using System.Data;
using System.Data.Entity;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.ModelBinding;
using System.Web.Mvc;
using Amop.Core.Constants;
using Amop.Core.Enumerations;
using Amop.Core.Models;
using Amop.Core.Models.DeviceBulkChange;
using Amop.Core.Models.Revio;
using Amop.Core.Services.Http;
using Amop.Core.Services.Revio;
using KeySys.BaseMultiTenant.Controllers.AmopInternal;
using KeySys.BaseMultiTenant.Helpers;
using KeySys.BaseMultiTenant.Mapping;
using KeySys.BaseMultiTenant.Models;
using KeySys.BaseMultiTenant.Models.Bandwidth.Api;
using KeySys.BaseMultiTenant.Models.BulkChange;
using KeySys.BaseMultiTenant.Models.CustomClasses;
using KeySys.BaseMultiTenant.Models.Device;
using KeySys.BaseMultiTenant.Models.M2M;
using KeySys.BaseMultiTenant.Models.Repositories;
using KeySys.BaseMultiTenant.Models.RevCustomer;
using KeySys.BaseMultiTenant.Models.ThingSpace;
using KeySys.BaseMultiTenant.Repositories;
using KeySys.BaseMultiTenant.Repositories.CarrierRatePlan;
using KeySys.BaseMultiTenant.Repositories.Device;
using KeySys.BaseMultiTenant.Repositories.M2M;
using KeySys.BaseMultiTenant.Repositories.Rev;
using KeySys.BaseMultiTenant.Resources;
using KeySys.BaseMultiTenant.Services.ThingSpaceService;
using KeySys.BaseMultiTenant.Utilities;
using Newtonsoft.Json;
using AdvancedFilter = KeySys.BaseMultiTenant.Models.M2M.AdvancedFilter;

namespace KeySys.BaseMultiTenant.Controllers
{
    public class M2MController : AmopBaseController
    {
        #region constants
        private const PortalTypes PORTAL_TYPE = PortalTypes.M2M;
        private const int ARCHIVAL_RECENT_USAGE_VALIDATION_DAYS = 30;
        private const string ACTIVE = "active";
        private const string PENDING_ACTIVE = "pending activation";
        #endregion

        #region public method
        // GET: M2M
        public ActionResult Index([QueryString] AdvancedFilter advancedFilter = null, string filter = "", int page = 1, string sort = "", string sortDir = "")
        {
            ViewBag.PageTitle = "M2M Inventory";

            if (permissionManager.UserCannotAccess(Session, ModuleEnum.M2M))
                return RedirectToAction("Index", "Home");

            var repository = new M2MInventoryRepository(altaWrxDb, permissionManager);
            var rowCount = repository.GetInventoryCount(filter, advancedFilter);
            var model = new M2MInventoryModel
            {
                AdvancedFilter = advancedFilter?.WithMultiselectDeserializationFix(),
                Filter = filter,
                DeviceInventoryList = PagedList.ToPagedList(repository.GetInventory(filter, advancedFilter, page, sort, sortDir), rowCount),
                AdvancedFilterServiceProviders = AdvancedFilterServiceProviders(),
                AdvancedFilterStatuses = AdvancedFilterStatuses(),
                JasperAllowedStatusList = ListHelper.JasperAllowedStatusList(altaWrxDb, permissionManager),
                ThingSpaceAllowedStatusList = ListHelper.ThingspaceAllowedStatusList(altaWrxDb),
                TealAllowedStatusList = ListHelper.TealAllowedStatusList(altaWrxDb),
                PondAllowedStatusList = ListHelper.PondAllowedStatusList(altaWrxDb),
                ThingSpaceStatusReasonCodeList = ListHelper.ThingSpaceDeviceStatusReasonCodeList(permissionManager, "deactive"),
                DefaultThingSpaceDeviceStatusZipCode = GetCustomObjectValue(permissionManager, CustomObjectKeys.DEFAULT_THINGSPACE_ZIPCODE) ?? "",
                BillingTimeZone = TimeZoneHelper.GetTimeZoneInfo(permissionManager.AltaworxCentralConnectionString)
            };

            if ((advancedFilter == null || !advancedFilter.IsUsingAdvancedFilter()) && string.IsNullOrWhiteSpace(filter))
            {
                model.CachedRowCount = rowCount;
            }

            return View(model);
        }

        public FileStreamResult DeviceInventoryExport([QueryString] AdvancedFilter advancedFilter = null, string filter = "", int cachedRowCount = 0)
        {
            if (permissionManager.UserCannotAccess(Session, ModuleEnum.M2M))
                return null;

            var repository = new M2MInventoryExportRepository(altaWrxDb, permissionManager);
            var m2mInventoryRepository = new M2MInventoryRepository(altaWrxDb, permissionManager);
            var canAccessRevCustomers = permissionManager.UserCanAccess(Session, ModuleEnum.RevCustomers);
            var isSuperAdmin = permissionManager.UserIsSuperAdmin(Session);
            var isParentAdmin = permissionManager.IsParentAdmin;

            int lineItemCount;

            if ((advancedFilter == null || !advancedFilter.IsUsingAdvancedFilter()) && string.IsNullOrWhiteSpace(filter) && cachedRowCount > 0)
            {
                lineItemCount = cachedRowCount;
            }
            else
            {
                lineItemCount = m2mInventoryRepository.GetInventoryCount(filter, advancedFilter);
            }

            byte[] combinedBytes = new byte[0];
            var currentPage = 0;
            for (int batchNumber = 0; batchNumber <= lineItemCount; batchNumber += CommonConstants.PORTAL_EXPORT_PAGE_SIZE)
            {
                var lineItemOnePage = repository.GetInventory(filter, advancedFilter, currentPage, CommonConstants.PORTAL_EXPORT_PAGE_SIZE);

                combinedBytes = ExcelUtilities.WriteDeviceInventory(combinedBytes, lineItemOnePage, batchNumber,
                    canAccessRevCustomers,
                    isSuperAdmin,
                    isParentAdmin,
                    permissionManager.OptimizationSettings.OptIntoCrossProviderCustomerOptimization,
                    permissionManager.MultipleCostCenter,
                    permissionManager.AltaworxCentralConnectionString);

                currentPage++;
            }

            MemoryStream content = new MemoryStream();
            ExcelUtilities.GenerateExcelFileFromByteArrays(combinedBytes, content);
            content.Position = 0;
            return File(content, CommonConstants.APPLICATION_OCTET_STREAM, $"ReportM2MInventory_{FileNameTimestamp()}.{ExcelFileExtension}");
        }

        public ActionResult SIMOrderForm()
        {
            ViewBag.PageTitle = "SIM Order Form";
            if (permissionManager.UserCannotAccess(Session, ModuleEnum.M2M))
            {
                return RedirectToAction("Index", "Home");
            }

            UserRepository userRep = new UserRepository(db);
            SIMOrderModel model = new SIMOrderModel();

            var serviceProviderRepository = new ServiceProviderRepository(altaWrxDb);
            var providerValues = serviceProviderRepository.GetAllByPortalType(PortalTypes.M2M).ToList();
            TempData[CommonStrings.ServiceProviderValues] = providerValues;

            var sessionUser = SessionHelper.User(Session);
            var dbUser = userRep.GetById(sessionUser.id);
            if (dbUser != null)
            {
                model.CompanyName = user.Tenant.Name;
                model.UserId = dbUser.id;
                model.ContactName = $"{dbUser.FirstName} {dbUser.LastName}";
                model.ccEmail = dbUser.Email;
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SIMOrderForm(SIMOrderModel model)
        {
            // This is the e-mail address that SIM order e-mails will be sent to.
            string emailRecipient = ConfigurationManager.AppSettings["SIM_Order_Form_To_Email"];

            var emailChecker = new EmailAddressAttribute();
            bool validToEmail = model.ccEmail != null && emailChecker.IsValid(model.ccEmail);
            if (ModelState.IsValid && validToEmail)
            {
                try
                {
                    UserRepository userRep = new UserRepository(db);
                    var dbUser = userRep.GetById(model.UserId);

                    string additionalCC = null;
                    if (dbUser != null && dbUser.Email != model.ccEmail)
                    {
                        additionalCC = dbUser.Email;
                    }
                    Notifier.SendSIMOrderForm(emailRecipient, model.ccEmail, additionalCC, model);

                    var actionUser = dbUser ?? SessionHelper.User(Session);
                    UserActionRepository.LogAction(actionUser, "Devices", $"The SIM Order form was sent by UserId:{actionUser.id} to {model.ccEmail} with Contact Name: {model.ContactName}.");

                    TempData["Alert"] = "The SIM Order form was sent.";
                    TempData["AlertType"] = "success";

                    return RedirectToAction("SIMOrderForm");
                }
                catch (Exception ex)
                {
                    Log.Error("Error sending the order form", ex);
                    TempData["Alert"] = $"Error: {ex.Message}";
                    TempData["AlertType"] = "danger";

                    return View(model);
                }
            }
            else
            {
                // Model is not valid return to the SIM Order Form screen
                string msg = validToEmail ? "SIM Order Form is Not Valid." : "SIM Order Form is Not Valid. Invalid To Email.";
                TempData["Alert"] = msg;
                TempData["AlertType"] = "danger";

                return View(model);
            }
        }

        private List<SelectListItem> AdvancedFilterServiceProviders()
        {
            var serviceProviders = ListHelper.ServiceProviderList(altaWrxDb, PORTAL_TYPE)
                .Select(provider => new SelectListItem { Text = provider.DisplayName, Value = provider.id.ToString() })
                .ToList();
            serviceProviders.Insert(0, new SelectListItem { Text = string.Empty, Value = string.Empty });
            return serviceProviders;
        }

        private List<SelectListItem> AdvancedFilterStatuses()
        {
            var statuses = ListHelper.DeviceStatuses(altaWrxDb, PORTAL_TYPE)
                .Select(status => new SelectListItem { Text = $"{status.DisplayName} ({status.Integration.Name})", Value = status.Status })
                .ToList();
            statuses.Insert(0, new SelectListItem { Text = string.Empty, Value = string.Empty });
            return statuses;
        }

        public FileContentResult ThingSpaceReasonCodeList()
        {
            var thingSpaceAllowedStatusList = altaWrxDb.DeviceStatus
                .Where(x => !x.IsDeleted && x.IsActive && x.AllowsApiUpdate && x.IntegrationId == (int)IntegrationType.ThingSpace)
                .OrderBy(x => x.Status)
                .ToList();

            DataSet dsReasonCodes = new DataSet();
            foreach (var status in thingSpaceAllowedStatusList)
            {
                var thingSpaceDeviceStatusReasonCodes = altaWrxDb.ThingSpaceDeviceStatusReasonCodes
                    .Where(x => x.IsActive && !x.IsDeleted && x.DeviceStatu.Status == status.Status && x.DeviceStatu.IntegrationId == (int)IntegrationType.ThingSpace)
                    .ToList();

                if (thingSpaceDeviceStatusReasonCodes.Count > 0)
                {
                    // add to dataset
                    var tempDataSet = thingSpaceDeviceStatusReasonCodes.Select(x => new ThingSpaceStatusReasonCodeLite(x)).ToDataSet(status.DisplayName + " Codes");
                    dsReasonCodes.Merge(tempDataSet.Tables[0]);
                }
            }

            var bytes = ExcelUtilities.Export(dsReasonCodes);

            var file = File(bytes, ExcelContentType, $"ThingSpaceReasonCodes.{ExcelFileExtension}");
            return file;
        }

        [HttpGet]
        public ActionResult SearchDevices(int? serviceProviderId = null, string filter = "")
        {
            if (!permissionManager.UserCanAccessPortalTypeModule(Session, PORTAL_TYPE))
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
            }

            var repository = new M2MInventoryRepository(altaWrxDb, permissionManager);
            var devices = repository.SearchDevices(serviceProviderId, filter, 25);

            return Json(devices.Select(device => new { id = device.id, label = device.ICCID, value = device.ICCID }), JsonRequestBehavior.AllowGet);
        }

        public ActionResult BulkChanges(int? serviceProviderId = null, int? changeType = null, string filter = null, int page = 1, int pageSize = 25, string sort = null, string sortDir = null)
        {
            if (!permissionManager.UserCanAccessPortalTypeModule(Session, PORTAL_TYPE))
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
            }

            var serviceProviderRepository = new ServiceProviderRepository(altaWrxDb);
            var serviceProviders = serviceProviderRepository.GetAllByPortalType(PortalTypes.M2M);
            var integrationTypes = serviceProviders.Select(serviceProvider => (IntegrationType)serviceProvider.IntegrationId).ToArray();

            var changeTypeRepository = new DeviceChangeRequestTypeRepository(altaWrxDb);
            ICollection<DeviceChangeRequestType> changeTypes;
            if (serviceProviderId.HasValue)
            {
                var serviceProvider = serviceProviders.First(sp => sp.id == serviceProviderId.Value);
                changeTypes = changeTypeRepository.GetAllByIntegrationId(serviceProvider.IntegrationId, permissionManager.HighestRoleForUserInTenant());
            }
            else
            {
                changeTypes = changeTypeRepository.GetAllByIntegrationType(permissionManager.HighestRoleForUserInTenant(), integrationTypes);
            }

            var changeRepository = new DeviceBulkChangeRepository(altaWrxDb, permissionManager);
            var search = new BulkChangeSearch
            {
                ServiceProviderId = serviceProviderId,
                ChangeType = changeType,
                Filter = filter,
                PortalType = PORTAL_TYPE
            };
            var changes = changeRepository.GetBulkChanges(search, sort, sortDir, page, pageSize);
            TimeZoneHelper.ApplySystemTimezoneForBulkChangeDateTimeColumn(changes, permissionManager.AltaworxCentralConnectionString, out string simpleTimeZoneInfoName);
            var totalCount = changeRepository.GetBulkChangeCount(search);
            var model = new BulkChangeListViewModel
            {
                PageSize = pageSize,
                ServiceProviderId = serviceProviderId,
                ChangeType = changeType,
                Filter = filter,
                IsSuperAdmin = permissionManager.UserIsSuperAdmin(), // AWXSUP-490
                ServiceProviders = serviceProviders,
                ChangeTypes = changeTypes,
                Changes = PagedList.ToPagedList(changes, totalCount),
                ThingSpaceStatusReasonCodeList = ListHelper.ThingSpaceDeviceStatusReasonCodeList(permissionManager, "deactive"),
                DefaultThingSpaceDeviceStatusZipCode = GetCustomObjectValue(permissionManager, CustomObjectKeys.DEFAULT_THINGSPACE_ZIPCODE) ?? "",
                IsBulkChangeRunning = changeRepository.CheckBulkChangeIsRunning((int)PortalTypes.M2M),
                TimeZoneInfoName = simpleTimeZoneInfoName
            };

            return View("BulkChanges", model);
        }

        public FileContentResult M2MBulkChangeExport(int bulkChangeId)
        {
            if (!permissionManager.UserCanAccessPortalTypeModule(Session, PORTAL_TYPE))
            {
                return null;
            }
            var changeRepository = new DeviceBulkChangeRepository(altaWrxDb, permissionManager);
            var bulkChange = changeRepository.GetBulkChange(bulkChangeId);
            if (bulkChange != null && bulkChange.PortalTypeId == (int)PORTAL_TYPE)
            {
                var serviceProviderRepository = new ServiceProviderRepository(altaWrxDb);
                var serviceProvider = serviceProviderRepository.GetById(bulkChange.ServiceProviderId);

                var lineItems = altaWrxDb.usp_M2MBulkChangeLogExport((int)bulkChange.id).ToList();
                var listTemp = new List<BulkChangeDetailExportModel>();

                var deviceChangeRepository = new M2MDeviceChangeRepository(altaWrxDb, permissionManager);
                var m2mDeviceChange = deviceChangeRepository.GetChanges(bulkChangeId).FirstOrDefault();
                var changeRequest = new BulkChangeStatusUpdateRequest<BulkChangeStatusUpdateThingSpace>();

                if (!string.IsNullOrWhiteSpace(m2mDeviceChange?.DeviceChangeRequest?.ChangeRequest))
                {
                    changeRequest = JsonConvert.DeserializeObject<BulkChangeStatusUpdateRequest<BulkChangeStatusUpdateThingSpace>>(m2mDeviceChange.DeviceChangeRequest.ChangeRequest);
                }
                else
                {
                    Log.Info($"M2M Change Request is empty. BulkChangeId: {m2mDeviceChange.BulkChangeId}");
                }
                string tableName = "Bulk Change Detail";
                foreach (var lineItem in lineItems)
                {
                    var itemAdd = new BulkChangeDetailExportModel
                    {
                        ChangeRequestType = lineItem.ChangeRequestType,
                        CreatedBy = lineItem.CreatedBy,
                        CreatedDate = lineItem.CreatedDate.ToString("MM/dd/yyyy"),
                        ProcessedBy = lineItem.ProcessedBy,
                        Status = lineItem.Status,
                        StatusDetails = lineItem.StatusDetails,
                        SubscriberNumber = lineItem.ICCID,
                        ProcessedDate = null
                    };

                    if (lineItem.ProcessedDate != null)
                    {
                        itemAdd.ProcessedDate = ((DateTime)lineItem.ProcessedDate).ToString("MM/dd/yyyy");
                    }

                    if (lineItem.ChangeRequestTypeId == (int)DeviceChangeType.StatusUpdate && serviceProvider.IntegrationId == (int)IntegrationEnum.ThingSpace
                        && changeRequest.UpdateStatus == DeviceStatusConstant.ThingSpace_Active || changeRequest.UpdateStatus == DeviceStatusConstant.ThingSpace_Pending_Activate)
                    {
                        itemAdd.MSISDN = lineItem.MSISDN;
                        itemAdd.IPAddress = lineItem.IPAddress;
                    }

                    listTemp.Add(itemAdd);
                }
                var data = listTemp.ToDataSet(tableName);
                data.Tables[tableName].Columns["Subscriber Number"].ColumnName = "ICCID";

                var bytes = ExcelUtilities.Export(data);

                return File(bytes, ExcelContentType, $"ReportM2MInventory_{FileNameTimestamp()}.{ExcelFileExtension}");
            }
            else
            {
                return null;
            }
        }

        [HttpGet]
        public ActionResult BulkChange(long id, int page = 1, int pageSize = 25, string sort = null, string sortDir = null)
        {
            if (!permissionManager.UserCanAccessPortalTypeModule(Session, PORTAL_TYPE))
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
            }

            var changeRepository = new DeviceBulkChangeRepository(altaWrxDb, permissionManager);
            var bulkChange = changeRepository.GetBulkChange(id);

            if (bulkChange == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound);
            }

            var serviceProviderRepository = new ServiceProviderRepository(altaWrxDb);
            var serviceProvider = serviceProviderRepository.GetById(bulkChange.ServiceProviderId);

            var deviceChangeRepository = new M2MDeviceChangeRepository(altaWrxDb, permissionManager);
            var changes = deviceChangeRepository.GetChanges(id, sort, sortDir, page, pageSize);
            var changeCount = deviceChangeRepository.GetCountForBulkChange(id);
            var allowDeleteAndRetry = false;
            foreach (var change in changes)
            {
                if (change.StatusDetails != null)
                {
                    Match deviceExistsMatch = Regex.Match(change.StatusDetails, CommonConstants.DEVICE_EXISTS_PATTERN);
                    if (deviceExistsMatch.Success && bulkChange.Status == BulkChangeStatus.PROCESSED &&
                        bulkChange.ErrorCount.GetValueOrDefault() > 0 &&
                        bulkChange.IntegrationId == (int)IntegrationType.ThingSpace)
                    {
                        allowDeleteAndRetry = true;
                        break;
                    }
                }
            }
            var model = new BulkChangeDetailsViewModel
            {
                BulkChangeId = id,
                BulkchangeDelayMinute = int.Parse(Resources.CommonStrings.BulkchangeRetryMinutes) * int.Parse(Resources.CommonStrings.BulkchangeTryTimeNumber),
                Count = bulkChange.ChangeCount.GetValueOrDefault(),
                ProcessedCount = bulkChange.ProcessedCount.GetValueOrDefault(),
                ErrorCount = bulkChange.ErrorCount.GetValueOrDefault(),
                Status = bulkChange.Status,
                ProcessedDate = bulkChange.ProcessedDate,
                ChangeTypeId = bulkChange.ChangeRequestTypeId,
                IntegrationTypeId = serviceProvider.IntegrationId,
                TargetStatus = GetThingSpaceStatusUpdateUpdateStatus(serviceProvider, changes?.FirstOrDefault()?.DeviceChangeRequest?.ChangeRequest),
                Details = PagedList.ToPagedList(changes, changeCount),
                AllowDeleteAndRetry = allowDeleteAndRetry
            };

            return View("BulkChange", model);
        }

        private static string GetThingSpaceStatusUpdateUpdateStatus(Models.Repositories.ServiceProvider serviceProvider, string changeRequestString)
        {
            if (serviceProvider.IntegrationId == (int)IntegrationEnum.ThingSpace && !string.IsNullOrWhiteSpace(changeRequestString))
            {
                return JsonConvert.DeserializeObject<BulkChangeStatusUpdateRequest<BulkChangeStatusUpdateThingSpace>>(changeRequestString).UpdateStatus;
            }
            else
            {
                return null;
            }
        }

        [HttpPost]
        public ActionResult ValidateBulkChange(BulkChangeCreateModel bulkChangeCreateModel)
        {
            if (!permissionManager.UserCanCreate(Session, ModuleEnum.M2M))
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
            }

            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    isValid = false,
                    validationMessage = "Errors Validating Model",
                    errors = ModelState.SelectMany(modelState => modelState.Value.Errors).Select(error => error.ErrorMessage).ToArray()
                });
            }

            var serviceProviderId = bulkChangeCreateModel.ServiceProviderId.GetValueOrDefault();
            var changeTypeId = bulkChangeCreateModel.ChangeType.GetValueOrDefault();
            var changeType = (DeviceChangeType)changeTypeId;
            var changes = BuildChangeDetails(altaWrxDb, Session, permissionManager, bulkChangeCreateModel, serviceProviderId, changeType).ToList();
            var changesWithErrors = changes.Where(x => x.HasErrors && x.StatusDetails.StartsWith("Active Rev Services")).ToList();
            if (changesWithErrors.Count > 0)
            {
                return Json(new
                {
                    isValid = false,
                    validationMessage = "One or more devices have active Rev Services",
                    errors = changesWithErrors.Select(x => x.ICCID).ToArray()
                });
            }
            else
            {
                return Json(new { isValid = true });
            }
        }

        [HttpPost]
        public async Task<ActionResult> BulkChange(BulkChangeCreateModel bulkChangeCreateModel)
        {
            if (!permissionManager.UserCanCreate(Session, ModuleEnum.M2M))
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
            }

            if (!ModelState.IsValid && bulkChangeCreateModel.RevService.EffectiveDate != null)
            {
                return Json(new
                {
                    errors = ModelState.SelectMany(modelState => modelState.Value.Errors).Select(error => error.ErrorMessage).ToArray()
                });
            }

            var changeRepository = new DeviceBulkChangeRepository(altaWrxDb, permissionManager);
            var serviceProviderId = bulkChangeCreateModel.ServiceProviderId.GetValueOrDefault();
            var changeTypeId = bulkChangeCreateModel.ChangeType.GetValueOrDefault();
            var changeType = (DeviceChangeType)changeTypeId;
            try
            {
                var changes = BuildChangeDetails(altaWrxDb, Session, permissionManager, bulkChangeCreateModel, serviceProviderId, changeType).ToList();
                var bulkchangeStatus = BulkChangeStatus.PROCESSED;
                if (changes.Any(change => !change.IsProcessed))
                {
                    bulkchangeStatus = BulkChangeStatus.NEW;
                }
                var bulkChange = new DeviceBulkChange
                {
                    ChangeRequestTypeId = changeTypeId,
                    ServiceProviderId = serviceProviderId,
                    TenantId = permissionManager.PermissionFilter.LoggedInTenantId,
                    SiteId = GetSiteIdForBulkChange(changes),
                    Status = bulkchangeStatus,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = SessionHelper.GetAuditByName(Session),
                    IsActive = true,
                    IsDeleted = false,
                    M2M_DeviceChange = changes
                };

                changeRepository.CreateBulkChange(bulkChange);
                if (bulkChangeCreateModel.ProcessChanges.GetValueOrDefault())
                {
                    return await ProcessBulkChange(bulkChange.id);
                }

                return Json(new { redirectUrl = Url.Action("BulkChange", "M2M", new { bulkChange.id }) }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { errors = new[] { $"An error occurred: {ex.Message}" } }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public async Task<ActionResult> ProcessBulkChange(long id)
        {
            if (!permissionManager.UserCanEdit(Session, ModuleEnum.M2M))
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
            }

            var result = await ProcessBulkChange(this, Url, altaWrxDb, Session, id);

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        public ActionResult RevenueAssurance(string filter, Utils.CustomerAssignedFilter customerAssignedFilter = Utils.CustomerAssignedFilter.All,
            bool showVarianceOnly = true)
        {
            ViewBag.PageTitle = "Revenue Assurance";
            var tenantId = permissionManager.PermissionFilter.LoggedInTenantId;
            var m2mRevenueAssuranceGroupModel = new M2MRevenueAssuranceGroupModel();
            var m2mRevenueAssuranceGroupRepository = new M2MRevenueAssuranceGroupRepository(altaWrxDb, permissionManager);
            var group = m2mRevenueAssuranceGroupRepository.GetM2MRevenueAssuraceGroup(tenantId, filter, customerAssignedFilter, showVarianceOnly);
            if (group != null)
            {
                m2mRevenueAssuranceGroupModel.Filter = filter;
                m2mRevenueAssuranceGroupModel.CustomerAssignedFilter = customerAssignedFilter;
                m2mRevenueAssuranceGroupModel.M2MRevenueAssuranceGroup = group;
                m2mRevenueAssuranceGroupModel.ShowVarianceOnly = showVarianceOnly;
            }

            return View(m2mRevenueAssuranceGroupModel);
        }

        public ActionResult GetRevenueAssuranceByCustomer(string customerId, int page = 1, int pageSize = 25,
           string sort = null, string sortDir = null, bool showVarianceOnly = true)
        {
            var tenantId = permissionManager.PermissionFilter.LoggedInTenantId;
            var m2mRevServiceProductRepository = new M2MRevServiceProductRepository(altaWrxDb);
            var DIDsFilter = new List<string>();
            if (!customerId.Equals(CommonConstants.UNASSIGNED))
            {
                DIDsFilter = ListHelper.GetIdentifierListFromDIDs(altaWrxDb, customerId, permissionManager.Tenant.id, base64Service, IsProduction, Int32.MaxValue - 1);
            }
            var totalDevicesCount = m2mRevServiceProductRepository.GetM2MRevServiceProductByCustomerCount(tenantId, customerId, showVarianceOnly, DIDsFilter: DIDsFilter);
            var revServiceProductsByCustomer = m2mRevServiceProductRepository.GetM2MRevServiceProductByCustomer(tenantId, customerId, showVarianceOnly, page, sort, sortDir, DIDsFilter: DIDsFilter);
            var revServiceProducts = PagedList.ToPagedList(revServiceProductsByCustomer, totalDevicesCount);
            var activeDevicesCount = m2mRevServiceProductRepository.GetActiveRevServiceProductByCustomerCount(tenantId, customerId, showVarianceOnly);
            var isVariant = revServiceProductsByCustomer.Any(rsp => rsp.IsActiveStatus != rsp.RevIsActiveStatus);
            var m2mRevenueAssuranceDeviceListModel = new M2MRevenueAssuranceDeviceListModel
            {
                RevCustomerId = customerId,
                RevServiceProducts = revServiceProducts,
                ActiveDevicesCount = activeDevicesCount,
                TotalDevicesCount = totalDevicesCount,
                IsVariant = isVariant,
                BillingTimeZone = TimeZoneHelper.GetTimeZoneInfo(permissionManager.AltaworxCentralConnectionString)
            };
            return PartialView("_RevCustomerM2MRevenueAssurance", m2mRevenueAssuranceDeviceListModel);
        }

        public ActionResult RevenueAssuranceExport(string filter,
            Utils.CustomerAssignedFilter customerAssignedFilter = Utils.CustomerAssignedFilter.All, bool showVarianceOnly = true)
        {
            if (permissionManager.UserCannotAccess(Session, ModuleEnum.M2M))
                return null;

            var billingTimeZone = TimeZoneHelper.GetTimeZoneInfo(permissionManager.AltaworxCentralConnectionString);
            var tenantId = permissionManager.PermissionFilter.LoggedInTenantId;
            var m2mRevServiceProductRepository = new M2MRevServiceProductRepository(altaWrxDb);
            var revServiceProducts = m2mRevServiceProductRepository.GetM2MRevServiceProductExport(tenantId, filter, customerAssignedFilter, showVarianceOnly)
                .Select(rs => rs.ToRevenueAssuranceExport(billingTimeZone)).ToList();

            var data = revServiceProducts.ToDataSet("M2M Revenue Assurance");
            var bytes = ExcelUtilities.Export(data);

            return File(bytes, ExcelContentType, $"M2M_Revenue_Assurance_{FileNameTimestamp()}.{ExcelFileExtension}");
        }

        [HttpPost]
        public async Task<ActionResult> AssociateCustomer(BulkChangeAssociateCustomerModel model)
        {
            if (!permissionManager.UserCanCreate(Session, ModuleEnum.M2M))
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
            }

            try
            {
                model.Devices = model.Devices.Distinct().ToArray();
                var changeRepository = new DeviceBulkChangeRepository(altaWrxDb, permissionManager);
                var changeType = DeviceChangeType.CustomerAssignment;
                var useCarrierActivation = model.EffectiveDate == null ? true : false;
                // Active statuses
                var deviceStatus = altaWrxDb.DeviceStatus.AsNoTracking().Where(x => x.IsActiveStatus).Select(y => y.Status).ToList();
                var devices = altaWrxDb.vwM2MDeviceInventory.AsNoTracking().Where(x => x.ServiceProviderId == model.ServiceProviderId && model.Devices.Contains(x.ICCID)).ToList();

                var deviceIds = devices.Select(x => x.id);
                var deviceTenants = altaWrxDb.Device_Tenant
                    .Include(x => x.RevService)
                    .Where(rs => deviceIds.Contains(rs.DeviceId) && rs.IsActive && !rs.IsDeleted).ToList();
                var revCustomers = ListHelper.GetRevCustomerList(permissionManager, permissionManager.Tenant.id, permissionManager.PermissionFilter.GetRevAccountFilter());
                var revCustomer = revCustomers.FirstOrDefault(x => x.RevCustomerId.Contains(model.RevCustomerId));
                var sites = altaWrxDb.Sites.AsNoTracking().Where(x => x.RevCustomerId == revCustomer.id && x.IsActive && x.TenantId == permissionManager.Tenant.id && x.RevCustomer.IsActive && !x.RevCustomer.IsDeleted).ToList();

                var bulkChange = new DeviceBulkChange
                {
                    ChangeRequestTypeId = (int)changeType,
                    ServiceProviderId = model.ServiceProviderId,
                    TenantId = permissionManager.PermissionFilter.LoggedInTenantId,
                    Status = BulkChangeStatus.NEW,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = SessionHelper.GetAuditByName(Session),
                    IsActive = true,
                    IsDeleted = false,
                    M2M_DeviceChange =
                        BuildAssociateCustomerDeviceChanges(altaWrxDb, Session, permissionManager, model, devices, deviceTenants, sites, deviceStatus, revCustomers, useCarrierActivation).ToList()
                };

                var bulkChangeId = changeRepository.CreateBulkChange(bulkChange);

                if (!model.CreateRevService && !model.AddCarrierRatePlan)
                {
                    ProcessBulkAssociateAMOP(changeRepository, devices, sites, revCustomers, deviceTenants, bulkChange.id, model);
                }
                // Lambda does not create rev service if Create Service is false, but will try processing Carrier Rate plan change, and Device ID update if write enabled
                await ProcessBulkChange(bulkChange.id);
                return new JsonResult { Data = new { Success = true, ChangeId = bulkChangeId } };
            }
            catch (Exception e)
            {
                return new JsonResult { Data = new { Success = false, e.Message } };
            }
        }

        public async Task<ActionResult> AssociateAmopCustomer(BulkChangeAssociateNonRevCustomerModel model)
        {
            if (!permissionManager.UserCanCreate(Session, ModuleEnum.M2M) && permissionManager.UserCanAccess(Session, ModuleEnum.RevCustomers))
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
            }
            try
            {
                model.Devices = model.Devices.Distinct().ToArray();
                var changeRepository = new DeviceBulkChangeRepository(altaWrxDb, permissionManager);
                var changeType = DeviceChangeType.CustomerAssignment;
                var changes = BuildAssociateAmopCustomerDeviceChanges(altaWrxDb, Session, permissionManager, model).ToList();
                var bulkChange = new DeviceBulkChange
                {
                    ChangeRequestTypeId = (int)changeType,
                    ServiceProviderId = model.ServiceProviderId,
                    TenantId = permissionManager.PermissionFilter.LoggedInTenantId,
                    Status = changes.Any(change => !change.IsProcessed) ? BulkChangeStatus.NEW : BulkChangeStatus.PROCESSED,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = SessionHelper.GetAuditByName(Session),
                    IsActive = true,
                    IsDeleted = false,
                    M2M_DeviceChange = changes
                };
                changeRepository.CreateBulkChange(bulkChange);
                await ProcessBulkAssociateAMOPCustomer(bulkChange.id);
                return new JsonResult { Data = new { Success = true } };
            }
            catch (Exception e)
            {
                return new JsonResult { Data = new { Success = false, e.Message } };
            }
        }

        [HttpPost]
        public ActionResult UpdateM2MCustomerRatePlan(int deviceId, decimal? customerDataAllocationMB, int? customerRatePlanId, int? customerRatePoolId, DateTime? effectiveDate, string customerRatePlanName, string customerRatePoolName)
        {
            try
            {
                var deviceRepository = new DeviceRepository(altaWrxDb);
                var device = deviceRepository.GetDeviceById(deviceId);
                if (device == null)
                    return new JsonResult { Data = new { Success = false, Message = "Error updating Customer Rate Plan for device. Device not found." } };

                var deviceTenant = altaWrxDb.Device_Tenant.FirstOrDefault(x => x.DeviceId == deviceId && x.TenantId == permissionManager.Tenant.id);
                var deviceActionHistories = new List<DeviceActionHistory>();

                var previousCustomerRatePlan = deviceTenant.JasperCustomerRatePlan?.RatePlanName;
                var previousCustomerRatePool = deviceTenant.CustomerRatePool?.Name;
                if (customerRatePlanId != null && customerRatePlanId > 0)
                {
                    deviceTenant.CustomerRatePlanId = customerRatePlanId;
                    deviceTenant.CustomerDataAllocationMB = customerDataAllocationMB;
                }
                else if (customerRatePlanId != CommonConstants.NO_CHANGE)
                {
                    deviceTenant.CustomerRatePlanId = null;
                    deviceTenant.CustomerDataAllocationMB = null;
                }

                if (customerRatePoolId != null && customerRatePoolId >= 0)
                {
                    deviceTenant.CustomerRatePoolId = customerRatePoolId;
                }
                else if (customerRatePoolId != CommonConstants.NO_CHANGE)
                {
                    deviceTenant.CustomerRatePoolId = null;
                }
                deviceTenant.ModifiedDate = DateTime.UtcNow;
                deviceTenant.ModifiedBy = SessionHelper.GetAuditByName(Session);

                try
                {
                    altaWrxDb.Entry(deviceTenant).State = EntityState.Modified;
                    altaWrxDb.SaveChanges();


                    if (previousCustomerRatePlan != customerRatePlanName && customerRatePlanId != CommonConstants.NO_CHANGE)
                    {
                        var deviceActionHistory = new DeviceActionHistory()
                        {
                            ServiceProviderId = device.ServiceProviderId,
                            M2MDeviceId = device.id,
                            ICCID = device.ICCID,
                            MSISDN = device.MSISDN,
                            PreviousValue = previousCustomerRatePlan,
                            CurrentValue = customerRatePlanName,
                            ChangedField = CommonStrings.CustomerRatePlan,
                            ChangeEventType = CommonStrings.UpdateCustomerRatePlan,
                            DateOfChange = DateTime.UtcNow,
                            ChangedBy = SessionHelper.GetAuditByName(Session),
                            Username = device.Username,
                            CustomerAccountName = deviceTenant.Site.Name,
                            CustomerAccountNumber = deviceTenant.AccountNumber,
                            TenantId = permissionManager.Tenant.id,
                            IsActive = true,
                            IsDeleted = false
                        };
                        deviceActionHistories.Add(deviceActionHistory);
                    }
                    if (previousCustomerRatePool != customerRatePoolName && customerRatePoolId != CommonConstants.NO_CHANGE)
                    {
                        var deviceActionHistory = new DeviceActionHistory()
                        {
                            ServiceProviderId = device.ServiceProviderId,
                            M2MDeviceId = device.id,
                            ICCID = device.ICCID,
                            MSISDN = device.MSISDN,
                            PreviousValue = previousCustomerRatePool,
                            CurrentValue = customerRatePoolName,
                            ChangedField = CommonStrings.CustomerRatePool,
                            ChangeEventType = CommonStrings.UpdateCustomerRatePlan,
                            DateOfChange = DateTime.UtcNow,
                            ChangedBy = SessionHelper.GetAuditByName(Session),
                            Username = device.Username,
                            CustomerAccountName = deviceTenant.Site.Name,
                            CustomerAccountNumber = deviceTenant.AccountNumber,
                            TenantId = permissionManager.Tenant.id,
                            IsActive = true,
                            IsDeleted = false
                        };
                        deviceActionHistories.Add(deviceActionHistory);
                    }
                    altaWrxDb.DeviceActionHistories.AddRange(deviceActionHistories);
                    altaWrxDb.SaveChanges();
                    altaWrxDb.usp_UpdateCrossProviderDeviceHistory(deviceTenant.DeviceId.ToString(), string.Empty, (int)PortalTypeEnum.M2M, deviceTenant.TenantId, device.ServiceProviderId, effectiveDate);
                    //send notification trigger to 2.0 from 1.0 to Customer Rate plan
                    int? tenantId = permissionManager.Tenant.id;
                    OptimizationApiController optimizationApiController = new OptimizationApiController();
                    optimizationApiController.SendTriggerAmopSync("m2m_inventory_live_sync", tenantId, null);
                }
                catch (Exception ex)
                {
                    Log.Error("Error UpdateM2MCustomerRatePlan", ex);
                    return new JsonResult
                    {
                        Data = new
                        {
                            Success = false,
                            Message = "Error updating Customer Rate Plan for device"
                        }
                    };
                }

                UserActionRepository.LogAction(SessionHelper.LoggedInUser(Session), "M2M", $"Updated Customer Rate Plan to {customerRatePlanId} for {deviceId}");

                var isM2MHistorySuccessfulUpdate = UpdateM2MDeviceHistory(device, deviceTenant, effectiveDate);
                if (!isM2MHistorySuccessfulUpdate)
                {
                    return new JsonResult
                    {
                        Data = new
                        {
                            Success = false,
                            Message = "Successfully updated Customer Rate Plan but there was an error updating M2M Device History."
                        }
                    };
                }

                return new JsonResult
                {
                    Data = new
                    {
                        Success = true,
                        Message = "Successfully updated Customer Rate Plan."
                    }
                };
            }
            catch (Exception ex)
            {
                return new JsonResult
                {
                    Data = new
                    {
                        Success = false,
                        Message = $"Error updating Customer Rate Plan for device. {ex.Message}"
                    }
                };
            }
        }

        public ActionResult ChangeIdentifier(int serviceProviderId)
        {
            if (!permissionManager.UserCanAccessPortalTypeModule(Session, PORTAL_TYPE))
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
            }

            var model = new BulkchangeUpdateICCIDorIMEI()
            {
                ServiceProviderId = serviceProviderId,
                PortalType = PortalTypes.M2M,
                EffectiveDate = DateTime.UtcNow
            };

            return PartialView("_BulkChangeUpdateIdentifier", model);
        }

        [HttpPost]
        public async Task<ActionResult> PostChangeIdentifier(BulkchangeUpdateICCIDorIMEI model)
        {
            if (!permissionManager.UserCanAccessPortalTypeModule(Session, PORTAL_TYPE))
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
            }
            try
            {
                model.Devices = model.Devices.Distinct().ToList();
                var changeRepository = new DeviceBulkChangeRepository(altaWrxDb, permissionManager);
                var changeType = DeviceChangeType.ChangeICCIDorIMEI;
                var bulkChange = new DeviceBulkChange
                {
                    ChangeRequestTypeId = (int)changeType,
                    ServiceProviderId = model.ServiceProviderId,
                    TenantId = permissionManager.PermissionFilter.LoggedInTenantId,
                    Status = BulkChangeStatus.NEW,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = SessionHelper.GetAuditByName(Session),
                    IsActive = true,
                    IsDeleted = false,
                    M2M_DeviceChange = BuildChangeIdentifier(altaWrxDb, Session, model).ToList()
                };
                var bulkChangeId = changeRepository.CreateBulkChange(bulkChange);
                await ProcessBulkChange(bulkChange.id);
                return new JsonResult { Data = new { Success = true, ChangeId = bulkChangeId } };
            }
            catch (Exception ex)
            {
                return new JsonResult { Data = new { Success = false, ex.Message } };
            }
        }

        internal static IEnumerable<M2M_DeviceChange> BuildChangeIdentifier(AltaWorxCentral_Entities awxDb, HttpSessionStateBase session, BulkchangeUpdateICCIDorIMEI model)
        {
            var createdBy = SessionHelper.GetAuditByName(session);
            var deviceRepository = new DeviceRepository(awxDb);
            var deviceList = deviceRepository.GetDevices(model.ServiceProviderId, model.Devices);
            var deviceChanges = new List<M2M_DeviceChange>();
            
            foreach (var modelDevice in model.Devices.Select((item, index) => new { item, index }))
            {
                var phoneNumber = modelDevice.item;
                if (!string.IsNullOrWhiteSpace(phoneNumber))
                {
                    // Step 1: Check old identifier exists in Device table
                    var device = deviceList.FirstOrDefault(x => phoneNumber.Equals(x.EID) || phoneNumber.Equals(x.MSISDN));
                    if (device == null)
                    {
                        deviceChanges.Add(CreateDeviceChangeError(phoneNumber, 
                            string.Format("Device with identifier {0} not found", phoneNumber), createdBy));
                        continue;
                    }

                    // Step 2: Verify device is active status
                    if (!device.DeviceStatu.IsActiveStatus)
                    {
                        deviceChanges.Add(CreateDeviceChangeError(phoneNumber, 
                            string.Format("Device {0} is not in active status", phoneNumber), createdBy));
                        continue;
                    }

                    var newICCID = string.Empty;
                    var newIMEI = string.Empty;
                    var identifierType = IdentifierTypeEnum.ICCID;
                    
                    if (model.NewICCIDs != null && model.NewICCIDs.Count > modelDevice.index)
                    {
                        newICCID = model.NewICCIDs[modelDevice.index];
                        identifierType = IdentifierTypeEnum.ICCID;
                        
                        // Step 3: Validate new identifier format
                        if (!ValidateICCIDFormat(newICCID))
                        {
                            deviceChanges.Add(CreateDeviceChangeError(phoneNumber, 
                                string.Format("Invalid ICCID format: {0}", newICCID), createdBy));
                            continue;
                        }

                        // Step 4: Check new identifier not already in use
                        if (IsIdentifierInUse(awxDb, newICCID, "ICCID"))
                        {
                            deviceChanges.Add(CreateDeviceChangeError(phoneNumber, 
                                string.Format("ICCID {0} is already in use", newICCID), createdBy));
                            continue;
                        }
                    }
                    
                    if (model.NewIMEIs != null && model.NewIMEIs.Count > modelDevice.index)
                    {
                        newIMEI = model.NewIMEIs[modelDevice.index];
                        identifierType = IdentifierTypeEnum.IMEI;
                        
                        // Step 3: Validate new identifier format
                        if (!ValidateIMEIFormat(newIMEI))
                        {
                            deviceChanges.Add(CreateDeviceChangeError(phoneNumber, 
                                string.Format("Invalid IMEI format: {0}", newIMEI), createdBy));
                            continue;
                        }

                        // Step 4: Check new identifier not already in use
                        if (IsIdentifierInUse(awxDb, newIMEI, "IMEI"))
                        {
                            deviceChanges.Add(CreateDeviceChangeError(phoneNumber, 
                                string.Format("IMEI {0} is already in use", newIMEI), createdBy));
                            continue;
                        }
                    }

                    // If all validations pass, create the device change
                    deviceChanges.Add(new M2M_DeviceChange(BuildIdentifierChangeRequest(newICCID, newIMEI, device, model, identifierType), device.id, device.ICCID, createdBy));
                }
            }
            return deviceChanges;
        }

        private static bool ValidateICCIDFormat(string iccid)
        {
            // ICCID should be 19-20 digits
            if (string.IsNullOrWhiteSpace(iccid))
                return false;
                
            // Remove any spaces or dashes
            var cleanIccid = iccid.Replace(" ", "").Replace("-", "");
            
            // Check if it's all digits and proper length
            return cleanIccid.All(char.IsDigit) && cleanIccid.Length >= 19 && cleanIccid.Length <= 20;
        }

        private static bool ValidateIMEIFormat(string imei)
        {
            // IMEI should be 15 digits
            if (string.IsNullOrWhiteSpace(imei))
                return false;
                
            // Remove any spaces or dashes
            var cleanImei = imei.Replace(" ", "").Replace("-", "");
            
            // Check if it's all digits and proper length
            return cleanImei.All(char.IsDigit) && cleanImei.Length == 15;
        }

        private static bool IsIdentifierInUse(AltaWorxCentral_Entities awxDb, string identifier, string identifierType)
        {
            try
            {
                if (identifierType == "ICCID")
                {
                    return awxDb.Devices.Any(d => d.ICCID == identifier && !d.IsDeleted);
                }
                else if (identifierType == "IMEI")
                {
                    return awxDb.Devices.Any(d => d.IMEI == identifier && !d.IsDeleted);
                }
            }
            catch (Exception)
            {
                // If there's an error checking, assume it's in use to be safe
                return true;
            }
            return false;
        }

        private static string BuildIdentifierChangeRequest(string newICCID, string newIMEI, Device device, BulkchangeUpdateICCIDorIMEI model, IdentifierTypeEnum identifierType)
        {
            var request = new BulkChangeStatusUpdateRequest<BulkChangeUpdateIdentifier>
            {
                Request = new BulkChangeUpdateIdentifier()
                {
                    OldICCID = device.ICCID,
                    NewICCID = newICCID,
                    OldIMEI = device.IMEI,
                    NewIMEI = newIMEI,
                    IdentifierType = identifierType,
                    AddCustomerRatePlan = model.IsChangeCustomerRatePlan,
                    CustomerRatePlan = model.CustomerRatePlanId.ToString(),
                    CustomerRatePool = model.CustomerRatePoolId.ToString()
                }
            };
            return JsonConvert.SerializeObject(request);
        }

        public ActionResult SIMOrderDetailSection()
        {
            return PartialView(new SIMOrderDetail());
        }
        #endregion

        #region private method
        private int? GetSiteIdForBulkChange(IEnumerable<M2M_DeviceChange> changes)
        {
            if (permissionManager.IsParentAdmin)
            {
                return null;
            }

            var userSiteId = UserSiteId;
            if (userSiteId != null)
            {
                return userSiteId;
            }

            var deviceIds = changes.Select(change => change.DeviceId).ToList();
            return altaWrxDb.Device_Tenant
                .FirstOrDefault(device => device.TenantId == permissionManager.Tenant.id && deviceIds.Contains(device.id) && device.SiteId != null)?.SiteId;
        }

        internal static async Task<object> ProcessBulkChange(AmopBaseController controller, UrlHelper url, AltaWorxCentral_Entities awxDb, HttpSessionStateBase session, long id)
        {
            var bulkChange = awxDb.DeviceBulkChanges.Find(id);
            if (bulkChange == null)
            {
                return new HttpNotFoundResult();
            }

            var newBulkChangeStatus = BulkChangeStatus.PROCESSED;
            if (bulkChange.M2M_DeviceChange.Any(change => !change.IsProcessed))
            {
                newBulkChangeStatus = BulkChangeStatus.PROCESSING;
                var customObjectDbList = controller.GetTenantCustomFields();
                var awsAccessKey = controller.AwsAccessKeyFromCustomObjects(customObjectDbList);
                var awsSecretAccessKey = controller.AwsSecretAccessKeyFromCustomObjects(customObjectDbList);
                var queueName = ValueFromCustomObjects(customObjectDbList, CommonConstants.CUSTOM_OBJECT_BULK_CHANGE_QUEUE_KEY);
                var sqsHelper = new SqsHelper(awsAccessKey, awsSecretAccessKey);
                var errorMessage = await sqsHelper.EnqueueBulkChangeAsync(queueName, id);
                if (!string.IsNullOrWhiteSpace(errorMessage))
                {
                    return new { errors = new[] { string.Format(LogCommonStrings.ERROR_WHEN_QUEUEING_BULK_CHANGE, errorMessage) } };
                }
            }

            var processedBy = SessionHelper.GetAuditByName(session);
            var processedDate = DateTime.UtcNow;
            bulkChange.Status = newBulkChangeStatus;
            bulkChange.ProcessedBy = processedBy;
            bulkChange.ProcessedDate = processedDate;
            bulkChange.ModifiedBy = processedBy;
            bulkChange.ModifiedDate = processedDate;
            await awxDb.SaveChangesAsync();

            return new { redirectUrl = url.Action("BulkChange", "M2M", new { id }) };
        }

        private void ProcessBulkAssociateAMOP(DeviceBulkChangeRepository deviceBulkChangeRepository, List<vwM2MDeviceInventory> devices, List<Site> sites, List<RevCustomer> revCustomers, List<Device_Tenant> device_Tenants, long bulkChangeId, BulkChangeAssociateCustomerModel model)
        {
            var m2mDeviceBulkChangeRepository = new M2MDeviceChangeRepository(altaWrxDb, permissionManager);
            var deviceBulkChanges = m2mDeviceBulkChangeRepository.GetUnprocessedChanges(bulkChangeId);
            var sessionUser = SessionHelper.GetAuditByName(Session);

            var m2mDeviceChangeNeedToProcess = new List<M2M_DeviceChange>();
            var deviceTenantNeedToProcess = new List<Device_Tenant>();
            var bulkChangeLogNeedToProcess = new List<DeviceBulkChangeLog>();
            var deviceIdList = new List<string>();

            foreach (var deviceChange in deviceBulkChanges)
            {
                var changeRequest = JsonConvert.DeserializeObject<BulkChangeAssociateCustomerModel>(deviceChange.DeviceChangeRequest?.ChangeRequest);
                var changeRequestDateIsInvalid = (changeRequest.EffectiveDate == null || (changeRequest.EffectiveDate >= DateTime.MinValue && changeRequest.EffectiveDate?.ToUniversalTime() <= DateTime.UtcNow));
                var m2mDevice = devices.FirstOrDefault(x => x.id == deviceChange.DeviceId.GetValueOrDefault());
                if (m2mDevice == null)
                {
                    string statusMessage = "AMOP Device not found";

                    deviceChange.Status = BulkChangeStatus.ERROR;
                    deviceChange.HasErrors = true;
                    deviceChange.StatusDetails = statusMessage;
                    deviceChange.ModifiedDate = DateTime.UtcNow;
                    deviceChange.ModifiedBy = sessionUser;
                    m2mDeviceChangeNeedToProcess.Add(deviceChange);

                    bulkChangeLogNeedToProcess.Add(new DeviceBulkChangeLog
                    {
                        BulkChangeId = bulkChangeId,
                        HasErrors = true,
                        LogEntryDescription = "AMOP Customer Assignment",
                        M2MDeviceChangeId = deviceChange.id,
                        ProcessedBy = sessionUser,
                        ProcessedDate = DateTime.UtcNow,
                        RequestText = deviceChange.DeviceChangeRequest?.ChangeRequest,
                        ResponseStatus = BulkChangeStatus.ERROR,
                        ErrorText = statusMessage,
                        ResponseText = "Preflight Check Failure"
                    });

                    continue;
                }
                var revCustomer = revCustomers.FirstOrDefault(x => x.RevCustomerId.Contains(changeRequest.RevCustomerId));
                var site = sites.FirstOrDefault(x => x.RevCustomerId == revCustomer.id);

                if (site == null)
                {
                    string statusMessage = "Rev Customer not found";

                    deviceChange.Status = BulkChangeStatus.ERROR;
                    deviceChange.HasErrors = true;
                    deviceChange.StatusDetails = statusMessage;
                    deviceChange.ModifiedDate = DateTime.UtcNow;
                    deviceChange.ModifiedBy = sessionUser;
                    m2mDeviceChangeNeedToProcess.Add(deviceChange);

                    bulkChangeLogNeedToProcess.Add(new DeviceBulkChangeLog
                    {
                        BulkChangeId = bulkChangeId,
                        HasErrors = true,
                        LogEntryDescription = "AMOP Customer Assignment",
                        M2MDeviceChangeId = deviceChange.id,
                        ProcessedBy = sessionUser,
                        ProcessedDate = DateTime.UtcNow,
                        RequestText = deviceChange.DeviceChangeRequest?.ChangeRequest,
                        ResponseStatus = BulkChangeStatus.ERROR,
                        ErrorText = statusMessage,
                        ResponseText = "Preflight Check Failure"
                    });

                    continue;
                }

                var deviceTenant = device_Tenants.FirstOrDefault(x => x.DeviceId == m2mDevice.id && x.TenantId == permissionManager.Tenant.id);

                if (deviceTenant == null)
                {
                    deviceTenant = new Device_Tenant()
                    {
                        DeviceId = m2mDevice.id,
                        TenantId = permissionManager.Tenant.id,
                        SiteId = site.id,
                        AccountNumber = revCustomer.RevCustomerId,
                        AccountNumberIntegrationAuthenticationId = revCustomer.IntegrationAuthenticationId,
                        IsActive = true,
                        IsDeleted = false
                    };

                    if (changeRequestDateIsInvalid && !string.IsNullOrWhiteSpace(changeRequest.CustomerRatePlan))
                    {
                        int.TryParse(changeRequest.CustomerRatePlan, out int customerRatePlanId);
                        deviceTenant.CustomerRatePlanId = customerRatePlanId;
                    }

                    if (changeRequestDateIsInvalid && !string.IsNullOrWhiteSpace(changeRequest.CustomerRatePool))
                    {
                        int.TryParse(changeRequest.CustomerRatePool, out int customerRatePoolId);
                        deviceTenant.CustomerRatePoolId = customerRatePoolId;
                    }

                    deviceTenant.CreatedDate = DateTime.UtcNow;
                    deviceTenant.CreatedBy = sessionUser;
                }
                else
                {
                    deviceTenant.SiteId = site.id;
                    deviceTenant.AccountNumber = revCustomer.RevCustomerId;
                    deviceTenant.AccountNumberIntegrationAuthenticationId = revCustomer.IntegrationAuthenticationId;

                    if (changeRequestDateIsInvalid && !string.IsNullOrWhiteSpace(changeRequest.CustomerRatePlan))
                    {
                        int.TryParse(changeRequest.CustomerRatePlan, out int customerRatePlanId);
                        deviceTenant.CustomerRatePlanId = customerRatePlanId;
                    }

                    if (changeRequestDateIsInvalid && !string.IsNullOrWhiteSpace(changeRequest.CustomerRatePool))
                    {
                        int.TryParse(changeRequest.CustomerRatePool, out int customerRatePoolId);
                        deviceTenant.CustomerRatePoolId = customerRatePoolId;
                    }
                    else
                    {
                        deviceTenant.CustomerRatePoolId = null;
                    }

                    deviceTenant.ModifiedDate = DateTime.UtcNow;
                    deviceTenant.ModifiedBy = sessionUser;
                }
                if (changeRequestDateIsInvalid && !string.IsNullOrWhiteSpace(changeRequest.CustomerRatePlan))
                {
                    deviceTenant.CustomerRatePlanId = int.Parse(changeRequest.CustomerRatePlan);
                }
                deviceTenant.ModifiedDate = DateTime.UtcNow;
                deviceTenant.ModifiedBy = sessionUser;
                deviceTenantNeedToProcess.Add(deviceTenant);

                bulkChangeLogNeedToProcess.Add(new DeviceBulkChangeLog
                {
                    BulkChangeId = bulkChangeId,
                    HasErrors = false,
                    LogEntryDescription = "AMOP Customer Assignment",
                    M2MDeviceChangeId = deviceChange.id,
                    ProcessedBy = sessionUser,
                    ProcessedDate = DateTime.UtcNow,
                    RequestText = deviceChange.DeviceChangeRequest?.ChangeRequest,
                    ResponseStatus = BulkChangeStatus.PROCESSED,
                    ResponseText = "ok"
                });

                if (!string.IsNullOrWhiteSpace(changeRequest.CustomerRatePlan))
                {
                    bulkChangeLogNeedToProcess.Add(new DeviceBulkChangeLog
                    {
                        BulkChangeId = bulkChangeId,
                        HasErrors = false,
                        LogEntryDescription = "AMOP Customer Rate Plan Assignment",
                        M2MDeviceChangeId = deviceChange.id,
                        ProcessedBy = sessionUser,
                        ProcessedDate = DateTime.UtcNow,
                        RequestText = deviceChange.DeviceChangeRequest?.ChangeRequest,
                        ResponseStatus = BulkChangeStatus.PROCESSED,
                        ResponseText = "ok"
                    });
                }

                if (!string.IsNullOrWhiteSpace(changeRequest.CustomerRatePool))
                {
                    bulkChangeLogNeedToProcess.Add(new DeviceBulkChangeLog
                    {
                        BulkChangeId = bulkChangeId,
                        HasErrors = false,
                        LogEntryDescription = "AMOP Customer Rate Pool Assignment",
                        M2MDeviceChangeId = deviceChange.id,
                        ProcessedBy = sessionUser,
                        ProcessedDate = DateTime.UtcNow,
                        RequestText = deviceChange.DeviceChangeRequest?.ChangeRequest,
                        ResponseStatus = BulkChangeStatus.PROCESSED,
                        ResponseText = "ok"
                    });
                }
                deviceIdList.Add(deviceChange.DeviceId.ToString());
            }
            BulkUpdateForDeviceChange(m2mDeviceChangeNeedToProcess);
            BulkCopyForDeviceBulkChangeLog(bulkChangeLogNeedToProcess);
            BulkCopyAndUpdateForDeviceTenant(deviceBulkChangeRepository, deviceTenantNeedToProcess, bulkChangeId);
            if (!string.IsNullOrWhiteSpace(model.CustomerRatePlan) || !string.IsNullOrWhiteSpace(model.CustomerRatePool))
            {
                altaWrxDb.usp_UpdateCrossProviderDeviceHistory(string.Join(",", deviceIdList), string.Empty, (int)PortalTypeEnum.M2M, permissionManager.Tenant.id, model.ServiceProviderId, model.EffectiveDate);
            }
        }
        private void BulkUpdateForDeviceChange(List<M2M_DeviceChange> deviceChanges)
        {
            if (deviceChanges.Count > 0)
            {
                //build data table
                var deviceUpdatesTable = new DataTable();
                deviceUpdatesTable.Columns.Add("Id");
                deviceUpdatesTable.Columns.Add("Status");
                deviceUpdatesTable.Columns.Add("HasErrors");
                deviceUpdatesTable.Columns.Add("StatusDetails");
                deviceUpdatesTable.Columns.Add("ModifiedDate");
                deviceUpdatesTable.Columns.Add("ModifiedBy");

                foreach (var deviceChange in deviceChanges)
                {
                    var newDeviceInfo = deviceUpdatesTable.NewRow();

                    newDeviceInfo[0] = deviceChange.id;
                    newDeviceInfo[1] = deviceChange.Status;
                    newDeviceInfo[2] = deviceChange.HasErrors;
                    newDeviceInfo[3] = deviceChange.StatusDetails;
                    newDeviceInfo[4] = deviceChange.ModifiedDate;
                    newDeviceInfo[5] = deviceChange.ModifiedBy;

                    deviceUpdatesTable.Rows.Add(newDeviceInfo);
                }
                //script build temptable
                var scriptCreateTempTable = @"
                    CREATE TABLE #tblTmp(
                        [Id] [int] NOT NULL, 
                        [Status][nvarchar](50) NULL, 
                        [HasErrors] [BIT] NULL,
                        [StatusDetails] [nvarchar] (500) NULL, 
                        [ModifiedDate] DATETIME NULL,
                        [ModifiedBy] [nvarchar] (50) NULL);";
                //script update to table
                var table = "M2M_DeviceChange";
                var updateScript = @"
                        UPDATE Target SET 
                            Status=Temp.Status, 
                            HasErrors=Temp.HasErrors,
                            StatusDetails=Temp.StatusDetails,
                            ModifiedDate=Temp.ModifiedDate,
                            ModifiedBy=Temp.ModifiedBy
                        FROM {0} Target 
                        INNER JOIN #tblTmp Temp ON Target.Id = Temp.Id;";
                var scriptUpdateTable = string.Format(updateScript, table);
                SQLHelper.SQLBulkUpdate(deviceUpdatesTable, permissionManager.AltaworxCentralConnectionStringWithoutEF, scriptCreateTempTable, scriptUpdateTable, table, "#tblTmp");
            }
        }
        private void BulkCopyForDeviceBulkChangeLog(List<DeviceBulkChangeLog> deviceBulkChangeLogs)
        {
            if (deviceBulkChangeLogs.Count > 0)
            {
                var DeviceBulkChangeLog = new DataTable();
                DeviceBulkChangeLog.Columns.Add("BulkChangeId");
                DeviceBulkChangeLog.Columns.Add("HasErrors");
                DeviceBulkChangeLog.Columns.Add("LogEntryDescription");
                DeviceBulkChangeLog.Columns.Add("M2MDeviceChangeId");
                DeviceBulkChangeLog.Columns.Add("ProcessedBy");
                DeviceBulkChangeLog.Columns.Add("ProcessedDate");
                DeviceBulkChangeLog.Columns.Add("RequestText");
                DeviceBulkChangeLog.Columns.Add("ResponseStatus");
                DeviceBulkChangeLog.Columns.Add("ResponseText");

                foreach (var deviceBulkChangeLog in deviceBulkChangeLogs)
                {
                    var deviceBulkChangeLogInfo = DeviceBulkChangeLog.NewRow();

                    deviceBulkChangeLogInfo[0] = deviceBulkChangeLog.BulkChangeId;
                    deviceBulkChangeLogInfo[1] = deviceBulkChangeLog.HasErrors;
                    deviceBulkChangeLogInfo[2] = deviceBulkChangeLog.LogEntryDescription;
                    deviceBulkChangeLogInfo[3] = deviceBulkChangeLog.M2MDeviceChangeId;
                    deviceBulkChangeLogInfo[4] = deviceBulkChangeLog.ProcessedBy;
                    deviceBulkChangeLogInfo[5] = deviceBulkChangeLog.ProcessedDate;
                    deviceBulkChangeLogInfo[6] = deviceBulkChangeLog.RequestText;
                    deviceBulkChangeLogInfo[7] = deviceBulkChangeLog.ResponseStatus;
                    deviceBulkChangeLogInfo[8] = deviceBulkChangeLog.ResponseText;

                    DeviceBulkChangeLog.Rows.Add(deviceBulkChangeLogInfo);
                }
                var columnMappings = new List<SqlBulkCopyColumnMapping>()
                {
                    new SqlBulkCopyColumnMapping("BulkChangeId", "BulkChangeId"),
                    new SqlBulkCopyColumnMapping("HasErrors", "HasErrors"),
                    new SqlBulkCopyColumnMapping("LogEntryDescription", "LogEntryDescription"),
                    new SqlBulkCopyColumnMapping("M2MDeviceChangeId", "M2MDeviceChangeId"),
                    new SqlBulkCopyColumnMapping("ProcessedBy", "ProcessedBy"),
                    new SqlBulkCopyColumnMapping("ProcessedDate", "ProcessedDate"),
                    new SqlBulkCopyColumnMapping("RequestText", "RequestText"),
                    new SqlBulkCopyColumnMapping("ResponseStatus", "ResponseStatus"),
                    new SqlBulkCopyColumnMapping("ResponseText", "ResponseText")
                };
                SQLHelper.SQLBulkCopy(DeviceBulkChangeLog, permissionManager.AltaworxCentralConnectionStringWithoutEF, "DeviceBulkChangeLog", columnMappings);
            }
        }

        private void BulkCopyAndUpdateForDeviceTenant(DeviceBulkChangeRepository deviceBulkChangeRepository, List<Device_Tenant> deviceTenants, long bulkChangeId)
        {
            try
            {
                var tenantName = permissionManager.Tenant.Name;
                if (deviceTenants.Count > 0)
                {
                    var deviceTenantDataTable = BuildDataTableForDeviceTenant(deviceTenants);
                    deviceBulkChangeRepository.DeviceBulkChangeInsertUpdateDeviceTenant(deviceTenantDataTable, permissionManager.AltaworxCentralConnectionStringWithoutEF, bulkChangeId, (int)PortalTypes.M2M);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error When Bulk Update for Device Tenant", ex);
            }

        }
        private DataTable BuildDataTableForDeviceTenant(List<Device_Tenant> deviceTenants)
        {
            var deviceTenantTable = new DataTable();
            deviceTenantTable.Columns.Add("DeviceId");
            deviceTenantTable.Columns.Add("TenantId");
            deviceTenantTable.Columns.Add("SiteId");
            deviceTenantTable.Columns.Add("AccountNumber");
            deviceTenantTable.Columns.Add("AccountNumberIntegrationAuthenticationId");
            deviceTenantTable.Columns.Add("CustomerRatePlanId");
            deviceTenantTable.Columns.Add("IsActive");
            deviceTenantTable.Columns.Add("IsDeleted");


            foreach (var deviceTenant in deviceTenants)
            {
                var deviceTenantInfo = deviceTenantTable.NewRow();

                deviceTenantInfo[0] = deviceTenant.DeviceId;
                deviceTenantInfo[1] = deviceTenant.TenantId;
                deviceTenantInfo[2] = deviceTenant.SiteId;
                deviceTenantInfo[3] = deviceTenant.AccountNumber;
                deviceTenantInfo[4] = deviceTenant.AccountNumberIntegrationAuthenticationId;
                deviceTenantInfo[5] = deviceTenant.CustomerRatePlanId;
                deviceTenantInfo[6] = deviceTenant.IsActive ? 1 : 0;
                deviceTenantInfo[7] = deviceTenant.IsActive ? 1 : 0;

                deviceTenantTable.Rows.Add(deviceTenantInfo);
            }
            return deviceTenantTable;
        }

        private async Task ProcessBulkAssociateAMOPCustomer(long bulkChangeId)
        {
            var m2mDeviceBulkChangeRepository = new M2MDeviceChangeRepository(altaWrxDb, permissionManager);
            var changes = m2mDeviceBulkChangeRepository.GetUnprocessedChanges(bulkChangeId);
            {
                var change = changes.First();
                var dataTableUpdates = BuildTableAssignNonRevCustomer(changes);
                await DeviceBulkChangeAssignNonRevCustomer(permissionManager.AltaworxCentralConnectionStringWithoutEF, dataTableUpdates, bulkChangeId, change.id);
            }
        }

        private DataTable BuildTableAssignNonRevCustomer(IList<M2M_DeviceChange> changes)
        {
            var table = new DataTable();
            table.Columns.Add("DeviceId");
            table.Columns.Add("TenantId");
            table.Columns.Add("SiteId", typeof(int));
            foreach (var change in changes.Where(change => !string.IsNullOrWhiteSpace(change.DeviceChangeRequest?.ChangeRequest)))
            {
                var associateNonRevCustomerModel = JsonConvert.DeserializeObject<BulkChangeAssociateNonRevCustomerModel>(change.DeviceChangeRequest?.ChangeRequest);
                var dataRow = AddDataToTableAssignNonRev(table, change, associateNonRevCustomerModel);
                table.Rows.Add(dataRow);
            }
            return table;
        }

        private DataRow AddDataToTableAssignNonRev(DataTable table, M2M_DeviceChange detailRecord, BulkChangeAssociateNonRevCustomerModel nonRevCustomerModel)
        {
            var dr = table.NewRow();
            dr[0] = detailRecord.DeviceId;
            dr[1] = nonRevCustomerModel.TenantId;
            dr[2] = nonRevCustomerModel.SiteId;
            return dr;
        }

        private async Task DeviceBulkChangeAssignNonRevCustomer(string CentralDbConnectionString, DataTable table, long bulkChangeId, long deviceChangeId)
        {
            DeviceChangeResult<string, string> dbResult;
            var deviceBulkChangeLogRepository = new DeviceBulkChangeLogRepository(altaWrxDb);
            try
            {
                using (var conn = new SqlConnection(CentralDbConnectionString))
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.CommandText = "dbo.usp_DeviceBulkChange_Assign_Non_Rev_Customer";
                        SqlParameter newrecordParam = cmd.Parameters.Add("@UpdatedValues", SqlDbType.Structured);
                        cmd.Parameters["@UpdatedValues"].Value = table;
                        cmd.Parameters["@UpdatedValues"].TypeName = "dbo.UpdateM2MDeviceMobilityDeviceSiteType";
                        cmd.Parameters.AddWithValue("@bulkChangeId", bulkChangeId);
                        cmd.Parameters.AddWithValue("@portalTypeId", PortalTypes.M2M);
                        conn.Open();

                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                dbResult = new DeviceChangeResult<string, string>()
                {
                    ActionText = "usp_DeviceBulkChange_Assign_Non_Rev_Customer",
                    HasErrors = false,
                    RequestObject = $"bulkChangeId: {bulkChangeId}",
                    ResponseObject = "OK"
                };
            }
            catch (Exception ex)
            {
                Log.Error($"Error Executing Stored Procedure usp_DeviceBulkChange_Assign_Non_Rev_Customer: {ex.Message} {ex.StackTrace}");
                var logId = Guid.NewGuid();
                dbResult = new DeviceChangeResult<string, string>()
                {
                    ActionText = "usp_DeviceBulkChange_Assign_Non_Rev_Customer",
                    HasErrors = true,
                    RequestObject = $"bulkChangeId: {bulkChangeId}",
                    ResponseObject = $"Error Executing Stored Procedure. Ref: {logId}"
                };
            }
            deviceBulkChangeLogRepository.AddM2MLogEntry(new CreateM2MDeviceBulkChangeLog()
            {
                BulkChangeId = bulkChangeId,
                ErrorText = dbResult.HasErrors ? dbResult.ResponseObject : null,
                HasErrors = dbResult.HasErrors,
                LogEntryDescription = "AssignNonRevCustomer: Update AMOP",
                M2MDeviceChangeId = deviceChangeId,
                ProcessBy = "AltaworxDeviceBulkChange",
                ProcessedDate = DateTime.UtcNow,
                ResponseStatus = dbResult.HasErrors ? BulkChangeStatus.ERROR : BulkChangeStatus.PROCESSED,
                RequestText = dbResult.ActionText + Environment.NewLine + dbResult.RequestObject,
                ResponseText = dbResult.ResponseObject
            });

            var bulkChange = altaWrxDb.DeviceBulkChanges.Find(bulkChangeId);
            var processedBy = SessionHelper.GetAuditByName(Session);
            var processedDate = DateTime.UtcNow;
            bulkChange.Status = BulkChangeStatus.PROCESSED;
            bulkChange.ProcessedBy = processedBy;
            bulkChange.ProcessedDate = processedDate;
            bulkChange.ModifiedBy = processedBy;
            bulkChange.ModifiedDate = processedDate;
            await altaWrxDb.SaveChangesAsync();
        }

        private void MarkM2MDeviceAsProcessed(IList<M2M_DeviceChange> deviceChanges, string sessionUser)
        {
            foreach (var device in deviceChanges)
            {
                device.IsProcessed = true;
                device.ProcessedDate = DateTime.UtcNow;
                device.ProcessedBy = sessionUser;
                device.ModifiedDate = DateTime.UtcNow;
                device.ModifiedBy = sessionUser;
                altaWrxDb.Entry(device).State = EntityState.Modified;
            }
        }

        internal static string CreateAssociateCustomerChangeRequest(List<Site> sites, BulkChangeAssociateCustomerModel model, vwM2MDeviceInventory device, int integrationAuthenticationId, RevCustomer revCustomer, string jasperDeviceId)
        {
            var site = GetSiteInfo(sites, revCustomer);

            if (model.ActivatedDate == null)
            {
                model.ActivatedDate = DateTime.UtcNow;
            }

            return JsonConvert.SerializeObject(new BulkChangeAssociateCustomer()
            {
                Number = device.MSISDN,
                ICCID = device.ICCID,
                RevCustomerId = model.RevCustomerId,
                DeviceId = device.id,
                CreateRevService = model.CreateRevService,
                ServiceTypeId = model.ServiceTypeId.GetValueOrDefault(0),
                RevPackageId = model.RevPackageId,
                RevProductIdList = model.RevProductIdList,
                RateList = model.RateList,
                Prorate = model.Prorate,
                Description = model.Description,
                EffectiveDate = model.EffectiveDate,
                AddCarrierRatePlan = model.AddCarrierRatePlan,
                CarrierRatePlan = model.CarrierRatePlan,
                CommPlan = model.CommPlan,
                IntegrationAuthenticationId = integrationAuthenticationId,
                JasperDeviceID = jasperDeviceId,
                SiteId = site.id,
                ActivatedDate = model.ActivatedDate,
                ProviderId = model.ProviderId,
                UsagePlanGroupId = model.UsagePlanGroupId,
                AddCustomerRatePlan = model.AddCustomerRatePlan,
                CustomerRatePlan = model.CustomerRatePlan,
                CustomerRatePool = model.CustomerRatePool
            });
        }

        private static string CreateAssociateAmopCustomerChangeRequest(AltaWorxCentral_Entities awxDb, BulkChangeAssociateNonRevCustomerModel model, int tenantId)
        {
            return JsonConvert.SerializeObject(new BulkChangeAssociateNonRevCustomerModel()
            {
                Devices = model.Devices,
                ServiceProviderId = model.ServiceProviderId,
                TenantId = tenantId,
                SiteId = model.SiteId
            });
        }

        private static Site GetSiteInfo(List<Site> sites, RevCustomer revCustomer)
        {
            return sites.FirstOrDefault(x => x.RevCustomerId == revCustomer.id);
        }

        public static bool CheckRevServiceStatus(List<Device_Tenant> device_Tenants, int deviceId, int tenantId)
        {
            var revService = GetRevServiceByDeviceId(device_Tenants, deviceId, tenantId);
            if (revService == null)
            {
                return false;
            }
            AltaWorxCentral_Entities awxDb = new AltaWorxCentral_Entities();
            var RevServiceProduct = awxDb.RevServiceProducts.Where(x => x.ServiceId == revService.RevServiceId && x.IntegrationAuthenticationId == revService.IntegrationAuthenticationId && x.IsActive && !x.IsDeleted).ToList();
            if (RevServiceProduct.Any(x => x.Status == "ACTIVE"))
            {
                return true;
            }
            return false;
        }
        public static bool CheckRevServiceStatusByRevCustomerId(List<Device_Tenant> device_Tenants, List<usp_Get_Rev_Service_Product_By_Rev_Customer_Id_Result> revServiceProducts, int deviceId, int tenantId, string revCustomerId)
        {
            var revService = GetRevServiceByDeviceId(device_Tenants, deviceId, tenantId);
            if (revService != null && revService.DisconnectedDate == null)
            {
                //if a service does not have service product. Or the service product are DISCONNECTED => Create a new service line
                var serviceProducts = GetRevServiceProductByRevService(revServiceProducts, revService.RevServiceId);
                return serviceProducts.Any(x => x.CustomerId == revCustomerId);
            }
            return false;
        }
        private static RevService GetRevServiceByDeviceId(List<Device_Tenant> device_Tenants, int deviceId, int tenantId)
        {
            var revService = device_Tenants
                .Where(rs => rs.DeviceId == deviceId && rs.TenantId == tenantId
                                         && rs.RevServiceId != null && rs.RevService.IsActive && !rs.RevService.IsDeleted)
                .Select(x => x.RevService);
            if (!revService.Any())
            {
                return null;
            }

            return revService.OrderBy(x => x.ActivatedDate).FirstOrDefault();
        }
        private static List<usp_Get_Rev_Service_Product_By_Rev_Customer_Id_Result> GetRevServiceProductByRevService(List<usp_Get_Rev_Service_Product_By_Rev_Customer_Id_Result> revServiceProducts, int revServiceId)
        {
            return revServiceProducts.Where(x => x.ServiceId == revServiceId).ToList();
        }

        private static IEnumerable<M2M_DeviceChange> BuildArchivalChangeDetails(AltaWorxCentral_Entities awxDb, HttpSessionStateBase session, BulkChangeCreateModel bulkChange, int serviceProviderId, int tenantId)
        {
            var iccids = bulkChange.Devices.Where(iccid => !string.IsNullOrWhiteSpace(iccid)).ToList();
            var devicesByICCID = GetDevicesByICCID(awxDb, serviceProviderId, iccids);
            var activeRevServicesByDeviceId = GetActiveRevServicesByDeviceId(awxDb, devicesByICCID.Values.Select(device => device.id).ToList(), tenantId);
            var createdBy = SessionHelper.GetAuditByName(session);
            var archivalRecentUsageCutoff = DateTime.UtcNow.Date.AddDays(-1 * ARCHIVAL_RECENT_USAGE_VALIDATION_DAYS);
            return GetArchivalChanges(bulkChange, iccids, devicesByICCID, activeRevServicesByDeviceId, createdBy, archivalRecentUsageCutoff);
        }

        private static IEnumerable<M2M_DeviceChange> GetArchivalChanges(BulkChangeCreateModel bulkChange, IEnumerable<string> iccids, ConcurrentDictionary<string, Device> devicesByICCID, ConcurrentDictionary<int, ConcurrentBag<RevService>> activeRevServicesByDeviceId, string createdBy, DateTime archivalRecentUsageCutoff)
        {
            var deviceChanges = new List<M2M_DeviceChange>();
            var changeRequest = new DeviceChangeRequest(JsonConvert.SerializeObject(bulkChange,
                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }), DateTime.UtcNow, createdBy);

            foreach (var iccid in iccids)
            {
                if (!devicesByICCID.ContainsKey(iccid))
                {
                    deviceChanges.Add(CreateDeviceChangeError(iccid, "Invalid ICCID", createdBy));
                }
                else
                {
                    var device = devicesByICCID[iccid];
                    var deviceChange = GetDeviceChange(bulkChange, device, iccid, activeRevServicesByDeviceId, createdBy, archivalRecentUsageCutoff, changeRequest);
                    deviceChanges.Add(deviceChange);
                }
            }

            return deviceChanges;
        }

        private static M2M_DeviceChange GetDeviceChange(BulkChangeCreateModel bulkChange, Device device, string iccid,
            ConcurrentDictionary<int, ConcurrentBag<RevService>> activeRevServicesByDeviceId, string createdBy, DateTime archivalRecentUsageCutoff, DeviceChangeRequest changeRequest)
        {
            if (activeRevServicesByDeviceId.ContainsKey(device.id) && !bulkChange.OverrideValidation.GetValueOrDefault(false))
            {
                var activeRevServiceIds = activeRevServicesByDeviceId[device.id].Select(svc => svc.RevServiceId);
                var errorMessage = $"Active Rev Services found associated with device: {string.Join(",", activeRevServiceIds)}";
                return CreateDeviceChangeError(iccid, errorMessage, createdBy);
            }

            if (device.LastUsageDate.HasValue && device.LastUsageDate.Value > archivalRecentUsageCutoff)
            {
                var errorMessage =
                    $"Device has had usage in the last {ARCHIVAL_RECENT_USAGE_VALIDATION_DAYS} days and is ineligible to be archived";
                return CreateDeviceChangeError(iccid, errorMessage, createdBy);
            }

            return new M2M_DeviceChange(changeRequest, device.id, iccid);
        }

        private static IEnumerable<M2M_DeviceChange> BuildCustomerRatePlanChangeDetails(AltaWorxCentral_Entities awxDb, HttpSessionStateBase session, BulkChangeCreateModel bulkChange, int serviceProviderId, DeviceChangeType changeType)
        {
            var iccids = bulkChange.Devices.Where(iccid => !string.IsNullOrWhiteSpace(iccid)).ToList();
            var devicesByICCID = GetDevicesByICCID(awxDb, serviceProviderId, iccids);
            var createdBy = SessionHelper.GetAuditByName(session);
            var serviceProviderRepository = new ServiceProviderRepository(awxDb);
            var integrationId = serviceProviderRepository.getIntegrationIdByServiceProviderId(serviceProviderId);
            if (changeType.Equals(DeviceChangeType.CarrierRatePlanChange))
            {
                var carrierRatePlanRepository = new JasperCarrierRatePlanRepository(awxDb);
                // bulkChange.CarrierRatePlanUpdate.CarrierRatePlan cannot be null because it was validated from UI. So we don't need to check null here.
                var carrierRatePlanCode = bulkChange.CarrierRatePlanUpdate.CarrierRatePlan;
                var carrierRatePlan = carrierRatePlanRepository.GetByCarrierRatePlanCode(carrierRatePlanCode);
                if (carrierRatePlan == null)
                {
                    throw new Exception(string.Format(CommonStrings.CarrierRatePlanNotExist, carrierRatePlanCode));
                }

                if (integrationId.Equals((int)IntegrationType.Teal))
                {
                    bulkChange.CarrierRatePlanUpdate.PlanUuid = carrierRatePlan.PlanUuid;
                }
                else if (integrationId.Equals((int)IntegrationType.Pond))
                {
                    bulkChange.CarrierRatePlanUpdate.RatePlanId = carrierRatePlan.JasperRatePlanId.Value;
                }
            }
            var deviceChanges = new List<M2M_DeviceChange>();
            var changeRequest = new DeviceChangeRequest(JsonConvert.SerializeObject(bulkChange,
                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }), DateTime.UtcNow, createdBy);

            foreach (var iccid in iccids)
            {
                if (!devicesByICCID.ContainsKey(iccid))
                {
                    deviceChanges.Add(CreateDeviceChangeError(iccid, "Invalid ICCID", createdBy));
                }
                else
                {
                    var device = devicesByICCID[iccid];
                    if (changeType.Equals(DeviceChangeType.CarrierRatePlanChange) && integrationId.Equals((int)IntegrationType.Teal) && string.IsNullOrWhiteSpace(device.EID))
                    {
                        deviceChanges.Add(CreateDeviceChangeError(iccid, string.Format(CommonStrings.DeviceMustHaveEID, iccid), createdBy));
                        continue;
                    }
                    deviceChanges.Add(new M2M_DeviceChange(changeRequest, device.id, iccid));
                }
            }

            return deviceChanges;
        }

        private static IEnumerable<M2M_DeviceChange> BuildStatusUpdateChangeDetails(AltaWorxCentral_Entities awxDb, HttpSessionStateBase session, PermissionManager permissionManager, BulkChangeCreateModel bulkChange, int serviceProviderId)
        {
            var serviceProviderRepository = new ServiceProviderRepository(awxDb);
            var serviceProvider = serviceProviderRepository.GetById(serviceProviderId);
            var statusUpdate = bulkChange.StatusUpdate;
            var revService = bulkChange.RevService;

            if (revService != null)
            {
                var revCustomerRepository = new RevCustomerRepository(awxDb, permissionManager.Tenant.id);
                var revCustomer = revCustomerRepository.GetByRevCustomerId(revService.RevCustomerId);
                revService.IntegrationAuthenticationId = revCustomer.IntegrationAuthenticationId.GetValueOrDefault();
            }

            var iccids = bulkChange.Devices.Where(iccid => !string.IsNullOrWhiteSpace(iccid)).ToList();
            var integrationType = (IntegrationType)serviceProvider.IntegrationId;
            switch (integrationType)
            {
                case IntegrationType.Jasper:
                case IntegrationType.POD19:
                case IntegrationType.Teal:
                case IntegrationType.TMobileJasper:
                case IntegrationType.Pond:
                case IntegrationType.Rogers:
                    return BuildStatusUpdateChangeDetailsJasper(awxDb, session, serviceProviderId, iccids, statusUpdate, revService, integrationType);
                case IntegrationType.ThingSpace:
                    // AWXSUP-490
                    var thingSpacePPU = bulkChange.thingSpacePPU;
                    if (!permissionManager.UserIsSuperAdmin() && !string.IsNullOrEmpty(statusUpdate?.ThingSpaceStatusUpdate?.MdnZipCode))
                    {
                        statusUpdate.ThingSpaceStatusUpdate.MdnZipCode = GetCustomObjectValue(permissionManager, CustomObjectKeys.DEFAULT_THINGSPACE_ZIPCODE);
                    }
                    return BuildStatusUpdateChangeDetailsThingSpace(awxDb, session, serviceProviderId, iccids, statusUpdate, revService, thingSpacePPU);
                default:
                    return null;
            }
        }

        private static IEnumerable<M2M_DeviceChange> BuildUsernameChangeDetails(AltaWorxCentral_Entities awxDb, HttpSessionStateBase session, BulkChangeCreateModel bulkChange, int serviceProviderId)
        {
            var iccids = bulkChange.Devices.Where(iccid => !string.IsNullOrWhiteSpace(iccid)).ToList();
            var devicesByICCID = GetDevicesByICCID(awxDb, serviceProviderId, iccids);
            var createdBy = SessionHelper.GetAuditByName(session);
            var deviceChanges = new List<M2M_DeviceChange>();
            var changeRequest = new DeviceChangeRequest(JsonConvert.SerializeObject(bulkChange.Username,
                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }), DateTime.UtcNow, createdBy);

            foreach (var iccid in iccids)
            {
                if (!devicesByICCID.ContainsKey(iccid))
                {
                    deviceChanges.Add(CreateDeviceChangeError(iccid, string.Format(CommonStrings.M2MDeviceNotExistError, iccid), createdBy));
                }
                else
                {
                    var device = devicesByICCID[iccid];
                    deviceChanges.Add(new M2M_DeviceChange(changeRequest, device.id, iccid));
                }
            }

            return deviceChanges;
        }

        public static IEnumerable<M2M_DeviceChange> BuildStatusUpdateChangeDetailsJasper(AltaWorxCentral_Entities awxDb, HttpSessionStateBase session, int serviceProviderId, ICollection<string> iccids, BulkChangeStatusUpdate statusUpdate, BulkChangeAssociateCustomer revService, IntegrationType integrationType)
        {
            var devicesByICCID = GetDevicesByICCID(awxDb, serviceProviderId, iccids);
            var targetStatus = statusUpdate.TargetStatus.Trim();
            var targetStatusId = awxDb.DeviceStatus.First(x =>
                x.IsActive && !x.IsDeleted
                && (x.Status == targetStatus || x.DisplayName == targetStatus)
                && x.IntegrationId == (int)integrationType).id;
            var createdBy = SessionHelper.GetAuditByName(session);

            var deviceChanges = new List<M2M_DeviceChange>();
            DeviceChangeRequest changeRequest = null;
            // Not creating rev service
            if (revService == null)
            {
                changeRequest = BuildStatusUpdateChangeRequest(statusUpdate, targetStatus, targetStatusId, createdBy);
            }
            foreach (var iccid in iccids)
            {
                if (!devicesByICCID.ContainsKey(iccid))
                {
                    deviceChanges.Add(CreateDeviceChangeError(iccid, "Invalid ICCID", createdBy));
                }
                else
                {
                    var device = devicesByICCID[iccid];

                    // Build the change request with create rev service info
                    if (revService != null)
                    {
                        revService.Number = device.MSISDN;
                        revService.DeviceId = device.id;
                        changeRequest = BuildStatusUpdateChangeRequest(statusUpdate, targetStatus, targetStatusId, createdBy, revService);
                    }

                    if (changeRequest == null)
                    {
                        deviceChanges.Add(CreateDeviceChangeError(iccid, string.Format(CommonStrings.ErrorCreatingDeviceChangeRequest, iccid), createdBy));
                    }
                    else
                    {
                        deviceChanges.Add(new M2M_DeviceChange(changeRequest, device.id, iccid));
                    }
                }
            }

            return deviceChanges;
        }

        private static DeviceChangeRequest BuildStatusUpdateChangeRequest(BulkChangeStatusUpdate statusUpdate, string targetStatus, int targetStatusId, string createdBy, BulkChangeAssociateCustomer revService = null)
        {
            return new DeviceChangeRequest(BuildStatusUpdateRequestJasper(targetStatus, targetStatusId, statusUpdate.JasperStatusUpdate ?? new BulkChangeStatusUpdateJasper(), revService), DateTime.UtcNow, createdBy);
        }

        public static IEnumerable<M2M_DeviceChange> BuildStatusUpdateChangeDetailsThingSpace(AltaWorxCentral_Entities awxDb, HttpSessionStateBase session, int serviceProviderId, ICollection<string> identifiers, BulkChangeStatusUpdate statusUpdate,
            BulkChangeAssociateCustomer revService, ThingSpacePPU thingSpacePPU)
        {
            var devicesByICCID = GetDevicesByICCID(awxDb, serviceProviderId, identifiers);
            var targetStatus = statusUpdate.TargetStatus;
            var postApiStatusId = awxDb.DeviceStatus.First(x =>
                x.IsActive && !x.IsDeleted && x.Status == targetStatus && x.IntegrationId == (int)IntegrationType.ThingSpace).id;
            var createdBy = SessionHelper.GetAuditByName(session);
            var deviceChanges = new List<M2M_DeviceChange>();
            var deviceRepo = new DeviceRepository(awxDb);

            var iccids = identifiers.Select(x => Regex.Split(x, @"[^\d]+")[0]).ToList();
            var imeis = identifiers.Where(x => Regex.Split(x, @"[^\d]+").Length > 1).Select(x => Regex.Split(x, @"[^\d]+")[1]).ToList();
            var devicesSelect = deviceRepo.GetDeviceByICCIDOrIMEI(iccids, imeis, serviceProviderId);

            foreach (var identifier in identifiers)
            {
                if (targetStatus == PENDING_ACTIVE || targetStatus == ACTIVE)
                {
                    // PORT-312: if pending actiavte with ICCID & IMEI and Rev AccountNumber -> Pending activate -> Active device
                    var ids = Regex.Split(identifier, @"[^\d]+");
                    if (targetStatus == ACTIVE && string.IsNullOrWhiteSpace(statusUpdate.ThingSpaceStatusUpdate.RatePlanCode))
                    {
                        deviceChanges.Add(CreateDeviceChangeError(identifier, $"Rate Plan Code is required", createdBy));
                    }
                    if (ids.Length < 1)
                    {
                        deviceChanges.Add(CreateDeviceChangeError(identifier, $"Invalid input line: {identifier}", createdBy));
                    }
                    else if (ids.Length < 2)
                    {
                        var device = devicesSelect.FirstOrDefault(d => d.ICCID == ids[0]);

                        if (device != null && !string.IsNullOrWhiteSpace(device.IMEI))
                        {
                            var deviceChange = CreateThingSpaceDeviceChange(statusUpdate.ThingSpaceStatusUpdate, revService, targetStatus, postApiStatusId, createdBy, device.ICCID,
                                device.IMEI, device.id, thingSpacePPU: thingSpacePPU, isIgnoreCurrentStatus: true);
                            deviceChanges.Add(deviceChange);
                        }
                        else
                        {
                            deviceChanges.Add(CreateDeviceChangeError(identifier, $"ICCID: {ids[0]} does not pending activate.", createdBy));
                        }
                    }
                    else
                    {
                        var iccid = ids[0];
                        var imei = ids[1];
                        var isIgnoreCurrentStatus = false;
                        var isError = false;
                        var checkIMEI = devicesSelect.Where(d => d.IMEI == imei && d.Status.ToLower() != DeviceStatusConstant.ThingSpace_Unknown && d.Status.ToLower() != DeviceStatusConstant.ThingSpace_Inventory).FirstOrDefault();
                        var checkICCID = devicesSelect.Where(d => d.ICCID == iccid && d.Status.ToLower() != DeviceStatusConstant.ThingSpace_Unknown && d.Status.ToLower() != DeviceStatusConstant.ThingSpace_Inventory).FirstOrDefault();

                        if (checkIMEI != null)
                        {
                            if (!checkIMEI.Status.ToLower().Contains(DeviceStatusConstant.ThingSpace_Active) && checkIMEI.ICCID.Contains(iccid))
                                isIgnoreCurrentStatus = true;
                            else
                            {
                                deviceChanges.Add(CreateDeviceChangeError(identifier, $"IMEI: {imei} already exists.", createdBy));
                                isError = true;
                            }
                        }
                        if (checkICCID != null)
                        {
                            if (!checkICCID.Status.ToLower().Contains(DeviceStatusConstant.ThingSpace_Active) && checkICCID.IMEI.Contains(imei))
                                isIgnoreCurrentStatus = true;
                            else
                            {
                                deviceChanges.Add(CreateDeviceChangeError(identifier, $"ICCID: {iccid} already exists.", createdBy));
                                isError = true;
                            }
                        }

                        if (!isError)
                        {
                            var device = devicesSelect.FirstOrDefault(d => d.ICCID == ids[0]);
                            var deviceChange = CreateThingSpaceDeviceChange(statusUpdate.ThingSpaceStatusUpdate, revService, targetStatus, postApiStatusId, createdBy, iccid, imei, device?.id, thingSpacePPU: thingSpacePPU, isIgnoreCurrentStatus: isIgnoreCurrentStatus);
                            deviceChanges.Add(deviceChange);
                        }
                    }
                }
                else if (targetStatus == DeviceStatusConstant.ThingSpace_Inventory)
                {
                    if (devicesByICCID.ContainsKey(identifier))
                    {
                        deviceChanges.Add(CreateDeviceChangeError(identifier, "Invalid ICCID", createdBy));
                    }
                    else
                    {
                        var device = devicesSelect.FirstOrDefault(d => d.ICCID == identifier);
                        var deviceChange = CreateThingSpaceDeviceChange(statusUpdate.ThingSpaceStatusUpdate, revService, targetStatus, postApiStatusId, createdBy, identifier, string.Empty, device != null ? (int?)device.id : null, thingSpacePPU: thingSpacePPU);
                        deviceChanges.Add(deviceChange);
                    }
                }
                else if (!devicesByICCID.ContainsKey(identifier))
                {
                    deviceChanges.Add(CreateDeviceChangeError(identifier, "Invalid ICCID", createdBy));
                }
                else
                {
                    var device = devicesByICCID[identifier];
                    if (revService != null)
                    {
                        revService.Number = device.MSISDN;
                        revService.DeviceId = device.id;
                    }

                    var deviceChange = CreateThingSpaceDeviceChange(statusUpdate.ThingSpaceStatusUpdate, revService, targetStatus, postApiStatusId, createdBy, device.ICCID, device.IMEI, device.id, thingSpacePPU: thingSpacePPU);
                    deviceChanges.Add(deviceChange);
                }
            }

            return deviceChanges;
        }

        private static M2M_DeviceChange CreateThingSpaceDeviceChange(BulkChangeStatusUpdateThingSpace statusUpdate, BulkChangeAssociateCustomer revService, string targetStatus, int postApiStatusId,
            string createdBy, string iccid, string imei, int? deviceId = null, ThingSpacePPU thingSpacePPU = null, bool isIgnoreCurrentStatus = false)
        {
            if (statusUpdate == null)
            {
                statusUpdate = new BulkChangeStatusUpdateThingSpace();
            }

            var changeRequest = BuildStatusUpdateRequestThingSpace(iccid, imei, targetStatus, postApiStatusId, statusUpdate, revService, thingSpacePPU, isIgnoreCurrentStatus);
            if (string.IsNullOrWhiteSpace(changeRequest))
            {
                return CreateDeviceChangeError(iccid, string.Format(CommonStrings.ErrorCreatingDeviceChangeRequest, iccid), createdBy);
            }
            else
            {
                return new M2M_DeviceChange(changeRequest, deviceId, iccid, createdBy);
            }
        }

        private static ConcurrentDictionary<string, Device> GetDevicesByICCID(AltaWorxCentral_Entities awxDb, int serviceProviderId, ICollection<string> iccids)
        {
            var devicesByICCID = new ConcurrentDictionary<string, Device>(awxDb.Devices
                .Include(device => device.ServiceProvider).AsNoTracking()
                .Where(device => device.IsActive
                                 && !device.IsDeleted
                                 && serviceProviderId == device.ServiceProviderId
                                 && iccids.Contains(device.ICCID))
                .AsEnumerable()
                .ToDictionary(device => device.ICCID, device => device));
            return devicesByICCID;
        }

        private static List<string> CheckDevicesArchived(AltaWorxCentral_Entities awxDb, int serviceProviderId, ICollection<string> iccids)
        {
            var archivedICCIDs = awxDb.Devices
                .Include(device => device.ServiceProvider).AsNoTracking()
                .Where(device => !device.IsActive
                                 && device.IsDeleted
                                 && serviceProviderId == device.ServiceProviderId
                                 && iccids.Contains(device.ICCID))
                .GroupBy(device => device.ICCID)
                .Select(device => device.FirstOrDefault().ICCID)
                .ToList();
            return archivedICCIDs;
        }

        private static ConcurrentDictionary<int, ConcurrentBag<RevService>> GetActiveRevServicesByDeviceId(AltaWorxCentral_Entities awxDb, ICollection<int> deviceIds, int tenantId)
        {
            // pass 0 for intAuthId b/c it doesn't matter for the call used
            var repo = new RevServiceRepository(awxDb, 0);
            var revServicesDictionary = repo.GetActiveRevServicesForDevices(deviceIds, PORTAL_TYPE, tenantId);
            var revServicesByDeviceId = new ConcurrentDictionary<int, ConcurrentBag<RevService>>();
            foreach (var revServiceEntry in revServicesDictionary)
            {
                revServicesByDeviceId.GetOrAdd(revServiceEntry.Key, s => new ConcurrentBag<RevService>()).Add(revServiceEntry.Value);
            }

            return revServicesByDeviceId;
        }

        public static M2M_DeviceChange CreateDeviceChangeError(string iccid, string errorMessage, string createdBy)
        {
            return new M2M_DeviceChange
            {
                Status = BulkChangeStatus.ERROR,
                ICCID = iccid,
                StatusDetails = errorMessage,
                IsProcessed = true,
                ProcessedBy = createdBy,
                ProcessedDate = DateTime.UtcNow,
                HasErrors = true,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = createdBy,
                IsActive = true,
                IsDeleted = false
            };
        }

        private static string BuildStatusUpdateRequestJasper(string targetStatus, int postUpdateStatusId, BulkChangeStatusUpdateJasper statusUpdate, BulkChangeAssociateCustomer revService)
        {
            if (string.IsNullOrWhiteSpace(targetStatus))
            {
                return null;
            }

            var request = new BulkChangeStatusUpdateRequest<BulkChangeStatusUpdateJasper>
            {
                UpdateStatus = targetStatus,
                PostUpdateStatusId = postUpdateStatusId,
                Request = statusUpdate,
            };
            if (revService != null)
                request.RevService = revService;

            return JsonConvert.SerializeObject(request, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        }

        private static string BuildStatusUpdateRequestThingSpace(string iccid, string imei, string targetStatus, int postUpdateStatusId,
            BulkChangeStatusUpdateThingSpace statusUpdate, BulkChangeAssociateCustomer revService, ThingSpacePPU thingSpacePPU, bool isIgnoreCurrentStatus = false)
        {
            if (string.IsNullOrWhiteSpace(targetStatus) || statusUpdate == null)
            {
                return null;
            }

            statusUpdate.ICCID = iccid;
            statusUpdate.IMEI = imei;
            statusUpdate.thingSpacePPU = thingSpacePPU;

            var request = new BulkChangeStatusUpdateRequest<BulkChangeStatusUpdateThingSpace>
            {
                IsIgnoreCurrentStatus = isIgnoreCurrentStatus,
                UpdateStatus = targetStatus,
                PostUpdateStatusId = postUpdateStatusId,
                Request = statusUpdate,
                RevService = revService,
            };
            return JsonConvert.SerializeObject(request, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        }

        private bool UpdateM2MDeviceHistory(Device device, Device_Tenant deviceTenant, DateTime? effectiveDate)
        {
            var m2mDeviceHistoryRepository = new M2MDeviceHistoryRepository(altaWrxDb);
            var billingPeriodRepo = new Repositories.BillingPeriod.BillingPeriodRepository(altaWrxDb);
            var billingPeriodIds = new List<int>();
            billingPeriodIds.Add((int)device.BillingPeriodId);
            if (effectiveDate != null)
            {
                billingPeriodIds = billingPeriodRepo.GetBillingPeriodIdsByServiceProviderAndDate(device.ServiceProviderId, (DateTime)effectiveDate);
            }
            var isUpdateM2MDeviceHistorySuccess = true;
            foreach (var billingPeriodId in billingPeriodIds)
            {
                DeviceHistory m2mDeviceHistory = m2mDeviceHistoryRepository.GetByDeviceTenantAndBillingperiod(deviceTenant.id, billingPeriodId);
                if (m2mDeviceHistory != null && m2mDeviceHistory.ID > 0)
                {
                    var updatedDeviceHistory = device.ToUpdatedM2MDeviceHistory(deviceTenant, user.Username, effectiveDate, m2mDeviceHistory);
                    if (m2mDeviceHistoryRepository.UpdateM2MDeviceHistory(m2mDeviceHistory, updatedDeviceHistory))
                    {
                        Log.Info(string.Format(LogCommonStrings.UPDATE_DEVICE_HISTORY_SUCCESS, updatedDeviceHistory.ToString()));
                    }
                    else
                    {
                        Log.Error(string.Format(LogCommonStrings.ERROR_WHILE_UPDATE_DEVICE_HISTORY, updatedDeviceHistory.ToString()));
                        isUpdateM2MDeviceHistorySuccess = false;
                    }
                }
            }
            return isUpdateM2MDeviceHistorySuccess;
        }
        #endregion

        #region internal method
        internal static IEnumerable<M2M_DeviceChange> BuildAssociateCustomerDeviceChanges(AltaWorxCentral_Entities awxDb,
            HttpSessionStateBase session, PermissionManager permissionManager, BulkChangeAssociateCustomerModel model, List<vwM2MDeviceInventory> devices, List<Device_Tenant> deviceTenants, List<Site> sites, List<string> activateStatuses, List<RevCustomer> revCustomers, bool useCarrierActivation = false, RevCustomer rev = null)
        {
            var tenantTimeZone = TimeZoneHelper.GetTimeZoneInfo(permissionManager.AltaworxCentralConnectionString);
            var createdBy = SessionHelper.GetAuditByName(session);
            var activateDevices = devices.Where(x => activateStatuses.Contains(x.StatusCode));
            var archivedICCIDs = devices.Where(x => !x.IsActive && x.IsDeleted);
            var revCustomer = rev;
            if (rev == null)
            {
                var revCustomerRepository = new RevCustomerRepository(awxDb, permissionManager.Tenant.id);
                revCustomer = revCustomers.FirstOrDefault(x => x.RevCustomerId.Equals(model.RevCustomerId));
            }

            var integrationAuthenticationId = revCustomer.IntegrationAuthenticationId;
            var revServiceRepostiory = new RevServiceRepository(awxDb, integrationAuthenticationId.GetValueOrDefault());
            var revServiceProductRepostiory = new RevServiceProductRepository(awxDb);
            var deviceChanges = new List<M2M_DeviceChange>();
            var carrierActivationHelper = new CarrierActivationHelper(awxDb);
            var activatedHistories = awxDb.DeviceStatusHistories.AsNoTracking().Where(x => model.Devices.Contains(x.ICCID) && activateStatuses.Contains(x.CurrentStatus)).ToList();
            var jasperDevices = ListHelper.GetJasperDeviceByICCIDs(permissionManager, model.Devices.ToList());
            var revServiceProducts = new List<usp_Get_Rev_Service_Product_By_Rev_Customer_Id_Result>();
            if (model.CreateRevService)
            {
                revServiceProducts = awxDb.usp_Get_Rev_Service_Product_By_Rev_Customer_Id(revCustomer.RevCustomerId, revCustomer.IntegrationAuthenticationId).ToList();
            }

            foreach (var iccid in model.Devices)
            {
                if (!string.IsNullOrEmpty(iccid))
                {
                    var device = devices.FirstOrDefault(x => x.ICCID == iccid);
                    var jasperDeviceId = jasperDevices.FirstOrDefault(x => x.ICCID == iccid)?.Id;
                    if (device == null)
                    {
                        if (archivedICCIDs.FirstOrDefault(x => x.ICCID == iccid) != null)
                        {
                            deviceChanges.Add(CreateDeviceChangeError(iccid, Resources.CommonStrings.M2MDeviceIsArchivedError, createdBy));
                        }
                        else
                        {
                            deviceChanges.Add(CreateDeviceChangeError(iccid, string.Format(CommonStrings.M2MDeviceNotExistError, iccid), createdBy));
                        }
                    }
                    else
                    {
                        if (useCarrierActivation)
                        {
                            var activatedDate = carrierActivationHelper.M2MActivationDateFromNumber(activatedHistories, devices, device.ICCID, tenantTimeZone);
                            model.ActivatedDate = activatedDate;
                            model.EffectiveDate = activatedDate;
                        }

                        var changeRequestString = CreateAssociateCustomerChangeRequest(sites, model, device, integrationAuthenticationId.GetValueOrDefault(), revCustomer, jasperDeviceId.ToString());

                        if (model.CreateRevService && CheckRevServiceStatusByRevCustomerId(deviceTenants, revServiceProducts, device.id, permissionManager.Tenant.id, revCustomer.RevCustomerId))
                        {
                            deviceChanges.Add(CreateDeviceChangeError(iccid, $"Active service line. New Service line not created.", createdBy));
                        }
                        else if (!model.CreateRevService && !string.IsNullOrEmpty(model.RevCustomerId))
                        {
                            var deviceTenant = deviceTenants.FirstOrDefault(x => x.TenantId == permissionManager.Tenant.id);
                            if (deviceTenant != null && deviceTenant.AccountNumber != model.RevCustomerId && CheckRevServiceStatus(deviceTenants, device.id, permissionManager.Tenant.id))
                            {
                                deviceChanges.Add(CreateDeviceChangeError(iccid, $"Active service line. Cannot change customers.", createdBy));
                            }
                            else
                            {
                                deviceChanges.Add(new M2M_DeviceChange(changeRequestString, device.id, iccid, createdBy, BulkChangeStatus.PROCESSING));
                            }
                        }
                        else
                        {
                            deviceChanges.Add(new M2M_DeviceChange(changeRequestString, device.id, iccid, createdBy, BulkChangeStatus.PROCESSING));
                        }
                    }
                }
            }

            return deviceChanges;
        }

        internal static IEnumerable<M2M_DeviceChange> BuildAssociateAmopCustomerDeviceChanges(AltaWorxCentral_Entities awxDb,
           HttpSessionStateBase session, PermissionManager permissionManager, BulkChangeAssociateNonRevCustomerModel model)
        {
            var createdBy = SessionHelper.GetAuditByName(session);
            var devicesByICCID = GetDevicesByICCID(awxDb, model.ServiceProviderId, model.Devices);
            var archivedICCIDs = CheckDevicesArchived(awxDb, model.ServiceProviderId, model.Devices);

            var deviceChanges = new List<M2M_DeviceChange>();
            foreach (var iccid in model.Devices)
            {
                if (!string.IsNullOrEmpty(iccid))
                {
                    if (!devicesByICCID.ContainsKey(iccid))
                    {
                        if (archivedICCIDs.Contains(iccid))
                        {
                            deviceChanges.Add(CreateDeviceChangeError(iccid, Resources.CommonStrings.M2MDeviceIsArchivedError, createdBy));
                        }
                        else
                        {
                            deviceChanges.Add(CreateDeviceChangeError(iccid, string.Format(CommonStrings.M2MDeviceNotExistError, iccid), createdBy));
                        }
                    }
                    else
                    {
                        var device = devicesByICCID[iccid];
                        deviceChanges.Add(new M2M_DeviceChange(CreateAssociateAmopCustomerChangeRequest(awxDb, model, permissionManager.Tenant.id), device.id, iccid, createdBy));
                    }
                }
            }

            return deviceChanges;
        }

        internal static IEnumerable<M2M_DeviceChange> BuildChangeDetails(AltaWorxCentral_Entities awxDb, HttpSessionStateBase session, PermissionManager permissionManager, BulkChangeCreateModel bulkChange, int serviceProviderId, DeviceChangeType changeType)
        {
            switch (changeType)
            {
                case DeviceChangeType.CustomerRatePlanChange:
                case DeviceChangeType.CarrierRatePlanChange:
                    return BuildCustomerRatePlanChangeDetails(awxDb, session, bulkChange, serviceProviderId, changeType);
                case DeviceChangeType.StatusUpdate:
                    return BuildStatusUpdateChangeDetails(awxDb, session, permissionManager, bulkChange, serviceProviderId);
                case DeviceChangeType.Archival:
                    return BuildArchivalChangeDetails(awxDb, session, bulkChange, serviceProviderId, permissionManager.Tenant.id);
                case DeviceChangeType.EditUsername:
                    return BuildUsernameChangeDetails(awxDb, session, bulkChange, serviceProviderId);
                default:
                    throw new NotImplementedException($"Unsupported device change type: {changeType}");
            }
        }


        [HttpPost]
        public async Task<ActionResult> DeleteThingSpaceDevices(long id)
        {
            if (!permissionManager.UserCanAccessPortalTypeModule(Session, PORTAL_TYPE))
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
            }

            var changeRepository = new DeviceBulkChangeRepository(altaWrxDb, permissionManager);
            var bulkChange = changeRepository.GetBulkChange(id);

            if (bulkChange == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound);
            }

            try
            {
                var deviceChangeRepository = new M2MDeviceChangeRepository(altaWrxDb, permissionManager);
                var changes = deviceChangeRepository.GetChanges(id);
                var thingSpaceDeleteDeviceRequest = new ThingSpaceDeleteDeviceRequest();
                thingSpaceDeleteDeviceRequest.devicesToDelete = new List<DeleteDevicesRequest>();
                foreach (var change in changes)
                {
                    if (change.StatusDetails != null)
                    {
                        Match deviceExistsMatch = Regex.Match(change.StatusDetails, CommonConstants.DEVICE_EXISTS_PATTERN);
                        if (deviceExistsMatch.Success && bulkChange.Status == BulkChangeStatus.PROCESSED &&
                            bulkChange.ErrorCount.GetValueOrDefault() > 0 &&
                            bulkChange.IntegrationId == (int)IntegrationType.ThingSpace)
                        {
                            var kind = CommonConstants.ICCID;
                            Match match = Regex.Match(change.StatusDetails, CommonConstants.IMEI_VALUE_PATTERN);
                            if (match.Success)
                            {
                                kind = CommonConstants.IMEI;
                            }
                            var deviceToDelete = new DeleteDevicesRequest()
                            {
                                deviceIds = new List<DeviceId>()
                                {
                                    new DeviceId()
                                    {
                                        id = deviceExistsMatch.Groups[1].Value,
                                        kind = kind
                                    }
                                }
                            };

                            thingSpaceDeleteDeviceRequest.devicesToDelete.Add(deviceToDelete);
                        }
                    }
                }
                var thingSpaceAuthentication = altaWrxDb.usp_ThingSpace_Get_AuthenticationByProviderId(bulkChange.ServiceProviderId).FirstOrDefault()?.ToThingSpaceAuthentication();
                var accessToken = await ThingSpaceCommon.GetAccessToken(thingSpaceAuthentication);
                if (accessToken != null)
                {
                    var sessionToken = await ThingSpaceCommon.GetSessionToken(thingSpaceAuthentication, accessToken);
                    var deleteThingSpaceDevice = new List<DeleteThingSpaceDeviceResponse>();
                    // Create a batch of devices to delete based on the batch size,
                    // as the ThingSpace API only allows deletion of a list of up to 100 devices per request
                    for (int i = 0; i < thingSpaceDeleteDeviceRequest.devicesToDelete.Count; i += CommonConstants.THINGSPACE_DELETE_DEVICE_BATCH_SIZE)
                    {
                        List<DeleteDevicesRequest> deleteDevicesRequestBatch = thingSpaceDeleteDeviceRequest.devicesToDelete
                            .Skip(i)
                            .Take(CommonConstants.THINGSPACE_DELETE_DEVICE_BATCH_SIZE)
                            .ToList();

                        var thingSpaceDeleteRequest = new ThingSpaceDeleteDeviceRequest();
                        thingSpaceDeleteRequest.devicesToDelete = deleteDevicesRequestBatch;
                        var deleteDeviceRequest = JsonConvert.SerializeObject(thingSpaceDeleteRequest, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                        // Call the Carrier API to delete ThingSpace devices
                        deleteThingSpaceDevice = await ThingSpaceCommon.DeleteThingSpaceDeviceAsync(deleteDeviceRequest, thingSpaceAuthentication, accessToken, sessionToken, Log.KeysysLogger);
                        if (deleteThingSpaceDevice != null)
                        {
                            var iccids = deleteThingSpaceDevice.Where(x => x.deviceIds.kind.ToLower() == CommonConstants.ICCID).Select(x => x.deviceIds.id).ToList();
                            var imeis = deleteThingSpaceDevice.Where(x => x.deviceIds.kind.ToLower() == CommonConstants.IMEI).Select(x => x.deviceIds.id).ToList();
                            var failedICCIDs = deleteThingSpaceDevice.Where(x => x.deviceIds.kind.ToLower() == CommonConstants.ICCID && x.status.ToLower() == CommonConstants.FAILED).Select(x => x.deviceIds.id).ToList();
                            var failedIMEIs = deleteThingSpaceDevice.Where(x => x.deviceIds.kind.ToLower() == CommonConstants.IMEI && x.status.ToLower() == CommonConstants.FAILED).Select(x => x.deviceIds.id).ToList();
                            var deviceRepository = new DeviceRepository(altaWrxDb);
                            // Retrieve devices from the database based on ICCIDs and IMEIs
                            var devices = deviceRepository.GetDeviceByICCIDOrIMEI(iccids, imeis, bulkChange.ServiceProviderId).Distinct();
                            var failedDeviceIds = devices.Where(device => failedICCIDs.Any(iccid => iccid == device.ICCID) || failedIMEIs.Any(imei => imei == device.IMEI)).Select(x => x.id).Distinct();
                            var successDevices = devices.Where(x => !failedDeviceIds.Contains(x.id)).Distinct();
                            // Update the database records for devices that were successfully deleted
                            foreach (var device in successDevices)
                            {
                                device.IsActive = false;
                                device.IsDeleted = true;
                                device.DeletedDate = DateTime.UtcNow;
                                device.DeletedBy = SessionHelper.GetAuditByName(Session);
                            }
                        }
                    }
                    await altaWrxDb.SaveChangesAsync();

                    if (!deleteThingSpaceDevice.Any(x => x.status.ToLower() == CommonConstants.FAILED))
                    {
                        return Json(new { ServiceProviderId = bulkChange.ServiceProviderId, Message = LogCommonStrings.THINGSPACE_DEVICES_DELETED_SUCCESSFULLY });
                    }
                    else
                    {
                        return Json(new { ServiceProviderId = bulkChange.ServiceProviderId, Message = LogCommonStrings.DELETE_THINGSPACE_DEVICES_HAVE_BEEN_COMPLETED });
                    }
                }
                else
                {
                    return Json(new { ServiceProviderId = bulkChange.ServiceProviderId, Message = LogCommonStrings.ERROR_WHILE_DELETING_THINGSPACE_DEVICES_ACCESS_TOKEN_NOT_FOUND });
                }
            }
            catch (Exception ex)
            {
                return Json(new { ServiceProviderId = bulkChange.ServiceProviderId, Message = string.Format(LogCommonStrings.ERROR_WHILE_DELETING_THINGSPACE_DEVICES, ex.Message) });
            }
        }
        #endregion
    }
}
