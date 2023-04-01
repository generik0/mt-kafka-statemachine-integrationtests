using FastMember;
using Microsoft.EntityFrameworkCore;
using SqlKata;
using SqlKata.Compilers;
using SqlKata.Execution;
using System.Data;
using System.Data.Common;

namespace Mass.Transit.Outbox.Repo.Replicate.core.Repositories;

public class MessageLogRepository : IMessageLogRepository
{
    private readonly QueryFactory _queryFactory;
    private readonly DbConnection _dbConnection;
    private readonly string? _tableName;

    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    public MessageLogRepository(MyDbContext dbContext)
    {
        var compiler = new PostgresCompiler();

        _dbConnection = dbContext.Database.GetDbConnection();
        _tableName = dbContext.Model.FindEntityType(typeof (MessageLog))!.GetTableName();
        _queryFactory = new QueryFactory(_dbConnection, compiler);
    }

    public async Task<long> InsertGetIdAsync(MessageLog entity, CancellationToken cancellationToken)
    {
        if (_dbConnection.State != ConnectionState.Open)
        {
            await _dbConnection.OpenAsync(cancellationToken);
        }
        var reader = ObjectAccessor.Create(entity);
        var data = TypeAccessor.Create(typeof(MessageLog)).GetMembers()
            .Where(x => !string.Equals(nameof(entity.Id), x.Name, StringComparison.InvariantCulture))
            .Select(prop => new KeyValuePair<string, object>(prop.Name, reader[prop.Name]));

        return await  _queryFactory.Query(_tableName).InsertGetIdAsync<long>(data, cancellationToken: cancellationToken);
    }
}