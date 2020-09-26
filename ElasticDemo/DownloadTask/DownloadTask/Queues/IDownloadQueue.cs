using DownloadTask.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace DownloadTask
{
    public interface IDownloadQueue
    {
        ValueTask EnqueueAsync(DownloadCommand command);
        IAsyncEnumerable<DownloadCommand> DequeueAsync(
            CancellationToken cancellationToken);
    }
}
