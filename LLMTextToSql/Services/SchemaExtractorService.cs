using System;                         // for Console
using System.Collections.Generic;     // for Dictionary<,> and List<>
using System.IO;                      // for File and Path
using System.Text.Json;               // for JsonSerializer
using System.Threading.Tasks;         // for Task
using Npgsql;                         // for NpgsqlConnection

namespace LLMTextToSql.Services
{
    public class SchemaExtractorService
    {
        private readonly string _connectionString;

        public SchemaExtractorService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task ExtractSchemaAsync(string outputPath)
        {
            var schema = new Dictionary<string, List<string>>();

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT c.table_name,
                       c.column_name
                   FROM information_schema.tables AS t
                   JOIN information_schema.columns AS c
                     ON t.table_schema = c.table_schema
                    AND t.table_name   = c.table_name
                  WHERE t.table_schema = 'public'
                    AND t.table_type   = 'BASE TABLE'
               ORDER BY c.table_name, c.ordinal_position;
            ";


            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string tableName = reader.GetString(0);
                string columnName = reader.GetString(1);

                if (!schema.ContainsKey(tableName))
                    schema[tableName] = new List<string>();

                schema[tableName].Add(columnName);
            }

            // Wrap in an object with a "tables" property
            var finalSchema = new { tables = schema };

            // Pretty-print JSON
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(finalSchema, options);

            // Write it out
            // Make sure the directory exists
            var dir = Path.GetDirectoryName(outputPath)!;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(outputPath, json);

            Console.WriteLine($"Schema exported successfully to: {outputPath}");
        }
    }
}
