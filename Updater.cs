using Npgsql;
using System.Text;
using System.Text.RegularExpressions;

namespace SqlDatabaseUpdater;
internal class Updater
{
    public NpgsqlConnection Connection { get; }
    public string QueryPath { get; }

    private readonly string extendedCommandAttribute = "-- Q ";
    public List<string> Transactions { get; set; } = new List<string>();

    public Updater(NpgsqlConnection connection, string queryPath)
    {
        Connection = connection;
        if (!File.Exists(queryPath)) throw new ArgumentException("File not exists");
        QueryPath = queryPath;
    }

    public void ExecuteTransactions()
    {
        if (Transactions == null) throw new Exception("The list of transactions is empty");
        foreach (var transaction in Transactions)
        {
            var dbTransaction = Connection.BeginTransaction();
            var dbCommand = Connection.CreateCommand();
            dbCommand.CommandText = transaction;
            dbCommand.Transaction = dbTransaction;
            dbCommand.ExecuteNonQuery();
            dbTransaction.Commit();
        }
    }

    public bool TryParse(out string message)
    {
        List<string> queryLines = GetTextFileLines(QueryPath);

        if (!IsValid(queryLines))
        {
            message = "Query is not valid";
            return false;
        }

        var transactions = new List<Transaction>();

        Transaction? currentTransaction = null;

        foreach (var line in queryLines)
        {
            if (line.Equals("BEGIN;", StringComparison.CurrentCultureIgnoreCase))
            {
                currentTransaction = new Transaction();
            }
            else
            {
                if (currentTransaction == null)
                {
                    message = "Attempt to work outside the transaction";
                    return false;
                }
                if (line.Equals("END;", StringComparison.CurrentCultureIgnoreCase))
                {
                    transactions.Add(currentTransaction);
                }
                else
                {
                    if (line.StartsWith(extendedCommandAttribute))
                    {
                        if (TryExtractCommand(line, ExtendedCommandEnum.Execute, out string filePath))
                        {
                            if (!File.Exists(filePath)) continue;
                            var execLines = GetTextFileLines(filePath);
                            foreach (var transactionLine in execLines)
                            {
                                currentTransaction.Builder.AppendLine(transactionLine);
                            }
                        }
                        else if (TryExtractCommand(line, ExtendedCommandEnum.Read, out string textPath))
                        {
                            if (!File.Exists(textPath)) continue;
                            var replacedCommand = $"{ExtendedCommandEnum.Read}({textPath})";
                            var fileTextLines = GetTextFileLines(textPath);
                            var fileText = string.Join(Environment.NewLine, fileTextLines);
                            var transactionLine = line
                                .Replace(replacedCommand, fileText)
                                .Replace(extendedCommandAttribute, string.Empty);
                            currentTransaction.Builder.AppendLine(transactionLine);
                        }
                        else
                        {
                            message = "Attempting to use an attribute without an extended command";
                            return false;
                        }
                    }
                    else
                    {
                        currentTransaction.Builder.AppendLine(line);
                    }
                }
            }
        }

        foreach (var transaction in transactions)
        {
            var transactionQuery = transaction.Builder.ToString();
            if (transactionQuery != null) Transactions.Add(transactionQuery);
        }

        message = "Success";
        return true;
    }

    private static List<string> GetTextFileLines(string queryPath)
    {
        var lines = new List<string>();
        using var stream = File.OpenRead(queryPath);
        using var reader = new StreamReader(stream);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            lines.Add(line);
        }
        return lines;
    }

    private static bool IsValid(List<string> lines)
    {
        // TODO: Добавить различные проверки на валидность
        return true;
    }

    private class Transaction
    {
        public int Number { get; set; }
        public StringBuilder Builder { get; set; }
        public static int Count { get; private set; } = 0;

        public Transaction()
        {
            Builder = new StringBuilder();
            Count++;
        }
    }

    private static class ExtendedCommandEnum
    {
        public const string Execute = "@EXEC";
        public const string Read = "@READ";
    }

    private static bool TryExtractCommand(string line, string extendedCommand, out string result)
    {
        var pattern = $@"{extendedCommand}\((.*?)\)";
        Match match = Regex.Match(line, pattern);
        if (match.Success)
        {
            string extractedText = match.Groups[1].Value;
            result = extractedText;
            return true;
        }
        else
        {
            result = string.Empty;
            return false;
        }
    }
}
