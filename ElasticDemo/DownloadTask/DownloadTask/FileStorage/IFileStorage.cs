using System;
using System.Collections.Generic;
using System.Text;

namespace DownloadTask.FileStorage
{
    public interface IFileStorage
    {
        Task<string> SaveAsync(
            string fileName,
            Stream content,
            CancellationToken cancellationToken);
    }
}
