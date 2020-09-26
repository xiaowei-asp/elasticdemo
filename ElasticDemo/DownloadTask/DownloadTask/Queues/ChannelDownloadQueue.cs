using DownloadTask.Dtos;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;

namespace DownloadTask
{
    public sealed class ChannelDownloadQueue : IDownloadQueue
    {
        private readonly Channel<DownloadCommand> _channel =
            Channel.CreateUnbounded<DownloadCommand>();

        public ValueTask EnqueueAsync(DownloadCommand command)
            => _channel.Writer.WriteAsync(command);

        public async IAsyncEnumerable<DownloadCommand> DequeueAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            while (await _channel.Reader.WaitToReadAsync(cancellationToken))
            {
                while (_channel.Reader.TryRead(out var command))
                    yield return command;
            }
        }
    }
}
