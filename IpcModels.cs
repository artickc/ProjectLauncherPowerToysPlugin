using System;
using System.Collections.Generic;

namespace ProjectLauncher.Core.IPC
{
    public enum IpcRequestType
    {
        Search,
        Launch,
        Create,
        GetSettings
    }

    public class IpcSearchRequest
    {
        public IpcRequestType Type { get; set; } = IpcRequestType.Search;
        public string Query { get; set; } = string.Empty;
        public int Limit { get; set; } = 20;
        
        // For Launch/Create
        public string TargetIde { get; set; } = string.Empty;
        public string TargetProject { get; set; } = string.Empty;
    }

    public class IpcProjectResult
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string IdeName { get; set; } = string.Empty;
        public string IdeId { get; set; } = string.Empty;
        public string IdeCommand { get; set; } = string.Empty;
        public string IdeIconPath { get; set; } = string.Empty;
        public int Score { get; set; }
        
        // Extended Metadata
        public string? GitUrl { get; set; }
        public List<string> Tags { get; set; } = new();
        public List<string> Languages { get; set; } = new();
        public DateTime? LastOpened { get; set; }
        public string Source { get; set; } = string.Empty;
    }

    public class IpcSearchResponse
    {
        public List<IpcProjectResult> Results { get; set; } = new();
        public bool Success { get; set; } = true;
        public string Message { get; set; } = string.Empty;
        
        // Settings data (populated for GetSettings request)
        public Dictionary<string, string>? IDEKeywords { get; set; }
    }
}
