using System.Text.Json;
using RabbitMQ.Client;

namespace Capstone.Transcriber.Messaging;

public class AudioProcessedPublisher
{
    public async Task PublishAsync(IChannel channel, Guid audioFileId, string routingKey, string exchangeName)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(new AudioProcessedMessage(audioFileId));
        await channel.BasicPublishAsync(exchangeName, routingKey, mandatory: false, body);
    }
}
