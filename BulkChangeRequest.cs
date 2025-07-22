namespace AltaworxDeviceBulkChange
{
    public class BulkChangeRequest
    {
        public int? ServiceProviderId { get; set; }

        public int? ChangeType { get; set; }

        public bool? ProcessChanges { get; set; }

        public string[] Devices { get; set; }

        public BulkChangeCustomerRatePlanUpdate CustomerRatePlanUpdate { get; set; }
        public CarrierRatePlanUpdate CarrierRatePlanUpdate { get; set; }
    }
}
