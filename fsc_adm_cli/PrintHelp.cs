using fsc_adm_cli.Operations;

namespace fsc_adm_cli
{
    public static class PrintHelp
    {
        public static void Print(string appName, string version, string description, ManagerOperation managerOperation)
        {
            Console.WriteLine($"{appName} v{version}");
            Console.WriteLine(description);
            Console.WriteLine();
            Console.WriteLine("Global options:");
            Console.WriteLine("  --service-url <url>    Base service URL (default: https://localhost:5770)");
            Console.WriteLine("  --admin-conf <path>    Path to admin.conf (default: auto-detect)");
            Console.WriteLine();
            Console.WriteLine("Operations:");

            foreach (var op in managerOperation.Operations)
            {
                Console.WriteLine($"  {op.Key,-20} {op.Description}");
            }

            Console.WriteLine();
            Console.WriteLine("Usage details:");
            foreach (var op in managerOperation.Operations)
            {
                Console.WriteLine();
                Console.WriteLine(op.Usage);
            }
        }
    }
}
