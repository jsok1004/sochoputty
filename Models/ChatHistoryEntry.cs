using System;
using System.Collections.Generic;

namespace SochoPutty.Models
{
    public class ChatHistoryEntry
    {
        public string PeerIpAddress { get; set; } = string.Empty;

        public List<SimpleP2PChatMessage> Messages { get; set; } = new List<SimpleP2PChatMessage>();

        public DateTime LastMessageTime { get; set; } = DateTime.UtcNow;
    }
}


