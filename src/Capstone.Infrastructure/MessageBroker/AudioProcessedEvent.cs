namespace Capstone.Infrastructure.MessageBroker;

public record AudioProcessedEvent(
    Guid AudioFileId
);
