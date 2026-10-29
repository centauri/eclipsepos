using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.IO;
using System.Text;
class Program
{
    static int Main(string[] args)
    {
        int count = 0;
        foreach (var file in args)
        {
            var parser = new TSql170Parser(true);
            using (var input = new StringReader(File.ReadAllText(file, Encoding.GetEncoding(1252))))
            {
                System.Collections.Generic.IList<ParseError> errors;
                parser.Parse(input, out errors);
                foreach (var error in errors) Console.WriteLine(file + ":" + error.Line + " " + error.Message);
                count += errors.Count;
            }
        }
        Console.WriteLine("SQL Server 2025 syntax errors: " + count + ". This does not test SQL execution.");
        return count == 0 && args.Length > 0 ? 0 : 1;
    }
}
