using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Altaworx.AWS.Core;
using Altaworx.AWS.Core.Helpers.Constants;
using Altaworx.AWS.Core.Models;
using Altaworx.ThingSpace.Core.Models;
using Amop.Core.Constants;
using Amop.Core.Helpers;
using Amop.Core.Models;
using Amop.Core.Models.DeviceBulkChange;
using Amop.Core.Models.Revio;
using Amop.Core.Repositories;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using static Amop.Core.Constants.SQLConstant;
using AMOPSQLConstants = Amop.Core.Constants.SQLConstant;

namespace AltaworxDeviceBulkChange.Repositories
{
    public class BulkChangeRepository
    {
        public BulkChangeRepository()
        {
        }



        public List<string> GetICCIDM2MDeviceChangeBybulkId(string connectionString, long bulkchangeId)
        {
            var iccids = new List<string>();

            using (var con = new SqlConnection(connectionString))
            {
                using (var cmd = con.CreateCommand())
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = "SELECT ICCID FROM M2M_DeviceChange WHERE BulkChangeId = @bulkChangeId AND MSISDN IS NULL";
                    cmd.Parameters.AddWithValue("@bulkChangeId", bulkchangeId);

                    con.Open();
                    SqlDataReader rdr = cmd.ExecuteReader();
                    while (rdr.Read())
                    {
                        var iccid = rdr["ICCID"].ToString();
                        iccids.Add(iccid);
                    }
                }
            }
            return iccids;
        }

        public List<string> UpdateMSISDNToM2M_DeviceChange(string connectionString, long bulkchangeId, string msisdn, string iccid, int serviceProviderId, DeviceResponse deviceResponse)
        {
            var iccids = new List<string>();

            using (var con = new SqlConnection(connectionString))
            {
                using (var cmd = con.CreateCommand())
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = @"UPDATE Device
                                        SET DeviceStatusId = @deviceStatusId,
                                        MSISDN = @msisdn,
                                        IpAddress = @ipAddress,
                                        ModifiedBy = 'AltaworxJasperAWSUpdateDeviceStatus',
                                        ModifiedDate = GETUTCDATE()
                                        WHERE IsDeleted = 0 
                                        AND ServiceProviderId = @serviceProviderId
                                        AND ICCID = @iccid

                                        UPDATE M2M_DeviceChange
                                        SET MSISDN = @msisdn,
                                        IpAddress = @ipAddress
                                        WHERE BulkChangeId = @bulkChangeId
                                        AND ICCID = @iccid";
                    cmd.Parameters.AddWithValue("@bulkChangeId", bulkchangeId);
                    cmd.Parameters.AddWithValue("@msisdn", string.IsNullOrEmpty(msisdn) ? string.Empty : msisdn);
                    cmd.Parameters.AddWithValue("@iccid", iccid);
                    cmd.Parameters.AddWithValue("@serviceProviderId", serviceProviderId);
                    cmd.Parameters.AddWithValue("@ipAddress", string.IsNullOrEmpty(deviceResponse.ipAddress) ? string.Empty : deviceResponse.ipAddress);
                    cmd.Parameters.AddWithValue("@DeviceStatusId", (int)ThingSpaceDeviceStatusIdEnum.THINGSPACE_DEVICESTATUSID_ACTIVE);

                    con.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            return iccids;
        }

        public async Task UpdateRevCustomer(KeySysLambdaContext context, BulkChangeAssociateCustomer changeRequest, int tenantId)
        {
            context.logger.LogInfo(CommonConstants.SUB, "");
            context.logger.LogInfo(CommonConstants.INFO, $"{nameof(changeRequest.ICCID)}: {changeRequest.ICCID}");
            context.logger.LogInfo(CommonConstants.INFO, $"{nameof(changeRequest.RevCustomerId)}: {changeRequest.RevCustomerId}");
            context.logger.LogInfo(CommonConstants.INFO, $"{nameof(tenantId)}: {tenantId}");

            var siteId = 0;
            try
            {
                using (var connection = new SqlConnection(context.CentralDbConnectionString))
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.CommandText = StoredProcedureName.ASSIGN_CUSTOMER_UPDATE_SITE;
                        command.Parameters.AddWithValue("@revcustomerId", changeRequest.RevCustomerId);
                        command.Parameters.AddWithValue("@tenantId", tenantId);
                        command.Parameters.AddWithValue("@iccid", changeRequest.ICCID);
                        command.CommandTimeout = ShortTimeoutSeconds;
                        connection.Open();

                        var rowsAffected = await command.ExecuteNonQueryAsync();
                        if (rowsAffected < NoStatementSQLExecuteDetected)
                        {
                            context.logger.LogInfo(LogTypeConstant.Exception, string.Format(LogCommonStrings.ERROR_WHILE_EXECUTING_STORED_PROCEDURE, StoredProcedureName.ASSIGN_CUSTOMER_UPDATE_SITE));
                        }
                    }
                }

                context.logger.LogInfo(CommonConstants.INFO, $"{nameof(changeRequest.RevCustomerId)}: {changeRequest.RevCustomerId} - {nameof(siteId)}: {siteId}.");
            }
            catch (SqlException ex)
            {
                AwsFunctionBase.LogInfo(context, LogTypeConstant.Exception, string.Format(LogCommonStrings.EXCEPTION_WHEN_EXECUTING_STORED_PROCEDURE, ex.Message, ex.ErrorCode, ex.Number, ex.StackTrace));
            }
            catch (InvalidOperationException ex)
            {
                AwsFunctionBase.LogInfo(context, LogTypeConstant.Exception, string.Format(LogCommonStrings.EXCEPTION_WHEN_CONNECTING_DATABASE, ex.Message));
            }
            catch (Exception ex)
            {
                AwsFunctionBase.LogInfo(context, LogTypeConstant.Exception, ex.Message);
            }
        }

        public async Task UpdateAMOPCustomer(KeySysLambdaContext context, DeviceBulkChangeLogRepository logRepo, ICollection<BulkChangeDetailRecord> changes, BulkChange bulkChange)
        {
            var change = changes.First();
            var dataTableUpdates = BuildTableAssignNonRevCustomer(changes);
            await DeviceBulkChangeAssignNonRevCustomer(context, logRepo, dataTableUpdates, bulkChange, change.Id);
        }

        private async Task DeviceBulkChangeAssignNonRevCustomer(KeySysLambdaContext context, DeviceBulkChangeLogRepository logRepo, DataTable table, BulkChange bulkChange, long deviceChangeId)
        {
            DeviceChangeResult<string, string> dbResult;
            try
            {
                using (var conn = new SqlConnection(context.CentralDbConnectionString))
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandTimeout = AMOPSQLConstants.ShortTimeoutSeconds;
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.CommandText = StoredProcedureName.DEVICE_BULK_CHANGE_ASSIGN_NON_REV_CUSTOMER;
                        cmd.Parameters.Add(CommonSQLParameterNames.UPDATED_VALUES, SqlDbType.Structured);
                        cmd.Parameters[CommonSQLParameterNames.UPDATED_VALUES].Value = table;
                        cmd.Parameters[CommonSQLParameterNames.UPDATED_VALUES].TypeName = StoredProcedureName.UPDATE_M2M_DEVICE_MOBILITY_DEVICE_SITE_TYPE;
                        cmd.Parameters.AddWithValue(CommonSQLParameterNames.BULK_CHANGE_ID, bulkChange.Id);
                        cmd.Parameters.AddWithValue(CommonSQLParameterNames.PORTAL_TYPE_ID, bulkChange.PortalTypeId);
                        conn.Open();

                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                dbResult = new DeviceChangeResult<string, string>()
                {
                    ActionText = StoredProcedureName.DEVICE_BULK_CHANGE_ASSIGN_NON_REV_CUSTOMER,
                    HasErrors = false,
                    RequestObject = $"bulkChangeId: {bulkChange.Id}",
                    ResponseObject = CommonConstants.OK
                };
            }
            catch (Exception ex)
            {
                context.logger.LogError($"Error Executing Stored Procedure usp_DeviceBulkChange_Assign_Non_Rev_Customer: {ex.Message} {ex.StackTrace}");
                var logId = Guid.NewGuid();
                dbResult = new DeviceChangeResult<string, string>()
                {
                    ActionText = StoredProcedureName.DEVICE_BULK_CHANGE_ASSIGN_NON_REV_CUSTOMER,
                    HasErrors = true,
                    RequestObject = $"bulkChangeId: {bulkChange.Id}",
                    ResponseObject = $"Error Executing Stored Procedure. Ref: {logId}"
                };
            }
            logRepo.AddM2MLogEntry(new CreateM2MDeviceBulkChangeLog()
            {
                BulkChangeId = bulkChange.Id,
                ErrorText = dbResult.HasErrors ? dbResult.ResponseObject : null,
                HasErrors = dbResult.HasErrors,
                LogEntryDescription = "AssignNonRevCustomer: Update AMOP",
                M2MDeviceChangeId = deviceChangeId,
                ProcessBy = LogCommonStrings.ALTAWORX_DEVICE_BULK_CHANGE,
                ProcessedDate = DateTime.UtcNow,
                ResponseStatus = dbResult.HasErrors ? BulkChangeStatus.ERROR : BulkChangeStatus.PROCESSED,
                RequestText = dbResult.ActionText + Environment.NewLine + dbResult.RequestObject,
                ResponseText = dbResult.ResponseObject
            });
        }

        private DataTable BuildTableAssignNonRevCustomer(ICollection<BulkChangeDetailRecord> changes)
        {
            var table = new DataTable();
            table.Columns.Add(CommonColumnNames.DeviceId);
            table.Columns.Add(CommonColumnNames.TenantId);
            table.Columns.Add(CommonColumnNames.SiteId, typeof(int));
            foreach (var change in changes.Where(change => !string.IsNullOrWhiteSpace(change.ChangeRequest)))
            {
                var associateNonRevCustomerModel = JsonConvert.DeserializeObject<BulkChangeAssociateNonRevCustomerModel>(change.ChangeRequest);
                var dataRow = AddDataToTableAssignNonRev(table, change, associateNonRevCustomerModel);
                table.Rows.Add(dataRow);
            }
            return table;
        }

        private DataRow AddDataToTableAssignNonRev(DataTable table, BulkChangeDetailRecord detailRecord, BulkChangeAssociateNonRevCustomerModel nonRevCustomerModel)
        {
            var dr = table.NewRow();
            dr[0] = detailRecord.DeviceId;
            dr[1] = nonRevCustomerModel.TenantId;
            dr[2] = nonRevCustomerModel.SiteId;
            return dr;
        }


        public async Task MarkBulkChangeStatusAsync(KeySysLambdaContext context, long bulkChangeId, string bulkChangeStatus)
        {
            context.logger.LogInfo(LogTypeConstant.Sub, $"({bulkChangeId})");

            try
            {
                using (var conn = new SqlConnection(context.CentralDbConnectionString))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandType = CommandType.Text;
                        cmd.CommandText = @"UPDATE [DeviceBulkChange]
                                            SET [Status] = @bulkChangeStatus,
                                            [ProcessedDate] = @processedDate
                                            WHERE [Id] = @bulkChangeId";

                        cmd.Parameters.AddWithValue("@processedDate", DateTime.UtcNow);
                        cmd.Parameters.AddWithValue("@bulkChangeId", bulkChangeId);
                        cmd.Parameters.AddWithValue("@bulkChangeStatus", bulkChangeStatus);
                        cmd.CommandTimeout = AMOPSQLConstants.ShortTimeoutSeconds;

                        var rowsAffected = await cmd.ExecuteNonQueryAsync();
                        if (rowsAffected < 1)
                        {
                            context.logger.LogInfo(LogTypeConstant.Error, $"Bulk change Id cannot be found {bulkChangeId}");
                        }

                    }
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandType = CommandType.Text;
                        cmd.CommandText = @"UPDATE [M2M_DeviceChange]
                                            SET [Status] = @bulkChangeStatus,
                                            [ProcessedDate] = @processedDate
                                            WHERE [BulkChangeId] = @bulkChangeId";

                        cmd.Parameters.AddWithValue("@processedDate", DateTime.UtcNow);
                        cmd.Parameters.AddWithValue("@bulkChangeId", bulkChangeId);
                        cmd.Parameters.AddWithValue("@bulkChangeStatus", bulkChangeStatus);
                        cmd.CommandTimeout = AMOPSQLConstants.ShortTimeoutSeconds;

                        var rowsAffected = await cmd.ExecuteNonQueryAsync();
                        if (rowsAffected < 1)
                        {
                            context.logger.LogInfo(LogTypeConstant.Error, $"Bulk change Id cannot be found {bulkChangeId}");
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                context.logger.LogInfo(LogTypeConstant.Exception, $"EXCEPTION: {ex.Message} - {ex.StackTrace}");
                context.logger.LogInfo(LogTypeConstant.Exception, $"Bulk Change ID: {bulkChangeId}");
            }
        }

        public (bool, string) UpdateUsernameDeviceForAMOP(KeySysLambdaContext context, int serviceProviderId, string iccid, BulkChangeEditUsername usernameUpdateRequest, int portalTypeId, int integrationId, long bulkChangeId, int tenantId, string msisdn = null)
        {
            context.logger.LogInfo(LogTypeConstant.Sub, $"({serviceProviderId}, {iccid}, {usernameUpdateRequest.ContactName}, {portalTypeId})");

            try
            {
                using (var conn = new SqlConnection(context.CentralDbConnectionString))
                {
                    conn.Open();

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.CommandText = StoredProcedureName.usp_Update_Username_Device;
                        cmd.Parameters.AddWithValue("@ServiceProviderId", serviceProviderId);
                        cmd.Parameters.AddWithValue("@IntegrationId", integrationId);
                        cmd.Parameters.AddWithValue("@ICCID", iccid);
                        cmd.Parameters.AddWithValue("@Username", usernameUpdateRequest.ContactName);
                        cmd.Parameters.AddWithValue("@CostCenter1", usernameUpdateRequest.CostCenter1);
                        cmd.Parameters.AddWithValue("@CostCenter2", usernameUpdateRequest.CostCenter2);
                        cmd.Parameters.AddWithValue("@CostCenter3", usernameUpdateRequest.CostCenter3);
                        cmd.Parameters.AddWithValue("@PortalTypeId", portalTypeId);
                        cmd.Parameters.AddWithValue("@ProcessedBy", context.Context.FunctionName ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@BulkChangeId", bulkChangeId > 0 ? bulkChangeId : (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@MSISDN", msisdn ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@TenantId", tenantId);

                        using (var jasperConnectionString = new SqlConnection(context.GeneralProviderSettings.JasperDbConnectionString))
                        {
                            cmd.Parameters.AddWithValue("@JasperDbName", jasperConnectionString.Database);
                        }

                        cmd.CommandTimeout = AMOPSQLConstants.ShortTimeoutSeconds;
                        var logMessage = string.Empty;
                        var hasErrors = false;
                        int affectedRows = cmd.ExecuteNonQuery();
                        if (affectedRows == 0)
                        {
                            logMessage = string.Format(LogCommonStrings.DEVICE_WAS_SUPPOSED_TO_BE_UPDATED_BUT_THE_DATABASE_UPDATE_FAILED, iccid, usernameUpdateRequest.ContactName);
                            context.logger.LogInfo(LogTypeConstant.Warning, logMessage);
                            hasErrors = true;
                        }
                        if (affectedRows > 0)
                        {
                            logMessage = LogCommonStrings.THE_USERNAME_AND_COST_CENTER_WAS_UPDATED_SUCCESSFULLY;
                            context.logger.LogInfo(LogTypeConstant.Info, logMessage);
                            hasErrors = false;
                        }
                        return (hasErrors, logMessage);
                    }
                }
            }
            catch (SqlException ex)
            {
                AwsFunctionBase.LogInfo(context, LogTypeConstant.Exception, $"Exception when executing the script stored procedure: {ex.Message}, ErrorCode:{ex.ErrorCode}-{ex.Number}");
                throw ex;
            }
            catch (InvalidOperationException ex)
            {
                AwsFunctionBase.LogInfo(context, LogTypeConstant.Exception, $"Exception when connecting to database: {ex.Message}");
                throw ex;
            }
            catch (Exception ex)
            {
                AwsFunctionBase.LogInfo(context, LogTypeConstant.Exception, $"Exception: {ex.Message} - {ex.StackTrace}");
                throw ex;
            }
        }

        public bool IsPreviousBulkChangeProcessing(KeySysLambdaContext context, long bulkChangeId)
        {
            context.logger.LogInfo(CommonConstants.SUB, "");
            var isBulkChangeProcessing = true;
            try
            {
                using (var connection = new SqlConnection(context.CentralDbConnectionString))
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.CommandText = StoredProcedureName.CHECK_PREVIOUS_BULK_CHANGE_PROCESSING;
                        command.Parameters.AddWithValue("@DeviceBulkChangeId", bulkChangeId);
                        command.CommandTimeout = ShortTimeoutSeconds;
                        connection.Open();

                        var result = command.ExecuteScalar();
                        if (result != null)
                        {
                            var bulkChangeProcessingRow = (int)result;
                            if (bulkChangeProcessingRow <= 0)
                            {
                                context.logger.LogInfo(CommonConstants.INFO, LogCommonStrings.NO_BULK_CHANGE_IN_PROCESSING);
                                isBulkChangeProcessing = false;
                            }
                            else
                            {
                                context.logger.LogInfo(CommonConstants.INFO, LogCommonStrings.BULK_CHANGE_IN_PROCESSING);
                                isBulkChangeProcessing = true;
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                AwsFunctionBase.LogInfo(context, CommonConstants.EXCEPTION, string.Format(LogCommonStrings.EXCEPTION_WHEN_EXECUTING_STORED_PROCEDURE, ex.Message, ex.ErrorCode, ex.Number, ex.StackTrace));
            }
            catch (InvalidOperationException ex)
            {
                AwsFunctionBase.LogInfo(context, CommonConstants.EXCEPTION, string.Format(LogCommonStrings.EXCEPTION_WHEN_CONNECTING_DATABASE, ex.Message));
            }
            catch (Exception ex)
            {
                AwsFunctionBase.LogInfo(context, CommonConstants.EXCEPTION, ex.Message);
            }

            return isBulkChangeProcessing;
        }
    }
}
