using System.Text;
using Dapper;

namespace ParsingEndpoint.Database;

public sealed record ElementRecord(string AttributeValue, string Html);

public interface IElementRepository
{
    Task InsertAsync(IReadOnlyList<ElementRecord> elements, CancellationToken cancellationToken);
}

public sealed class ElementRepository(IDapperSession session) : IElementRepository
{
    public async Task InsertAsync(IReadOnlyList<ElementRecord> elements, CancellationToken cancellationToken)
    {
        var connection = await session.GetOpenConnectionAsync(cancellationToken);
        var transaction = session.Transaction
            ?? throw new InvalidOperationException("An active transaction is required to insert elements.");

        foreach (var batch in elements.Chunk(500))
        {
            var sql = new StringBuilder("INSERT INTO elements (attribute_value, html) VALUES ");
            var parameters = new DynamicParameters();
            for (var i = 0; i < batch.Length; i++)
            {
                if (i > 0) sql.Append(',');
                sql.Append($"(@attribute{i}, @html{i})");
                parameters.Add($"attribute{i}", batch[i].AttributeValue);
                parameters.Add($"html{i}", batch[i].Html);
            }
            await connection.ExecuteAsync(new CommandDefinition(sql.ToString(), parameters, transaction,
                cancellationToken: cancellationToken));
        }
    }
}
