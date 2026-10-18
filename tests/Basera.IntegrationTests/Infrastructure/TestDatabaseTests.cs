using Npgsql;
using Xunit.Abstractions;

namespace Basera.IntegrationTests.Infrastructure;

[Collection(DatabaseCollection.Name)]
public sealed class TestDatabaseTests(TestDatabase database, ITestOutputHelper output)
{
    [Fact]
    public async Task Connection_string_points_at_the_test_database()
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT current_database()", connection);

        var name = (string?)await command.ExecuteScalarAsync();

        output.WriteLine($"Connected to {name} ({(database.IsLocalServer ? "local server" : "container")}).");
        name.Should().Be(database.DatabaseName);
        if (database.IsLocalServer)
        {
            name.Should().MatchRegex("^basera_test_[0-9a-f]{8}$");
        }
    }
}
