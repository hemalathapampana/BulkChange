using System;
using Altaworx.AWS.Core.Models;
using Amop.Core.Constants;
using static Amazon.Lambda.SQSEvents.SQSEvent;

namespace AltaworxDeviceBulkChange
{
    public class SqsValues
    {
        public long BulkChangeId { get; set; }
        public long M2MDeviceChangeId { get; set; }
        public bool IsRetryNewActivateThingSpaceDevice { get; set; }
        public bool IsFromAutomatedUpdateDeviceStatusLambda { get; set; }
        public bool IsRetryUpdateIdentifier { get; set; }
        public int RetryNumber { get; set; }
        public string RequestId { get; set; }
        public SqsValues()
        {
            BulkChangeId = 0;
            M2MDeviceChangeId = 0;
            RetryNumber = 0;
            IsRetryNewActivateThingSpaceDevice = false;
            IsFromAutomatedUpdateDeviceStatusLambda = false;
            IsRetryUpdateIdentifier = false;
            RequestId = string.Empty;
        }

        public SqsValues(KeySysLambdaContext context, SQSMessage message)
        {
            if (message.MessageAttributes.ContainsKey(SQSMessageKeyConstant.BULK_CHANGE_ID))
            {
                BulkChangeId = long.Parse(message.MessageAttributes[SQSMessageKeyConstant.BULK_CHANGE_ID].StringValue);
                context.LogInfo(SQSMessageKeyConstant.BULK_CHANGE_ID, BulkChangeId);
            }
            else
            {
                BulkChangeId = 0;
                context.LogInfo(SQSMessageKeyConstant.BULK_CHANGE_ID, BulkChangeId);
            }

            if (message.MessageAttributes.ContainsKey(SQSMessageKeyConstant.M2M_DEVICE_CHANGE_ID))
            {
                M2MDeviceChangeId = long.Parse(message.MessageAttributes[SQSMessageKeyConstant.M2M_DEVICE_CHANGE_ID].StringValue);
                context.LogInfo(SQSMessageKeyConstant.M2M_DEVICE_CHANGE_ID, M2MDeviceChangeId);
            }

            if (message.MessageAttributes.ContainsKey(SQSMessageKeyConstant.IS_RETRY_NEW_ACTIVATE_THINGSPACE_DEVICE))
            {
                IsRetryNewActivateThingSpaceDevice = Boolean.Parse(message.MessageAttributes[SQSMessageKeyConstant.IS_RETRY_NEW_ACTIVATE_THINGSPACE_DEVICE].StringValue);
                context.LogInfo(SQSMessageKeyConstant.IS_RETRY_NEW_ACTIVATE_THINGSPACE_DEVICE, IsRetryNewActivateThingSpaceDevice);
            }

            if (message.MessageAttributes.ContainsKey(SQSMessageKeyConstant.RETRY_NUMBER))
            {
                RetryNumber = Int32.Parse(message.MessageAttributes[SQSMessageKeyConstant.RETRY_NUMBER].StringValue);
                context.LogInfo(SQSMessageKeyConstant.RETRY_NUMBER, RetryNumber);
            }

            if (message.MessageAttributes.ContainsKey(SQSMessageKeyConstant.IS_FROM_AUTOMATED_UPDATE_DEVICE_STATUS_LAMBDA))
            {
                IsFromAutomatedUpdateDeviceStatusLambda = Boolean.Parse(message.MessageAttributes[SQSMessageKeyConstant.IS_FROM_AUTOMATED_UPDATE_DEVICE_STATUS_LAMBDA].StringValue);
                context.LogInfo(SQSMessageKeyConstant.IS_FROM_AUTOMATED_UPDATE_DEVICE_STATUS_LAMBDA, IsFromAutomatedUpdateDeviceStatusLambda);
            }

            if (message.MessageAttributes.ContainsKey(SQSMessageKeyConstant.IS_RETRY_UPDATE_IDENTIFIER))
            {
                IsRetryUpdateIdentifier = Boolean.Parse(message.MessageAttributes[SQSMessageKeyConstant.IS_RETRY_UPDATE_IDENTIFIER].StringValue);
                context.LogInfo(SQSMessageKeyConstant.IS_RETRY_UPDATE_IDENTIFIER, IsRetryUpdateIdentifier);
            }

            if (message.MessageAttributes.ContainsKey(SQSMessageKeyConstant.REQUEST_ID))
            {
                RequestId = message.MessageAttributes[SQSMessageKeyConstant.REQUEST_ID].StringValue.ToString();
                context.LogInfo(SQSMessageKeyConstant.REQUEST_ID, RequestId);
            }
        }
    }
}
