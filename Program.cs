using Microsoft.Extensions.Configuration;
using Npgsql;

namespace SqlDatabaseUpdater;

internal class Program
{
    static void Main(string[] args)
    {
        var filePath = args.ElementAtOrDefault(0);
        if (filePath == null)
        {
            Console.WriteLine("You must provide the path to the file");
            return;
        }

        var builer = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        var configuration = builer.Build();

        var connectionString = configuration.GetConnectionString("Postgres");

        var connection = new NpgsqlConnection(connectionString);
        try
        {
            connection.Open();
            var updater = new Updater(connection, filePath);
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
    }
}
