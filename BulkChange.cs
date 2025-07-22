namespace AltaworxDeviceBulkChange
{
    public class BulkChange
    {
        public long Id { get; set; }
        public int TenantId { get; set; }
        public string Status { get; set; }
        public int ChangeRequestTypeId { get; set; }
        public string ChangeRequestType { get; set; }
        public int ServiceProviderId { get; set; }
        public string ServiceProvider { get; set; }
        public int IntegrationId { get; set; }
        public string Integration { get; set; }
        public int PortalTypeId { get; set; }
        public string CreatedBy { get; set; }
    }
}
