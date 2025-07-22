using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AltaworxDeviceBulkChange.Models
{
    public partial class IMEI_DeviceType_CarrierRatePlan
    {
        public int Id { get; set; }
        public string Manufacturer { get; set; }
        public string Model { get; set; }
        public string FromIMEI { get; set; }
        public string ToIMEI { get; set; }
        public string DeviceType { get; set; }
        public string SIMType { get; set; }
        public bool ATTCertified { get; set; }
        public string DeviceCommonName { get; set; }
        public string DeviceMarketingName { get; set; }
        public string NetworkType { get; set; }
        public string RANType { get; set; }
        public bool NSDEV { get; set; }
        public bool VoLTECapable { get; set; }
        public int ServiceProviderId { get; set; }
        public System.DateTime CreatedDate { get; set; }
        public string ModifiedBy { get; set; }
        public Nullable<System.DateTime> ModifiedDate { get; set; }
        public string DeletedBy { get; set; }
        public Nullable<System.DateTime> DeletedDate { get; set; }
        public bool IsActive { get; set; }
    }

}
