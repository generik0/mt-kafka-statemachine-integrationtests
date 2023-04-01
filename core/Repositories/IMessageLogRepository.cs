namespace Mass.Transit.Outbox.Repo.Replicate.core.Repositories;

public interface IMessageLogRepository
{
    Task<long> InsertGetIdAsync(MessageLog entity, CancellationToken cancellationToken);

    Task UpdateAsync(string invoiceNumber, Guid correlationId, LogMessageUpdate entity, CancellationToken cancellationToken);
}