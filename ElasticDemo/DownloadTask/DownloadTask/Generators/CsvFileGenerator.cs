using DownloadTask.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace DownloadTask
{
    public sealed class CsvFileGenerator : IFileGenerator
    {
        public Task<Stream> GenerateAsync(
            DownloadCommand command,
            CancellationToken cancellationToken)
        {
            var ms = new MemoryStream();
            var writer = new StreamWriter(ms);

            writer.WriteLine("Id,Name");
            writer.WriteLine("1,Test");

            writer.Flush();
            ms.Position = 0;

            return Task.FromResult<Stream>(ms);
        }
    }
}
