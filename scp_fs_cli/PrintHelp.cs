using scp_fs_cli.Operations;

namespace scp_fs_cli
{
    public static class PrintHelp
    {
        public static void Print(string appName, string description, ManagerOperation managerOperation)
        {
            Console.WriteLine("Description:");
            Console.WriteLine($"  {description}");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine($"  {appName} [command] [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --version       Show version information");
            Console.WriteLine("  -?, -h, --help  Show help and usage information");
            Console.WriteLine();
            Console.WriteLine("Commands:");

            foreach (var operation in managerOperation.Operations)
                Console.WriteLine($"  {operation.UsageSignature,-18} {operation.Description}");
        }
    }
}
