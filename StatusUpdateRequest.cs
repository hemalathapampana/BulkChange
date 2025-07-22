using Amop.Core.Models.Revio;

namespace AltaworxDeviceBulkChange
{
    public class StatusUpdateRequest<T>
    {
        public string UpdateStatus { get; set; }
        public bool IsIgnoreCurrentStatus { get; set; }
        public int PostUpdateStatusId { get; set; }
        public string AccountNumber { get; set; }
        public T Request { get; set; }
        public BulkChangeAssociateCustomer RevService { get; set; }
        public RevServiceProductCreateModel RevServiceProductCreateModel { get; set; }
        public int IntegrationAuthenticationId { get; set; }
    }
}
