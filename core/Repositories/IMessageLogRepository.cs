namespace Mass.Transit.Outbox.Repo.Replicate.core.Repositories;

public interface IMessageLogRepository
{
    Task<long> InsertGetIdAsync(MessageLog entity, CancellationToken cancellationToken);

    Task UpdateAsync(string invoiceNumber, Guid correlationId, LogMessageUpdate entity, CancellationToken cancellationToken);
    Task<IEnumerable<MessageLog>> GetAllAsync(string invoiceNumber, CancellationToken cancellationToken);
    Task<MessageLog?> GetAsync(string invoiceNumber, Guid correlationId, CancellationToken cancellationToken);
}