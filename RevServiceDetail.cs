using System;
using System.Collections.Generic;
using System.Text;

namespace AltaworxDeviceBulkChange.Models
{
    public class RevServiceDetail
    {
        public int Id { get; set; }
        public string ICCID { get; set; }
        public string MSISDN { get; set; }
        public int TenantId { get; set; }
        public int RevServiceId { get; set; }
        public DateTime? ActivatedDate { get; set; }
        public DateTime? DisconnectedDate { get; set; }
        public int IntegrationAuthenticationId { get; set; }
    }
}
