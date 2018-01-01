using System;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;

namespace SimpleQueryTool
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length != 2 || String.IsNullOrWhiteSpace(args[0]) || String.IsNullOrWhiteSpace(args[1]))
            {
                Console.Error.WriteLine("Usage: SimpleQueryTool <connectionstring_name> <query>");
                return 1;
            }
            if(!TryGetConnectionStringAndFactory(args[0], out string connectionString, out DbProviderFactory factory))
            {
                Console.Error.WriteLine("Connection string not found in the configuration.");
                return 1;
            }
            try
            {
                using(DbConnection connection = factory.CreateConnection())
                {
                    connection.ConnectionString = connectionString;
                    DbCommand command = connection.CreateCommand();
                    command.CommandText = args[1];
                    connection.Open();
                    using (DbDataReader reader = command.ExecuteReader(CommandBehavior.CloseConnection))
                    {
                        do
                        {
                            PrintReader(reader);
                            Console.WriteLine();
                        } while (reader.NextResult());
                    }
                    connection.Close();
                }
            }
            catch(Exception e)
            {
#if DEBUG
                Console.Error.WriteLine($"An error occurred:\n{e.ToString()}");
                return 1;
#else
                Console.Error.WriteLine($"An error occurred: {e.Message}");
                return 1;
#endif
            }
            return 0;
        }

        private static void PrintReader(DbDataReader reader)
        {
            bool isFirstRow = true;
            while (reader.Read())
            {
                if (isFirstRow)
                {
                    isFirstRow = false;
                    for (int i = 0; i < reader.VisibleFieldCount; i++)
                    {
                        if (i != 0)
                            Console.Write("\t");

                        Console.Write(reader.GetName(i));
                    }
                    Console.WriteLine();
                }
                for (int i = 0; i < reader.VisibleFieldCount; i++)
                {
                    if (i != 0)
                        Console.Write("\t");

                    var value = reader.GetValue(i);
                    Console.Write(FormatValue(value));
                }
                Console.WriteLine();
            }
        }

        private static string FormatValue(object value)
        {
            if (value == null || value == DBNull.Value)
                return "null";

            if (value is Array arrayValue)
                return FormatArray(arrayValue);

            // Here some better attempt to format the value might happen in practice but ToString() should work for now.
            return value.ToString();
        }

        private static string FormatArray(Array array)
        {
            StringBuilder sb = new StringBuilder();
            int[] indices = new int[array.Rank];
            int currentDimIndex = 0;
            AppendValues(sb, array, ref currentDimIndex, indices);
            sb.Length = sb.Length - 1;
            return sb.ToString();
        }

        private static void AppendValues(StringBuilder sb, Array array, ref int currentDimIndex, int[] indices)
        {
            sb.Append("[");
            for (int i = array.GetLowerBound(currentDimIndex); i <= array.GetUpperBound(currentDimIndex); i++)
            {
                indices[currentDimIndex] = i;
                if (currentDimIndex < array.Rank - 1)
                {
                    currentDimIndex++;
                    AppendValues(sb, array, ref currentDimIndex, indices);
                }
                else
                {
                    sb.Append(FormatValue(array.GetValue(indices)));
                    sb.Append(',');
                }
            }
            currentDimIndex--;
            sb.Length = sb.Length - 1;
            sb.Append("],");
        }

        private static bool TryGetConnectionStringAndFactory(string connectionStringName, out string connectionString, out DbProviderFactory factory)
        {
            if (TryGetConnectionStringSettings(connectionStringName, out ConnectionStringSettings settings))
            {
                connectionString = settings.ConnectionString;
                factory = DbProviderFactories.GetFactory(settings.ProviderName);
                return true;
            }
            connectionString = null;
            factory = null;
            return false;
        }

        private static bool TryGetConnectionStringSettings(string connectionStringName, out ConnectionStringSettings settings)
        {
            ConnectionStringSettingsCollection settingsCollection = ConfigurationManager.ConnectionStrings;
            if (settingsCollection != null)
            {
                foreach (ConnectionStringSettings cs in settingsCollection)
                {
                    if (cs.Name == connectionStringName)
                    {
                        settings = cs;
                        return true;
                    }
                }
            }
            settings = null;
            return false;
        }
    }
}
