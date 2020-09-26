using System;
using System.Collections.Generic;
using System.Text;

namespace DownloadTask.Results
{
    public sealed record DownloadResult
    {
        public string TaskId { get; init; } = default!;
        public string DownloadUrl { get; init; } = default!;
        public DateTime ExpireAt { get; init; }
    }
}
