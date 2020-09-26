using System;
using System.Collections.Generic;
using System.Text;

namespace DownloadTask.Dtos
{
    public sealed record DownloadCommand
    {
        public string TaskId { get; init; } = Guid.NewGuid().ToString();
        public string FileName { get; init; } = default!;
        public string FileType { get; init; } = default!;
        public Dictionary<string, object> Parameters { get; init; } = new();
    }
}
