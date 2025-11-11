using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SochoPutty.Models
{
    public class ChatHistoryStore
    {
        private readonly string _storagePath;

        public ChatHistoryStore()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var folder = Path.Combine(appData, "SochoPutty");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            _storagePath = Path.Combine(folder, "chat_history.json");
        }

        public List<ChatHistoryEntry> Load()
        {
            try
            {
                if (!File.Exists(_storagePath))
                {
                    return new List<ChatHistoryEntry>();
                }

                var json = File.ReadAllText(_storagePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new List<ChatHistoryEntry>();
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var entries = JsonSerializer.Deserialize<List<ChatHistoryEntry>>(json, options);
                return entries ?? new List<ChatHistoryEntry>();
            }
            catch
            {
                return new List<ChatHistoryEntry>();
            }
        }

        public void Save(IEnumerable<ChatHistoryEntry> entries)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                var json = JsonSerializer.Serialize(entries, options);
                File.WriteAllText(_storagePath, json);
            }
            catch
            {
                // 저장 실패는 무시 (로깅 추가 가능)
            }
        }
    }
}


