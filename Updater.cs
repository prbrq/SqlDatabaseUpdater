using Npgsql;
using System.Runtime.ConstrainedExecution;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace SqlDatabaseUpdater;

internal class Updater
{
    public NpgsqlConnection Connection { get; }
    public string QueryPath { get; }
    public List<string> Transactions { get; set; }

    private static readonly string extendedCommandAttribute = "-- Q ";
    private static readonly char[] pathSeparators = ['\\', '/'];

    public Updater(NpgsqlConnection connection, string queryPath)
    {
        Connection = connection;
        var qp = GetPlatformPath(queryPath);
        if (!File.Exists(qp)) throw new ArgumentException("File not exists");
        QueryPath = qp;
        Transactions = new List<string>();
    }

    private static string GetPlatformPath(string rowPath)
    {
        var rowPathParts = rowPath.Split(pathSeparators, StringSplitOptions.None);
        return string.Join(Path.DirectorySeparatorChar, rowPathParts);
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

    public void GetTransactions()
    {
        List<string> queryLines = GetTextFileLines(QueryPath);

        if (!IsValid(queryLines))
        {
            throw new Exception("Query is not valid");
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
                    throw new Exception("Attempt to work outside the transaction");
                }
                if (line.Equals("END;", StringComparison.CurrentCultureIgnoreCase))
                {
                    transactions.Add(currentTransaction);
                }
                else
                {
                    if (line.StartsWith(extendedCommandAttribute))
                    {
                        var extendedCommand = ExtractExtendedCommand(line);
                        var filePath = extendedCommand.Path;
                        if (!File.Exists(filePath)) throw new Exception($"File {GetAbsolutePath(filePath)} was not found");
                        if (extendedCommand.Type == ExtendedCommandTypeEnum.Execute)
                        {
                            var execLines = GetTextFileLines(filePath);
                            foreach (var transactionLine in execLines)
                            {
                                currentTransaction.Builder.AppendLine(transactionLine);
                            }
                        }
                        else if (extendedCommand.Type == ExtendedCommandTypeEnum.Read)
                        {
                            var fileTextLines = GetTextFileLines(filePath);
                            var fileText = string.Join(Environment.NewLine, fileTextLines);
                            var transactionLine = line
                                .Replace(extendedCommand.Value, fileText)
                                .Replace(extendedCommandAttribute, string.Empty);
                            currentTransaction.Builder.AppendLine(transactionLine);
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

    private static class ExtendedCommandTypeEnum
    {
        public const string Execute = "@EXEC";
        public const string Read = "@READ";
    }

    private static ExtendedCommand ExtractExtendedCommand(string line)
    {
        var extendedCommandTypes = new string[]
        {
            ExtendedCommandTypeEnum.Execute,
            ExtendedCommandTypeEnum.Read,
        };
        foreach (var extendedCommandType in extendedCommandTypes)
        {
            var pattern = $@"{extendedCommandType}\((.*?)\)";
            Match match = Regex.Match(line, pattern);
            if (match.Success)
            {
                string extractedText = match.Groups[1].Value;
                return new ExtendedCommand(GetPlatformPath(extractedText), match.Value, extendedCommandType);
            }
        }
        throw new Exception("The extended command could not be extracted");
    }

    private class ExtendedCommand(string path, string value, string type)
    {
        public string Path { get; set; } = path;
        public string Value { get; set; } = value;
        public string Type { get; set; } = type;
    }

    private static string GetAbsolutePath(string relativePath)
    {
        if (Path.IsPathRooted(relativePath)) return relativePath;

        string absolutePath = Path.GetFullPath(relativePath);
        return absolutePath;
    }
}
