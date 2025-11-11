using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SochoPutty.Models
{
    public class ChatPeer : INotifyPropertyChanged
    {
        private DateTime _lastOnlineTime;
        private bool _isOnline;

        public string IpAddress { get; set; } = string.Empty;

        public DateTime LastOnlineTime
        {
            get => _lastOnlineTime;
            set
            {
                if (_lastOnlineTime != value)
                {
                    _lastOnlineTime = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsKnown { get; set; }

        public bool IsOnline
        {
            get => _isOnline;
            set
            {
                if (_isOnline != value)
                {
                    _isOnline = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}


