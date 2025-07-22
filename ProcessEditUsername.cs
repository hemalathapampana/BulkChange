using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Altaworx.AWS.Core;
using Altaworx.AWS.Core.Helpers.Constants;
using Altaworx.AWS.Core.Models;
using AltaworxDeviceBulkChange.Models;
using AltaworxDeviceBulkChange.Repositories;
using Amazon;
using Amazon.Runtime.Internal.Util;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Amop.Core.Constants;
using Amop.Core.Helpers;
using Amop.Core.Models;
using Amop.Core.Models.DeviceBulkChange;
using Amop.Core.Models.Revio;
using Amop.Core.Repositories;
using Amop.Core.Repositories.Environment;
using Amop.Core.Repositories.Revio;
using Amop.Core.Repositories.Tenant;
using Amop.Core.Resilience;
using Amop.Core.Services.Base64Service;
using Amop.Core.Services.Http;
using Amop.Core.Services.Jasper;
using Amop.Core.Services.Revio;
using Azure;
using HtmlAgilityPack;
using Microsoft.Data.SqlClient;
using MimeKit;
using MimeKit.Utils;
using Newtonsoft.Json;
using static AltaworxDeviceBulkChange.Common;

namespace AltaworxDeviceBulkChange
{
    public partial class Function
    {
        public IEnvironmentRepository EnvironmentRepo = new EnvironmentRepository();

        private async Task<bool> ProcessEditUsernameAsync(KeySysLambdaContext context, DeviceBulkChangeLogRepository logRepo, BulkChangeRepository bulkRepo, BulkChange bulkChange)
        {
            //var baseMutliTenantConnectionString = EnvironmentRepo.GetEnvironmentVariable(context.Context, "BaseMultiTenantConnectionString");
            //TenantRepository tenantRepository = new TenantRepository(baseMutliTenantConnectionString);

            //update
            string baseMutliTenantConnectionString;

            try
            {
                baseMutliTenantConnectionString = EnvironmentRepo.GetEnvironmentVariable(context.Context, "BaseMultiTenantConnectionString");

                if (string.IsNullOrWhiteSpace(baseMutliTenantConnectionString))
                    throw new Exception("Missing or empty 'BaseMultiTenantConnectionString' environment variable.");
            }
            catch (Exception ex)
            {
                context.logger.LogError(LogTypeConstant.Error, $"Failed to retrieve connection string for BulkChangeId {bulkChange.Id}: {ex.Message}");
                await bulkRepo.MarkBulkChangeStatusAsync(context, bulkChange.Id, "ERROR");
                return false;
            }

            //update
            TenantRepository tenantRepository;
            try
            {
                tenantRepository = new TenantRepository(baseMutliTenantConnectionString);
            }
            catch (Exception ex)
            {
                context.logger.LogError(LogTypeConstant.Error, $"Failed to initialize TenantRepository for BulkChangeId {bulkChange.Id}: {ex.Message}");
                await bulkRepo.MarkBulkChangeStatusAsync(context, bulkChange.Id, "ERROR");
                return false;
            }

            LogInfo(context, LogTypeConstant.Sub, $"({bulkChange.Id})");
            var changes = GetDeviceChanges(context, bulkChange.Id, bulkChange.PortalTypeId, PageSize);
            LogInfo(context, LogTypeConstant.Info, $"There are {changes.Count} devices that need to be updated.");
            var pushUsernameUpdateType = tenantRepository.GetCustomObjectById(ParameterizedLog(context), bulkChange.TenantId, CommonConstants.OBJECT_DESCRIPTION_ID);
            var result = false;
            BulkChangeEditUsername usernameUpdateRequest;
            if (changes != null && changes.Count > 0)
            {
                usernameUpdateRequest = JsonConvert.DeserializeObject<BulkChangeEditUsername>(changes.FirstOrDefault()?.ChangeRequest);
            }
            else
            {
                context.logger.LogInfo(LogTypeConstant.Warning, $"No unprocessed changes found for status change {bulkChange.Id}");
                return true;
            }

            var httpRetryPolicy = GetHttpRetryPolicy(context);
            var httpClientFactory = new KeysysHttpClientFactory();
            var processedBy = context.Context.FunctionName;
            switch (bulkChange.IntegrationId)
            {
                case (int)IntegrationType.Jasper:
                case (int)IntegrationType.POD19:
                case (int)IntegrationType.TMobileJasper:
                case (int)IntegrationType.Rogers:
                    result = await ProcessEditUsernameJasperAsync(context, logRepo, bulkChange, changes, usernameUpdateRequest, processedBy, httpClientFactory);
                    break;
                case (int)IntegrationType.ThingSpace:
                    // TODO: Process Edit Username ThingSpace
                    break;
                case (int)IntegrationType.Telegence:
                    result = await ProcessEditUsernameTelegenceAsync(context, logRepo, bulkChange, changes, usernameUpdateRequest, processedBy, httpClientFactory, pushUsernameUpdateType);
                    break;
                default:
                    throw new Exception($"Error Processing Bulk Change {bulkChange.Id}: Integration Type {bulkChange.IntegrationId} is unsupported.");
            }

            return result;
        }

        private async Task<bool> ProcessEditUsernameJasperAsync(KeySysLambdaContext context, DeviceBulkChangeLogRepository logRepo, BulkChange bulkChange, IEnumerable<BulkChangeDetailRecord> changes, BulkChangeEditUsername usernameUpdateRequest, string processedBy, IKeysysHttpClientFactory httpClientFactory)
        {
            LogInfo(context, LogTypeConstant.Sub, $"({bulkChange.Id})");
            var jasperAuthentication = JasperCommon.GetJasperAuthenticationInformation(context.CentralDbConnectionString, bulkChange.ServiceProviderId);
            var contactName = usernameUpdateRequest.ContactName;
            var costCenter1 = usernameUpdateRequest.CostCenter1;
            var costCenter2 = usernameUpdateRequest.CostCenter2;
            var costCenter3 = usernameUpdateRequest.CostCenter3;

            if (!jasperAuthentication.WriteIsEnabled)
            {
                string message = Common.CommonString.WRITE_DISABLED_FOR_SERVICE_PROVIDER;
                LogInfo(context, LogTypeConstant.Exception, message);

                var change = changes.First();
                logRepo.AddM2MLogEntry(new CreateM2MDeviceBulkChangeLog()
                {
                    BulkChangeId = bulkChange.Id,
                    ErrorText = message,
                    HasErrors = true,
                    LogEntryDescription = Common.CommonString.UPDATE_USERNAME_OF_JASPER_DEVICES,
                    M2MDeviceChangeId = change.Id,
                    ProcessBy = processedBy,
                    ProcessedDate = DateTime.UtcNow,
                    ResponseStatus = BulkChangeStatus.ERROR,
                    RequestText = JsonConvert.SerializeObject(change),
                    ResponseText = message
                });
                await MarkProcessedForM2MDeviceChangeAsync(context, change.Id, false, message);
                return false;
            }

            var policyFactory = new PolicyFactory(context.logger);
            var sqlRetryPolicy = policyFactory.GetSqlRetryPolicy(CommonConstants.NUMBER_OF_RETRIES);
            var iccids = string.Join(",", changes.Select(x => x.ICCID));
            var tenantId = changes.FirstOrDefault()?.TenantId;
            var revServices = new List<RevServiceDetail>();
            sqlRetryPolicy.Execute(() =>
            {
                var parameters = new List<SqlParameter>()
                    {
                        new SqlParameter(CommonSQLParameterNames.ICCIDS, iccids),
                        new SqlParameter(CommonSQLParameterNames.SERVICE_PROVIDER_ID_PASCAL_CASE, bulkChange.ServiceProviderId),
                        new SqlParameter(CommonSQLParameterNames.TENANT_ID, tenantId),
                    };
                revServices = SqlQueryHelper.ExecuteStoredProcedureWithListResult((type, message) =>
                    context.logger.LogInfo(type, message),
                    context.CentralDbConnectionString,
                    Amop.Core.Constants.SQLConstant.StoredProcedureName.UPDATE_USERNAME_GET_M2M_REV_SERVICE,
                    (dataReader) => GetM2MRevServicesFromReader(dataReader),
                    parameters,
                    Amop.Core.Constants.SQLConstant.TimeoutSeconds);
            });

            var revIOAuthenticationRepository = new RevioAuthenticationRepository(context.CentralDbConnectionString, new Base64Service());
            var integrationAuthenticationId = revServices.FirstOrDefault()?.IntegrationAuthenticationId ?? 0;

            var revIOAuthentication = revIOAuthenticationRepository.GetRevioApiAuthentication(integrationAuthenticationId);
            var revApiClient = new RevioApiClient(new SingletonHttpClientFactory(), _httpRequestFactory, revIOAuthentication,
                context.IsProduction);

            foreach (var change in changes)
            {
                if (context.Context.RemainingTime.TotalSeconds < RemainingTimeCutoff)
                {
                    // processing should continue, we just need to requeue
                    return true;
                }

                ApiResponse apiResult;
                var responseText = "";
                var hasErrors = false;
                var logMessage = string.Empty;
                if (!string.IsNullOrWhiteSpace(usernameUpdateRequest.ContactName))
                {
                    LogInfo(context, LogTypeConstant.Info, Common.CommonString.UPDATE_USERNAME_OF_JASPER_DEVICES);
                    var jasperDeviceService = new JasperAPIService(jasperAuthentication, new Base64Service(), httpClientFactory);
                    var updateResult = await jasperDeviceService.UpdateUsernameJasperDeviceAsync(usernameUpdateRequest, JasperDeviceUsernameUpdatePath, change.ICCID, context.logger);
                    if (!updateResult.HasErrors)
                    {
                        if (bulkChange.IntegrationId == (int)IntegrationType.POD19)
                        {
                            // Call Jasper Audit to check the username really updated successfully.
                            var isEditSuccess = await jasperDeviceService.IsEditUsernamePOD19Success(JasperDeviceAuditTrailPath, change.ICCID, Common.CommonString.ERROR_MESSAGE, Common.CommonString.USERNAME_STRING);
                            if (!isEditSuccess)
                            {
                                var message = String.Format(Common.CommonString.UPDATE_USERNAME_FAILED, usernameUpdateRequest.ContactName);
                                LogInfo(context, LogTypeConstant.Info, message);
                                updateResult.HasErrors = true;
                                updateResult.ResponseObject = message;
                            }
                        }
                    }

                    apiResult = new ApiResponse() { IsSuccess = !updateResult.HasErrors, Response = JsonConvert.SerializeObject(updateResult.ResponseObject) };

                    responseText = JsonConvert.SerializeObject(apiResult);
                    var logEntryResponse = Common.CommonString.UPDATE_USERNAME_OF_JASPER_DEVICES;
                    AddBulkChangeLog(logRepo, bulkChange.Id, updateResult.HasErrors, updateResult.ResponseObject, change.Id, processedBy, change.ChangeRequest, responseText, logEntryResponse);
                    hasErrors = !apiResult.IsSuccess;
                    if (!apiResult.IsSuccess)
                    {
                        LogInfo(context, LogTypeConstant.Error, $"Response: {apiResult?.Response}");
                    }
                    else
                    {
                        LogInfo(context, LogTypeConstant.Info, $"Response: {apiResult?.Response}");
                    }
                }

                if (!string.IsNullOrWhiteSpace(usernameUpdateRequest.CostCenter1) || !string.IsNullOrWhiteSpace(usernameUpdateRequest.CostCenter2) || !string.IsNullOrWhiteSpace(usernameUpdateRequest.CostCenter3))
                {
                    LogInfo(context, LogTypeConstant.Info, Common.CommonString.UPDATE_USERNAME_AND_COST_CENTER_OF_DEVICES_REVIO_API);
                    var amopRevService = revServices.Where(x => x.ICCID == change.ICCID && x.TenantId == change.TenantId).OrderBy(x => x.ActivatedDate)?.FirstOrDefault();
                    if (revApiClient != null && amopRevService != null)
                    {
                        var revService = await LookupRevServiceAsync(amopRevService?.RevServiceId, revApiClient, context);
                        if (revService != null)
                        {
                            var costCenterIndex1 = GetRevFieldIndex(revService, CommonColumnNames.COST_CENTER_1);
                            var costCenterIndex2 = GetRevFieldIndex(revService, CommonColumnNames.COST_CENTER_2);
                            var costCenterIndex3 = GetRevFieldIndex(revService, CommonColumnNames.COST_CENTER_3);

                            Dictionary<int, string> fieldsToUpdate = new Dictionary<int, string>();

                            if (!string.IsNullOrWhiteSpace(costCenter1) && costCenterIndex1 >= 0)
                            {
                                fieldsToUpdate.Add(costCenterIndex1, costCenter1);
                            }
                            // For some cases those have the "Cost Center" field instead of "Cost Center 1" 
                            else if (!string.IsNullOrWhiteSpace(costCenter1) && costCenterIndex1 < 0)
                            {
                                var costCenterIndex = GetRevFieldIndex(revService, CommonColumnNames.COST_CENTER);
                                if (costCenterIndex >= 0)
                                {
                                    fieldsToUpdate.Add(costCenterIndex, costCenter1);
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(costCenter2) && costCenterIndex2 >= 0)
                            {
                                fieldsToUpdate.Add(costCenterIndex2, costCenter2);
                            }

                            if (!string.IsNullOrWhiteSpace(costCenter3) && costCenterIndex3 >= 0)
                            {
                                fieldsToUpdate.Add(costCenterIndex3, costCenter3);
                            }
                            if (fieldsToUpdate.Count > 0)
                            {
                                var response = await revApiClient.UpdateServiceCustomFieldAsync(amopRevService.RevServiceId, fieldsToUpdate, (message) => LogInfo(context, CommonConstants.SUB, message));
                                hasErrors = response.HasErrors;
                                responseText = JsonConvert.SerializeObject(response.ResponseObject);
                                logRepo.AddM2MLogEntry(new CreateM2MDeviceBulkChangeLog()
                                {
                                    BulkChangeId = bulkChange.Id,
                                    ErrorText = response.HasErrors ? responseText : null,
                                    HasErrors = response.HasErrors,
                                    LogEntryDescription = Common.CommonString.UPDATE_USERNAME_AND_COST_CENTER_OF_DEVICES_REVIO_API,
                                    M2MDeviceChangeId = change.Id,
                                    ProcessBy = processedBy,
                                    ProcessedDate = DateTime.UtcNow,
                                    RequestText = response.ActionText + Environment.NewLine + response.RequestObject,
                                    ResponseStatus = response.HasErrors ? BulkChangeStatus.ERROR : BulkChangeStatus.PROCESSED,
                                    ResponseText = responseText
                                });
                                LogInfo(context, hasErrors ? LogTypeConstant.Error : LogTypeConstant.Info, responseText);
                            }
                        }
                    }
                }

                if (!hasErrors)
                {
                    LogInfo(context, LogTypeConstant.Info, Common.CommonString.UPDATE_USERNAME_AND_COST_CENTER_OF_DEVICES);
                    (hasErrors, logMessage) = bulkChangeRepository.UpdateUsernameDeviceForAMOP(context, bulkChange.ServiceProviderId, change.ICCID, usernameUpdateRequest, bulkChange.PortalTypeId, bulkChange.IntegrationId, bulkChange.Id, bulkChange.TenantId);
                    logRepo.AddM2MLogEntry(new CreateM2MDeviceBulkChangeLog()
                    {
                        BulkChangeId = bulkChange.Id,
                        ErrorText = hasErrors ? logMessage : null,
                        HasErrors = hasErrors,
                        LogEntryDescription = Common.CommonString.UPDATE_USERNAME_AND_COST_CENTER_OF_DEVICES,
                        M2MDeviceChangeId = change.Id,
                        ProcessBy = processedBy,
                        ProcessedDate = DateTime.UtcNow,
                        RequestText = LogCommonStrings.UPDATING_USERNAME_AND_COST_CENTER,
                        ResponseStatus = hasErrors ? BulkChangeStatus.ERROR : BulkChangeStatus.PROCESSED,
                        ResponseText = logMessage
                    });

                    LogInfo(context, hasErrors ? LogTypeConstant.Error : LogTypeConstant.Info, logMessage);
                }
                else
                {
                    LogInfo(context, LogTypeConstant.Error, $"Response: {logMessage}");
                }

                await MarkProcessedForM2MDeviceChangeAsync(context, change.Id, !hasErrors, responseText);
            }

            return true;
        }

        private async Task<bool> ProcessEditUsernameTelegenceAsync(KeySysLambdaContext context, DeviceBulkChangeLogRepository logRepo, BulkChange bulkChange, IEnumerable<BulkChangeDetailRecord> changes, BulkChangeEditUsername usernameUpdateRequest, string processedBy, IKeysysHttpClientFactory httpClientFactory, string pushUsernameUpdateType)
        {
            LogInfo(context, LogTypeConstant.Sub, $"({bulkChange.Id})");
            var contactName = usernameUpdateRequest.ContactName;
            var costCenter1 = usernameUpdateRequest.CostCenter1;
            var costCenter2 = usernameUpdateRequest.CostCenter2;
            var costCenter3 = usernameUpdateRequest.CostCenter3;
            var serviceDescription = usernameUpdateRequest.ContactName;

            var policyFactory = new PolicyFactory(context.logger);
            var sqlRetryPolicy = policyFactory.GetSqlRetryPolicy(CommonConstants.NUMBER_OF_RETRIES);
            var msisdns = string.Join(",", changes.Select(x => x.MSISDN));
            var tenantId = changes.FirstOrDefault()?.TenantId;
            var revServices = new List<RevServiceDetail>();
            sqlRetryPolicy.Execute(() =>
            {
                var parameters = new List<SqlParameter>()
                    {
                        new SqlParameter(CommonSQLParameterNames.MSISDNS, msisdns),
                        new SqlParameter(CommonSQLParameterNames.SERVICE_PROVIDER_ID_PASCAL_CASE, bulkChange.ServiceProviderId),
                        new SqlParameter(CommonSQLParameterNames.TENANT_ID, tenantId),
                    };
                revServices = SqlQueryHelper.ExecuteStoredProcedureWithListResult((type, message) =>
                    context.logger.LogInfo(type, message),
                    context.CentralDbConnectionString,
                    Amop.Core.Constants.SQLConstant.StoredProcedureName.UPDATE_USERNAME_GET_REV_SERVICE,
                    (dataReader) => GetRevServicesFromReader(dataReader),
                    parameters,
                    Amop.Core.Constants.SQLConstant.TimeoutSeconds);
            });

            var revIOAuthenticationRepository = new RevioAuthenticationRepository(context.CentralDbConnectionString, new Base64Service());
            var integrationAuthenticationId = revServices.FirstOrDefault()?.IntegrationAuthenticationId ?? 0;

            var revIOAuthentication = revIOAuthenticationRepository.GetRevioApiAuthentication(integrationAuthenticationId);
            if (revIOAuthentication == null || string.IsNullOrWhiteSpace(revIOAuthentication.APIKey))
            {
                string message = LogCommonStrings.CAN_NOT_FIND_REV_AUTHENTICATION;
                LogInfo(context, LogTypeConstant.Exception, message);

                var change = changes.First();
                logRepo.AddM2MLogEntry(new CreateM2MDeviceBulkChangeLog()
                {
                    BulkChangeId = bulkChange.Id,
                    ErrorText = message,
                    HasErrors = true,
                    LogEntryDescription = Common.CommonString.UPDATE_USERNAME_AND_COST_CENTER_OF_DEVICES,
                    M2MDeviceChangeId = change.Id,
                    ProcessBy = processedBy,
                    ProcessedDate = DateTime.UtcNow,
                    ResponseStatus = BulkChangeStatus.ERROR,
                    RequestText = JsonConvert.SerializeObject(change),
                    ResponseText = message
                });
                await MarkProcessedForMobilityDeviceChangeAsync(context, change.Id, false, message);
                return false;
            }
            var revApiClient = new RevioApiClient(new SingletonHttpClientFactory(), _httpRequestFactory, revIOAuthentication,
                context.IsProduction);

            foreach (var change in changes)
            {
                var hasErrors = false;
                var logMessage = string.Empty;
                var amopRevService = revServices.Where(x => x.MSISDN == change.MSISDN && x.TenantId == change.TenantId).OrderBy(x => x.ActivatedDate)?.FirstOrDefault();
                if (revApiClient != null && amopRevService != null)
                {
                    var revService = await LookupRevServiceAsync(amopRevService?.RevServiceId, revApiClient, context);
                    if (revService != null)
                    {
                        var usernameIndex = GetRevFieldIndex(revService, CommonColumnNames.Username);
                        var costCenterIndex1 = GetRevFieldIndex(revService, CommonColumnNames.COST_CENTER_1);
                        var costCenterIndex2 = GetRevFieldIndex(revService, CommonColumnNames.COST_CENTER_2);
                        var costCenterIndex3 = GetRevFieldIndex(revService, CommonColumnNames.COST_CENTER_3);
                        var serviceDescriptionPath = GetRevFieldPath(revService, CommonColumnNames.Description);
                        Dictionary<int, string> fieldsToUpdate = new Dictionary<int, string>();

                        if (!string.IsNullOrWhiteSpace(contactName) && usernameIndex >= 0 && !string.Equals(pushUsernameUpdateType, PushUsernameUpdate.SERVICE_DESCRIPTION, StringComparison.OrdinalIgnoreCase))
                        {
                            fieldsToUpdate.Add(usernameIndex, contactName);
                        }
                        if (string.Equals(pushUsernameUpdateType, PushUsernameUpdate.SERVICE_DESCRIPTION, StringComparison.OrdinalIgnoreCase) || string.Equals(pushUsernameUpdateType, PushUsernameUpdate.BOTH, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!string.IsNullOrWhiteSpace(contactName) && !string.IsNullOrWhiteSpace(serviceDescriptionPath))
                            {
                                //passed -1 for service description as it is in root level of Response from Get Rev Service API
                                fieldsToUpdate.Add(-1, contactName);
                            }

                        }
                        if (!string.IsNullOrWhiteSpace(costCenter1) && costCenterIndex1 >= 0)
                        {
                            fieldsToUpdate.Add(costCenterIndex1, costCenter1);
                        }
                        // For some cases those have the "Cost Center" field instead of "Cost Center 1" 
                        else if (!string.IsNullOrWhiteSpace(costCenter1) && costCenterIndex1 < 0)
                        {
                            var costCenterIndex = GetRevFieldIndex(revService, CommonColumnNames.COST_CENTER);
                            if (costCenterIndex >= 0)
                            {
                                fieldsToUpdate.Add(costCenterIndex, costCenter1);
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(costCenter2) && costCenterIndex2 >= 0)
                        {
                            fieldsToUpdate.Add(costCenterIndex2, costCenter2);
                        }

                        if (!string.IsNullOrWhiteSpace(costCenter3) && costCenterIndex3 >= 0)
                        {
                            fieldsToUpdate.Add(costCenterIndex3, costCenter3);
                        }

                        if (fieldsToUpdate.Count > 0)
                        {
                            var response = await revApiClient.UpdateServiceCustomFieldAsync(amopRevService.RevServiceId, fieldsToUpdate, (message) => LogInfo(context, CommonConstants.SUB, message));
                            hasErrors = response.HasErrors;
                            var responseText = JsonConvert.SerializeObject(response.ResponseObject);
                            logRepo.AddMobilityLogEntry(new CreateMobilityDeviceBulkChangeLog()
                            {
                                BulkChangeId = bulkChange.Id,
                                ErrorText = response.HasErrors ? responseText : null,
                                HasErrors = response.HasErrors,
                                LogEntryDescription = Common.CommonString.UPDATE_USERNAME_AND_COST_CENTER_OF_DEVICES_REVIO_API,
                                MobilityDeviceChangeId = change.Id,
                                ProcessBy = processedBy,
                                ProcessedDate = DateTime.UtcNow,
                                RequestText = response.ActionText + Environment.NewLine + response.RequestObject,
                                ResponseStatus = response.HasErrors ? BulkChangeStatus.ERROR : BulkChangeStatus.PROCESSED,
                                ResponseText = responseText
                            });
                        }
                    }
                }

                if (!hasErrors)
                {
                    (hasErrors, logMessage) = bulkChangeRepository.UpdateUsernameDeviceForAMOP(context, bulkChange.ServiceProviderId, change.ICCID, usernameUpdateRequest, bulkChange.PortalTypeId, bulkChange.IntegrationId, bulkChange.Id, bulkChange.TenantId, change.MSISDN);
                    logRepo.AddMobilityLogEntry(new CreateMobilityDeviceBulkChangeLog()
                    {
                        BulkChangeId = bulkChange.Id,
                        ErrorText = hasErrors ? logMessage : null,
                        HasErrors = hasErrors,
                        LogEntryDescription = Common.CommonString.UPDATE_USERNAME_AND_COST_CENTER_OF_DEVICES,
                        MobilityDeviceChangeId = change.Id,
                        ProcessBy = processedBy,
                        ProcessedDate = DateTime.UtcNow,
                        RequestText = LogCommonStrings.UPDATING_USERNAME_AND_COST_CENTER,
                        ResponseStatus = hasErrors ? BulkChangeStatus.ERROR : BulkChangeStatus.PROCESSED,
                        ResponseText = logMessage
                    });
                }

                await MarkProcessedForMobilityDeviceChangeAsync(context, change.Id, !hasErrors, logMessage);
            }
            return true;
        }

        public static NotificationEmailDevice GetNotificationEmailDeviceFromReader(SqlDataReader dataReader)
        {
            ArgumentNullException.ThrowIfNull(dataReader);
            var columns = dataReader.GetColumnsFromReader();
            var device = new NotificationEmailDevice()
            {
                Id = dataReader.IntFromReader(columns, CommonColumnNames.Id),
                MSISDN = dataReader.StringFromReader(columns, CommonColumnNames.MSISDN),
                Customer = dataReader.StringFromReader(columns, CommonColumnNames.Customer),
                Username = dataReader.StringFromReader(columns, CommonColumnNames.Username)
            };
            return device;
        }

        public static RevServiceDetail GetRevServicesFromReader(SqlDataReader dataReader)
        {
            ArgumentNullException.ThrowIfNull(dataReader);
            DateTime? activatedDate = null;
            DateTime? disconnectedDate = null;
            if (!dataReader.IsDBNull(dataReader.GetOrdinal(CommonColumnNames.ActivatedDate)))
            {
                activatedDate = DateTime.Parse(dataReader[CommonColumnNames.ActivatedDate].ToString());
            }
            if (!dataReader.IsDBNull(dataReader.GetOrdinal(CommonColumnNames.DisconnectedDate)))
            {
                disconnectedDate = DateTime.Parse(dataReader[CommonColumnNames.DisconnectedDate].ToString());
            }
            var columns = dataReader.GetColumnsFromReader();
            var revServiceDetail = new RevServiceDetail()
            {
                Id = dataReader.IntFromReader(columns, CommonColumnNames.Id),
                RevServiceId = dataReader.IntFromReader(columns, CommonColumnNames.RevServiceId),
                ActivatedDate = activatedDate,
                DisconnectedDate = disconnectedDate,
                MSISDN = dataReader.StringFromReader(columns, CommonColumnNames.MSISDN),
                TenantId = dataReader.IntFromReader(columns, CommonColumnNames.TenantId),
                IntegrationAuthenticationId = dataReader.IntFromReader(columns, CommonColumnNames.IntegrationAuthenticationId)
            };
            return revServiceDetail;
        }

        public static RevServiceDetail GetM2MRevServicesFromReader(SqlDataReader dataReader)
        {
            ArgumentNullException.ThrowIfNull(dataReader);
            DateTime? activatedDate = null;
            DateTime? disconnectedDate = null;
            if (!dataReader.IsDBNull(dataReader.GetOrdinal(CommonColumnNames.ActivatedDate)))
            {
                activatedDate = DateTime.Parse(dataReader[CommonColumnNames.ActivatedDate].ToString());
            }
            if (!dataReader.IsDBNull(dataReader.GetOrdinal(CommonColumnNames.DisconnectedDate)))
            {
                disconnectedDate = DateTime.Parse(dataReader[CommonColumnNames.DisconnectedDate].ToString());
            }
            var columns = dataReader.GetColumnsFromReader();
            var revServiceDetail = new RevServiceDetail()
            {
                Id = dataReader.IntFromReader(columns, CommonColumnNames.Id),
                RevServiceId = dataReader.IntFromReader(columns, CommonColumnNames.RevServiceId),
                ActivatedDate = activatedDate,
                DisconnectedDate = disconnectedDate,
                ICCID = dataReader.StringFromReader(columns, CommonColumnNames.ICCID),
                TenantId = dataReader.IntFromReader(columns, CommonColumnNames.TenantId),
                IntegrationAuthenticationId = dataReader.IntFromReader(columns, CommonColumnNames.IntegrationAuthenticationId)
            };
            return revServiceDetail;
        }

        public async Task<RevServiceResponse> LookupRevServiceAsync(int? revServiceId, RevioApiClient client, KeySysLambdaContext context)
        {
            if (revServiceId.HasValue)
            {
                var response = await client.GetRevServicesAsync<RevServiceResponse>(revServiceId.Value, (message) => LogInfo(context, CommonConstants.SUB, message));
                if (response == null || !response.OK)
                {
                    return null;
                }
                return response;
            }
            return null;
        }

        private static int GetRevFieldIndex(RevServiceResponse response, string fieldName)
        {
            for (int i = 0; i < response.Fields.Count; i++)
            {
                if (string.Equals(Regex.Replace(response.Fields[i].Label, @"\s+", ""), fieldName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        private async Task SendUsernameUpdatedNotifications(KeySysLambdaContext context, List<NotificationEmailDevice> devices, string newUsername, string changedBy)
        {
            var subject = BuildAdminNotificationSubject(string.Format(CommonConstants.USERNAME_UPDATE_REPORT, DateTime.Now.ToString(CommonConstants.AMOP_DATE_FORMAT)));
            var body = BuildNotificationBody(devices, newUsername, changedBy);
            await SendEmailAsync(context, subject, body);
        }

        private async Task SendEmailAsync(KeySysLambdaContext context, string subject, string body)
        {
            LogInfo(context, CommonConstants.SUB);
            using (var client = new AmazonSimpleEmailServiceClient(AwsSesCredentials(context), RegionEndpoint.USEast1))
            {
                var message = new MimeMessage();
                Multipart multipart = new MultipartAlternative();

                message.From.Add(MailboxAddress.Parse(context.OptimizationSettings.FromEmailAddress));
                var recipientAddressList = context.OptimizationSettings.ToEmailAddresses.Split(';').ToList();
                foreach (var recipientAddress in recipientAddressList)
                {
                    message.To.Add(MailboxAddress.Parse(recipientAddress));
                }

                var bccAddressList = context.OptimizationSettings.BccEmailAddresses?.Split(';').ToList() ?? new List<string>();
                foreach (var bccAddress in bccAddressList)
                {
                    message.Bcc.Add(MailboxAddress.Parse(bccAddress));
                }

                message.Subject = subject;
                multipart.Add(BuildResultsEmailBody(context, body).ToMessageBody());
                message.Body = multipart;
                var stream = new System.IO.MemoryStream();
                message.WriteTo(stream);

                var sendRequest = new SendRawEmailRequest
                {
                    RawMessage = new RawMessage(stream)
                };
                try
                {
                    var response = await client.SendRawEmailAsync(sendRequest);
                    LogInfo(context, CommonConstants.RESPONSE_STATUS, $"{response.HttpStatusCode:d} {response.HttpStatusCode:g}");
                }
                catch (Exception ex)
                {
                    LogInfo(context, CommonConstants.EXCEPTION, "Error Sending Email: " + ex.Message);
                }
            }
        }

        private string BuildAdminNotificationSubject(string subjectLine)
        {
            return $"{(!string.IsNullOrWhiteSpace(subjectLine) ? $"{subjectLine}" : string.Empty)}";
        }

        private string BuildNotificationBody(List<NotificationEmailDevice> devices, string newUsername, string changedBy)
        {
            StringBuilder stringBuilder = new StringBuilder();
            AddGreetingAndUpdatedUsernameMessage(stringBuilder, CommonConstants.DEFAULT_ADMIN_NAME_NOTIFICATION_RULE);
            BuildEmailUpdatedUsernameDataTable(stringBuilder, devices, newUsername, changedBy);

            return stringBuilder.ToString();
        }

        private static void AddGreetingAndUpdatedUsernameMessage(StringBuilder stringBuilder, string name)
        {
            stringBuilder.AppendLine($"<div>{string.Format(CommonConstants.NOTIFICATION_RULE_GREETING_MESSAGE, name)}</div><br/>");
            stringBuilder.AppendLine($"<div>{string.Format(CommonConstants.UPDATED_USERNAME_NOTIFICATION_MESSAGE, DateTime.Now.ToString(CommonConstants.AMOP_DATE_FORMAT))}</div><br/>");
        }

        private void BuildEmailUpdatedUsernameDataTable(StringBuilder stringBuilder, List<NotificationEmailDevice> devices, string newUsername, string changedBy)
        {
            BuildEmailTableStyles(stringBuilder);
            stringBuilder.AppendLine(@"
            <div class=""table_component"">
                <table>
                    <thead>
                        <tr>
                            <th>Customer</th>
                            <th>MSISDN</th>
                            <th>Username Previous Value</th>
                            <th>Username New Value</th>
                            <th>Date Changed</th>
                            <th>Changed By</th>
                        </tr>
                    </thead>
                    <tbody>");
            foreach (var device in devices)
            {
                stringBuilder.AppendLine(@$"
                <tr>
                    <td>{device.Customer}</td>
                    <td>{device.MSISDN}</td>
                    <td>{device.Username}</td>
                    <td>{newUsername}</td>
                    <td>{DateTime.Now.ToString(CommonConstants.AMOP_DATE_FORMAT)}</td>
                    <td>{changedBy}</td>
                </tr>");
            }
            stringBuilder.AppendLine("</tbody></table></div>");
        }

        private void BuildEmailTableStyles(StringBuilder stringBuilder)
        {
            stringBuilder.AppendLine(@"
            <style> 
                .table_component {overflow:auto;width:100%;} .table_component table {border:1px double #000000;height:100%;width:100%;table-layout:auto;border-collapse:separate;border-spacing:2px;text-align:center;} .table_component caption {caption-side:top;text-align:left;} .table_component th {border:1px double #000000;background-color:#ffffff;color:#000000;padding:1px;} .table_component td {border:1px double #000000;background-color:#ffffff;color:#000000;padding:1px;}
            </style>");
        }

        private BodyBuilder BuildResultsEmailBody(KeySysLambdaContext context, string bodyText, string fileNamePrefix = "", byte[] logoBytes = null)
        {
            LogInfo(context, CommonConstants.SUB, $"");
            var body = new BodyBuilder();
            if (logoBytes != null)
            {
                var logo = body.LinkedResources.Add("Logo.png", logoBytes);
                logo.ContentId = MimeUtils.GenerateMessageId();
                bodyText = bodyText.Replace("[[logo_content_id]]", logo.ContentId);
            }
            body.HtmlBody = bodyText;
            body.TextBody = HtmlToPlainText(bodyText);
            return body;
        }

        static string HtmlToPlainText(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return string.Empty;

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            // Extract text with custom handling for specific tags
            var text = ExtractTextWithFormatting(htmlDoc.DocumentNode);

            // Decode HTML entities
            text = WebUtility.HtmlDecode(text);

            return text;
        }

        static string ExtractTextWithFormatting(HtmlNode node)
        {
            if (node.Name == "br")
                return Environment.NewLine;

            if (node.Name == "p")
                return Environment.NewLine + node.InnerText + Environment.NewLine;

            if (node.HasChildNodes)
            {
                string result = "";
                foreach (var child in node.ChildNodes)
                {
                    result += ExtractTextWithFormatting(child);
                }
                return result;
            }

            return node.InnerText;
        }
        private static string GetRevFieldPath(RevServiceResponse response, string fieldName)
        {
            var properties = typeof(RevServiceResponse).GetProperties();

            foreach (var property in properties)
            {
                if (string.Equals(property.Name, fieldName, StringComparison.OrdinalIgnoreCase))
                {
                    return $"/{property.Name.ToLower()}";
                }
            }

            return string.Empty;
        }
    }
}
