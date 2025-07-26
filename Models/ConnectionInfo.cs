using System;
using System.ComponentModel;

namespace SochoPutty.Models
{
    public class ConnectionInfo : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _hostname = string.Empty;
        private int _port = 22;
        private string _username = string.Empty;
        private string _password = string.Empty;
        private ConnectionType _connectionType = ConnectionType.SSH;
        private string _privateKeyPath = string.Empty;
        private string _description = string.Empty;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        public string Hostname
        {
            get => _hostname;
            set
            {
                _hostname = value;
                OnPropertyChanged(nameof(Hostname));
            }
        }

        public int Port
        {
            get => _port;
            set
            {
                _port = value;
                OnPropertyChanged(nameof(Port));
            }
        }

        public string Username
        {
            get => _username;
            set
            {
                _username = value;
                OnPropertyChanged(nameof(Username));
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                _password = value;
                OnPropertyChanged(nameof(Password));
            }
        }

        public ConnectionType ConnectionType
        {
            get => _connectionType;
            set
            {
                _connectionType = value;
                OnPropertyChanged(nameof(ConnectionType));
                // SSH가 아닐 때 기본 포트 변경
                if (value == ConnectionType.Telnet && Port == 22)
                    Port = 23;
                else if (value == ConnectionType.SSH && Port == 23)
                    Port = 22;
            }
        }

        public string PrivateKeyPath
        {
            get => _privateKeyPath;
            set
            {
                _privateKeyPath = value;
                OnPropertyChanged(nameof(PrivateKeyPath));
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged(nameof(Description));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public ConnectionInfo Clone()
        {
            return new ConnectionInfo
            {
                Name = this.Name,
                Hostname = this.Hostname,
                Port = this.Port,
                Username = this.Username,
                Password = this.Password,
                ConnectionType = this.ConnectionType,
                PrivateKeyPath = this.PrivateKeyPath,
                Description = this.Description
            };
        }

        public override string ToString()
        {
            return $"{Name} ({Username}@{Hostname}:{Port})";
        }
    }

    public enum ConnectionType
    {
        SSH,
        Telnet,
        Raw,
        Rlogin
    }
} 