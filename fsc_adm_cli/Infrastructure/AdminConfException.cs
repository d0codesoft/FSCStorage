namespace fsc_adm_cli.Infrastructure
{
    public sealed class AdminConfException : Exception
    {
        public AdminConfException(string message)
            : base(message)
        {
        }

        public AdminConfException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
