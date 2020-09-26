using Outbox.Abstractions;
using Outbox.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace Outbox
{
    public class HttpOutboxPublisher : IOutboxPublisher
    {
        private readonly HttpClient _httpClient;

        public HttpOutboxPublisher(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task PublishAsync(OutboxMessage message, CancellationToken ct)
        {
            var content = new StringContent(
                message.Payload,
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(
                $"/events/{message.MessageType}",
                content,
                ct);

            response.EnsureSuccessStatusCode();
        }
    }
}
