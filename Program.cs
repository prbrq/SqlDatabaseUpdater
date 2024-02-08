using Microsoft.Extensions.Configuration;
using Npgsql;

namespace SqlDatabaseUpdater;

internal class Program
{
    static void Main(string[] args)
    {
        var builer = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        var configuration = builer.Build();

        var connectionString = configuration.GetConnectionString("Postgres");

        var connection = new NpgsqlConnection(connectionString);
        try
        {
            connection.Open();
        }
        catch (Exception ex) 
        {
            Console.WriteLine(ex.Message);
        }

        Console.Write("\nPress any key to close...");
        Console.ReadKey(false);
    }
}
