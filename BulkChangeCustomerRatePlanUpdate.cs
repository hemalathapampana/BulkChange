using System;

namespace AltaworxDeviceBulkChange
{
    public class BulkChangeCustomerRatePlanUpdate
    {
        public int? CustomerRatePlanId { get; set; }
        public decimal? CustomerDataAllocationMB { get; set; }
        public int? CustomerPoolId { get; set; }
        public DateTime? EffectiveDate { get; set; }
    }
}
