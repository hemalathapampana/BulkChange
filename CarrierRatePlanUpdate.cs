using System;

namespace AltaworxDeviceBulkChange
{
    public class CarrierRatePlanUpdate
    {
        public string CarrierRatePlan { get; set; }
        public string CommPlan { get; set; }
        public DateTime? EffectiveDate { get; set; }
        public string PlanUuid { get; set; }
        public long RatePlanId { get; set; }
    }

    public class BulkChangeCarrierRatePlanUpdate
    {
        public CarrierRatePlanUpdate CarrierRatePlanUpdate { get; set; }
    }
}
