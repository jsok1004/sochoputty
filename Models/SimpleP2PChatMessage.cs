using System;

namespace SochoPutty.Models
{
    public class SimpleP2PChatMessage
    {
        public string SenderIpAddress { get; set; } = string.Empty;

        public string RecipientIpAddress { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}

