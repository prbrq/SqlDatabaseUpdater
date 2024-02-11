using Microsoft.Extensions.Configuration;
using Npgsql;

namespace SqlDatabaseUpdater;

internal static class Program
{
    private static void Main(string[] args)
    {
        var filePath = args.ElementAtOrDefault(0);

        if (filePath == null)
        {
            Console.WriteLine("You must provide the path to the file");
            return;
        }

        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("Config.json", optional: false, reloadOnChange: true);

        IConfigurationRoot configuration;
        try
        {
            configuration = builder.Build();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return;
        }

        var connectionString = configuration.GetConnectionString("Postgres");

        var connection = new NpgsqlConnection(connectionString);
        try
        {
            connection.Open();
            var queryBuilder = new QueryBuilder(connection, filePath);
            queryBuilder.GetTransactions();
            queryBuilder.ExecuteTransactions();
            connection.Close();
            Console.WriteLine("\nThe query is executed!\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}