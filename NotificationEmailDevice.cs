using System;
using System.Collections.Generic;
using System.Text;

namespace AltaworxDeviceBulkChange.Models
{
    public class NotificationEmailDevice
    {
        public int Id { get; set; }
        public string MSISDN { get; set; }
        public string Customer { get; set; }
        public string Username { get; set; }
    }
}
