using DownloadTask.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace DownloadTask
{
    public interface IFileGenerator
    {
        Task<Stream> GenerateAsync(
            DownloadCommand command,
            CancellationToken cancellationToken);
    }
}
