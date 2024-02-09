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
            var updater = new Updater(connection, ".\\Query.sql");
            if (updater.TryParse(out string message))
            {
                updater.ExecuteTransactions();
            }
            else
            {
                Console.WriteLine(message);
            }
            connection.Close();
        }
        catch (Exception ex) 
        {
            Console.WriteLine(ex.Message);
        }

        Console.Write(Environment.NewLine + "Press any key to close...");
        Console.ReadKey(false);
    }
}
