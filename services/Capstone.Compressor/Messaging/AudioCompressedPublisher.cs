using System.Text.Json;
using RabbitMQ.Client;

namespace Capstone.Compressor.Messaging;

public class AudioCompressedPublisher
{
    public async Task PublishAsync(IChannel channel, Guid audioFileId, string storageUrl, string fileName, string routingKey, string exchangeName)
    {
        var message = new AudioCompressedMessage(audioFileId, storageUrl, fileName);
        var body = JsonSerializer.SerializeToUtf8Bytes(message);
        await channel.BasicPublishAsync(exchangeName, routingKey, mandatory: false, body);
    }
}
