namespace SCP.StorageFSC.Security
{
    public sealed class AdminBootstrapConfig
    {
        public string Name { get; set; } = "Administrator";
        public string Key { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
