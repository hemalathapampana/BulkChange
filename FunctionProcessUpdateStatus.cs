using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Altaworx.AWS.Core;
using Altaworx.AWS.Core.Models;
using Altaworx.ThingSpace.Core;
using AltaworxDeviceBulkChange.Repositories;

namespace AltaworxDeviceBulkChange
{
    public class FunctionProcessUpdateStatus
    {
        private BulkChangeRepository bulkChangeRepository;

        public FunctionProcessUpdateStatus()
        {
            this.bulkChangeRepository = new BulkChangeRepository();
        }

        public async Task<bool> ProcessUpdateDeviceAfterActivateThingSpaceDevice(KeySysLambdaContext context, SqsValues sqsValues, BulkChange bulkChange)
        {
            context.LogInfo("ProcessUpdateDeviceAfterActivateThingSpaceDevice", "");
            // get devices from M2M_DeviceChange
            var devices = bulkChangeRepository.GetICCIDM2MDeviceChangeBybulkId(context.CentralDbConnectionString, bulkChange.Id);

            var thingSpaceAuthentication =
               ThingSpaceCommon.GetThingspaceAuthenticationInformation(context.CentralDbConnectionString, bulkChange.ServiceProviderId);
            var accessToken = ThingSpaceCommon.GetAccessToken(thingSpaceAuthentication);
            if (accessToken != null)
            {
                var sessionToken = ThingSpaceCommon.GetSessionToken(thingSpaceAuthentication, accessToken);
                if (sessionToken != null)
                {
                    // call api get device
                    foreach (var iccid in devices)
                    {
                        var deviceResponse = await ThingSpaceCommon.GetThingSpaceDeviceAsync(iccid, thingSpaceAuthentication.BaseUrl, accessToken, sessionToken, context.logger);

                        // if status not active -> return false
                        // else update msisdn to M2M_DeviceChange
                        var carrierInformation = deviceResponse?.carrierInformations?.FirstOrDefault();
                        var msisdnFromAPI = deviceResponse?.deviceIds?.Where(x => x.kind.Equals("msisdn")).Select(x => x.id).FirstOrDefault();
                        var state = carrierInformation?.state;

                        if (!string.IsNullOrEmpty(state) && state.Equals("active"))
                        {
                            bulkChangeRepository.UpdateMSISDNToM2M_DeviceChange(context.CentralDbConnectionString, bulkChange.Id, msisdnFromAPI, iccid, bulkChange.ServiceProviderId, deviceResponse);
                        }
                    }
                }
            }

            // get devices need update msisdn. If without any SIMs need update -> return true, else -> return false -> send message sqs
            var devicesAfterUpdate = bulkChangeRepository.GetICCIDM2MDeviceChangeBybulkId(context.CentralDbConnectionString, bulkChange.Id);
            context.LogInfo("INFO", $"Check Device After Change Status: totalDevice ({devices.Count}), device don't activated ({devicesAfterUpdate})");
            if (devicesAfterUpdate.Count > 0)
            {
                return false;
            }

            return true;
        }
    }
}
