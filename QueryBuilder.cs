using Npgsql;
using System.Text;
using System.Text.RegularExpressions;

namespace SqlDatabaseUpdater;

internal class QueryBuilder(NpgsqlConnection connection, string queryPath)
{
    private NpgsqlConnection Connection { get; } = connection;
    private string QueryPath { get; } = GetRealPath(queryPath);
    private List<string> Transactions { get; set; } = [];

    private static readonly char[] PathSeparators = ['\\', '/'];

    private const string ExtendedCommandAttribute = "-- Q ";

    private static string GetRealPath(string rowPath)
    {
        var rowPathParts = rowPath.Split(PathSeparators, StringSplitOptions.None);
        var realPath = string.Join(Path.DirectorySeparatorChar, rowPathParts);
        if (!File.Exists(realPath))
            throw new Exception($"File {GetAbsolutePath(realPath)} was not found");
        return realPath;
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
        var queryLines = GetTextFileLines(QueryPath);

        var set = new TransactionSet();

        Transaction? currentTransaction = null;

        foreach (var line in queryLines)
        {
            if (line.StartsWith("BEGIN;", StringComparison.CurrentCultureIgnoreCase))
            {
                var attributes = line
                    .Split(" -- ", StringSplitOptions.RemoveEmptyEntries)
                    .Last()
                    .Split(" ", StringSplitOptions.RemoveEmptyEntries);

                if (!DateOnly.TryParse(attributes.First(), out var date))
                {
                    throw new Exception("The transaction attribute \"Date\" could not be found");
                }

                if (!int.TryParse(attributes.Last(), out var number))
                {
                    throw new Exception("The transaction attribute \"Date\" could not be found");
                }

                currentTransaction = new Transaction(date, number);
            }
            else
            {
                if (currentTransaction == null)
                {
                    throw new Exception("Attempt to work outside the transaction");
                }

                if (line.Equals("END;", StringComparison.CurrentCultureIgnoreCase))
                {
                    set.Transactions.Add(currentTransaction);
                }
                else
                {
                    if (line.StartsWith(ExtendedCommandAttribute))
                    {
                        if (!ValidateExtendedLine(line, out var validatorMessage))
                        {
                            throw new Exception(validatorMessage);
                        }

                        var extendedCommand = ExtractExtendedCommand(line);
                        foreach (var outputLine in extendedCommand.OutputLines)
                        {
                            currentTransaction.Builder.AppendLine(outputLine);
                        }
                    }
                    else
                    {
                        currentTransaction.Builder.AppendLine(line);
                    }
                }
            }
        }

        var transactionValues = set.Transactions
            .Select(t => t.Builder.ToString())
            .ToList();
        Transactions.AddRange(transactionValues);
    }

    private static bool ValidateExtendedLine(string line, out string message)
    {
        if (!IsBracketsClosed(line))
        {
            message = $"Not all brackets are closed in \"{line}\"";
            return false;
        }

        var lineParts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (lineParts.Length < 2)
        {
            message = "An incomplete sql expression was found";
            return false;
        }

        if (lineParts[1].Equals("UPDATE", StringComparison.CurrentCultureIgnoreCase))
        {
            if (!IsUpdateStatementContainsWhere(lineParts))
            {
                message = "An UPDATE expression without a WHERE was found";
                return true;
            }
        }

        message = "OK";
        return true;

        static bool IsBracketsClosed(string line)
        {
            var openBracketsCounter = 0;
            foreach (var character in line)
            {
                switch (character)
                {
                    case '(':
                        openBracketsCounter++;
                        break;
                    case ')':
                        openBracketsCounter--;
                        break;
                }
            }

            return openBracketsCounter == 0;
        }

        static bool IsUpdateStatementContainsWhere(IEnumerable<string> lineParts)
        {
            return lineParts
                .Any(part => part.Equals("WHERE", StringComparison.CurrentCultureIgnoreCase));
        }
    }

    private static List<string> GetTextFileLines(string queryPath)
    {
        var lines = new List<string>();
        using var stream = File.OpenRead(queryPath);
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        return lines;
    }

    private class TransactionSet
    {
        public readonly List<Transaction> Transactions = [];
    }

    private class Transaction(DateOnly date, int number)
    {
        public DateOnly Date { get; } = date;
        public int Number { get; } = number;
        public StringBuilder Builder { get; } = new();
    }

    private static class ExtendedCommandTypeEnum
    {
        public const string Execute = "@EXEC";
        public const string Read = "@READ";
    }

    private static ExtendedCommand ExtractExtendedCommand(string line)
    {
        var extendedCommandTypes = new[]
        {
            ExtendedCommandTypeEnum.Execute,
            ExtendedCommandTypeEnum.Read,
        };
        foreach (var extendedCommandType in extendedCommandTypes)
        {
            var pattern = $@"{extendedCommandType}\((.*?)\)";
            var match = Regex.Match(line, pattern);
            if (!match.Success) continue;
            var extractedText = match.Groups[1].Value;
            return new ExtendedCommand(GetRealPath(extractedText), match.Value, extendedCommandType, line);
        }

        throw new Exception("The extended command could not be extracted");
    }

    private class ExtendedCommand
    {
        public List<string> OutputLines { get; }

        private string Path { get; }
        private string Value { get; }
        private string Type { get; }
        private string InputLine { get; set; }

        public ExtendedCommand(string path, string value, string type, string line)
        {
            Path = path;
            Value = value;
            Type = type;
            InputLine = line;

            OutputLines = new List<string>();

            switch (Type)
            {
                case ExtendedCommandTypeEnum.Execute:
                {
                    var execLines = GetTextFileLines(Path);
                    OutputLines.AddRange((execLines));

                    break;
                }
                case ExtendedCommandTypeEnum.Read:
                {
                    var fileTextLines = GetTextFileLines(Path);
                    var fileText = string.Join(Environment.NewLine, fileTextLines);
                    var transactionLine = line
                        .Replace(Value, fileText)
                        .Replace(ExtendedCommandAttribute, string.Empty);
                    OutputLines.Add(transactionLine);
                    break;
                }
            }
        }
    }

    private static string GetAbsolutePath(string relativePath)
    {
        if (Path.IsPathRooted(relativePath)) return relativePath;

        var absolutePath = Path.GetFullPath(relativePath);
        return absolutePath;
    }
}