using Amop.Core.Helpers;
using Amop.Core.Constants;
using Microsoft.Data.SqlClient;
using Amop.Core.Enumerations;
using System;
using System.Collections.Generic;
using Amop.Core.Models.Internal.Webhook;
using System.Linq;

namespace AltaworxDeviceBulkChange
{
    public class BulkChangeDetailRecord
    {
        public long Id { get; set; }
        public string MSISDN { get; set; }
        public string DeviceIdentifier { get; set; }
        public long BulkChangeId { get; set; }
        public string Status { get; set; }
        public int ServiceProviderId { get; set; }
        public int IntegrationId { get; set; }
        public int TenantId { get; set; }
        public string ChangeRequest { get; set; }
        public string StatusDetails { get; set; }
        public int ChangeRequestTypeId { get; set; }
        public string ChangeRequestType { get; set; }
        public string ICCID { get; set; }
        public string PhoneNumberIdentifier { get; set; }
        public int? DeviceId { get; set; }
        public string AdditionalStepStatus { get; set; }
        public string AdditionalStepDetails { get; set; }
        public string Eid { get; set; }
        public List<DetailsLogEntry> DetailsLogEntries { get; set; } = new List<DetailsLogEntry>();

        public static class ColumnNames
        {
            public const string Id = CommonColumnNames.Id;
            public const string MSISDN = CommonColumnNames.MSISDN;
            public const string DeviceIdentifier = CommonColumnNames.DeviceIdentifier;
            public const string BulkChangeId = CommonColumnNames.BulkChangeId;
            public const string Status = CommonColumnNames.Status;
            public const string ServiceProviderId = CommonColumnNames.ServiceProviderId;
            public const string IntegrationId = CommonColumnNames.IntegrationId;
            public const string TenantId = CommonColumnNames.TenantId;
            public const string ChangeRequestTypeId = CommonColumnNames.ChangeRequestTypeId;
            public const string ChangeRequestType = CommonColumnNames.ChangeRequestType;
            public const string ChangeRequest = CommonColumnNames.ChangeRequest;
            public const string ICCID = CommonColumnNames.ICCID;
            public const string PhoneNumberIdentifier = CommonColumnNames.PhoneNumberIdentifier;
            public const string DeviceId = CommonColumnNames.DeviceId;
            public const string AdditionalStepStatus = CommonColumnNames.AdditionalStepStatus;
            public const string AdditionalStepDetails = CommonColumnNames.AdditionalStepDetails;
            public const string DetailsLogEntries = CommonColumnNames.DetailsLogEntries;
        }

        public static BulkChangeDetailRecord DeviceRecordFromReader(SqlDataReader dataReader, int portalTypeId)
        {
            var columns = dataReader.GetColumnsFromReader();
            var bulkChangeDetailRecord = new BulkChangeDetailRecord
            {
                Id = dataReader.LongFromReader(columns, ColumnNames.Id),
                MSISDN = dataReader.StringFromReader(columns, ColumnNames.MSISDN),
                DeviceIdentifier = dataReader.StringFromReader(columns, ColumnNames.DeviceIdentifier),
                BulkChangeId = dataReader.LongFromReader(columns, ColumnNames.BulkChangeId),
                Status = dataReader.StringFromReader(columns, ColumnNames.Status),
                ServiceProviderId = dataReader.IntFromReader(columns, ColumnNames.ServiceProviderId),
                IntegrationId = dataReader.IntFromReader(columns, ColumnNames.IntegrationId),
                TenantId = dataReader.IntFromReader(columns, ColumnNames.TenantId),
                ChangeRequestTypeId = dataReader.IntFromReader(columns, ColumnNames.ChangeRequestTypeId),
                ChangeRequestType = dataReader.StringFromReader(columns, ColumnNames.ChangeRequestType),
                ChangeRequest = dataReader.StringFromReader(columns, ColumnNames.ChangeRequest),
                ICCID = dataReader.StringFromReader(columns, ColumnNames.ICCID),
                DeviceId = dataReader.IntFromReader(columns, ColumnNames.DeviceId),
                DetailsLogEntries = dataReader.ListFromReader<DetailsLogEntry>(columns, ColumnNames.DetailsLogEntries)
            };

            if (portalTypeId == (int)PortalTypeEnum.M2M)
            {

                bulkChangeDetailRecord.Eid = dataReader.StringFromReader(columns, CommonColumnNames.EID);
            }
            return bulkChangeDetailRecord;
        }

        public static BulkChangeDetailRecord LNPDeviceRecordFromReader(SqlDataReader dataReader)
        {
            var columns = dataReader.GetColumnsFromReader();
            return new BulkChangeDetailRecord
            {
                Id = dataReader.LongFromReader(columns, ColumnNames.Id),
                PhoneNumberIdentifier = dataReader.StringFromReader(columns, ColumnNames.PhoneNumberIdentifier),
                BulkChangeId = dataReader.LongFromReader(columns, ColumnNames.BulkChangeId),
                Status = dataReader.StringFromReader(columns, ColumnNames.Status),
                ServiceProviderId = dataReader.IntFromReader(columns, ColumnNames.ServiceProviderId),
                IntegrationId = dataReader.IntFromReader(columns, ColumnNames.IntegrationId),
                TenantId = dataReader.IntFromReader(columns, ColumnNames.TenantId),
                ChangeRequestTypeId = dataReader.IntFromReader(columns, ColumnNames.ChangeRequestTypeId),
                ChangeRequestType = dataReader.StringFromReader(columns, ColumnNames.ChangeRequestType),
                ChangeRequest = dataReader.StringFromReader(columns, ColumnNames.ChangeRequest),
            };
        }

        public static BulkChangeDetailRecord ProcessedDeviceRecordFromReader(SqlDataReader dataReader)
        {
            var columns = dataReader.GetColumnsFromReader();
            return new BulkChangeDetailRecord
            {
                Id = dataReader.LongFromReader(columns, ColumnNames.Id),
                MSISDN = dataReader.StringFromReader(columns, ColumnNames.MSISDN),
                DeviceIdentifier = dataReader.StringFromReader(columns, ColumnNames.DeviceIdentifier),
                BulkChangeId = dataReader.LongFromReader(columns, ColumnNames.BulkChangeId),
                Status = dataReader.StringFromReader(columns, ColumnNames.Status),
                AdditionalStepStatus = dataReader.StringFromReader(columns, ColumnNames.AdditionalStepStatus),
                AdditionalStepDetails = dataReader.StringFromReader(columns, ColumnNames.AdditionalStepDetails),
                ServiceProviderId = dataReader.IntFromReader(columns, ColumnNames.ServiceProviderId),
                IntegrationId = dataReader.IntFromReader(columns, ColumnNames.IntegrationId),
                TenantId = dataReader.IntFromReader(columns, ColumnNames.TenantId),
                ChangeRequestTypeId = dataReader.IntFromReader(columns, ColumnNames.ChangeRequestTypeId),
                ChangeRequestType = dataReader.StringFromReader(columns, ColumnNames.ChangeRequestType),
                ChangeRequest = dataReader.StringFromReader(columns, ColumnNames.ChangeRequest),
                ICCID = dataReader.StringFromReader(columns, ColumnNames.ICCID),
                DeviceId = dataReader.IntFromReader(columns, ColumnNames.DeviceId),
            };
        }

        public static BulkChangeDetailRequest MapToBulkChangeDetailRequest(BulkChangeDetailRecord record)
        {
            return new BulkChangeDetailRequest
            {
                Id = record.Id,
                MSISDN = record.MSISDN,
                DeviceIdentifier = record.DeviceIdentifier,
                BulkChangeId = record.BulkChangeId,
                Status = record.Status,
                ServiceProviderId = record.ServiceProviderId,
                IntegrationId = record.IntegrationId,
                TenantId = record.TenantId,
                ChangeRequest = record.ChangeRequest,
                StatusDetails = record.StatusDetails,
                ChangeRequestTypeId = record.ChangeRequestTypeId,
                ChangeRequestType = record.ChangeRequestType,
                ICCID = record.ICCID,
                PhoneNumberIdentifier = record.PhoneNumberIdentifier,
                DeviceId = record.DeviceId,
                AdditionalStepStatus = record.AdditionalStepStatus,
                AdditionalStepDetails = record.AdditionalStepDetails,
                Eid = record.Eid,
                DetailsLogEntries = record.DetailsLogEntries.Select(x => new BulkChangeDetailsLog
                {
                    ProcessedDate = x.ProcessedDate,
                    LogEntryDescription = x.LogEntryDescription,
                    ResponseStatus = x.ResponseStatus,
                    RequestText = x.RequestText,
                    ResponseText = x.ResponseText
                }).ToList()
            };
        }
    }

    public class DetailsLogEntry
    {
        public DateTime ProcessedDate { get; set; }
        public string LogEntryDescription { get; set; }
        public string ResponseStatus { get; set; }
        public string RequestText { get; set; }
        public string ResponseText { get; set; }
    }
}
