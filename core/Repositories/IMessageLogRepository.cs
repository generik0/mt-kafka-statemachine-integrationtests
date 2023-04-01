namespace Mass.Transit.Outbox.Repo.Replicate.core.Repositories;

public interface IMessageLogRepository
{
    Task<long> InsertGetIdAsync(MessageLog entity, CancellationToken cancellationToken);

}