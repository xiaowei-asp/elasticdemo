using System;
using System.Collections.Generic;
using System.Text;

namespace DownloadTask.FileStorage
{
    public sealed class LocalFileStorage : IFileStorage
    {
        private readonly string _root;

        public LocalFileStorage(string root)
        {
            _root = root;
            Directory.CreateDirectory(_root);
        }

        public async Task<string> SaveAsync(
            string fileName,
            Stream content,
            CancellationToken cancellationToken)
        {
            var path = Path.Combine(_root, fileName);

            await using var fs = File.Create(path);
            await content.CopyToAsync(fs, cancellationToken);

            return $"/downloads/{fileName}";
        }
    }

}
