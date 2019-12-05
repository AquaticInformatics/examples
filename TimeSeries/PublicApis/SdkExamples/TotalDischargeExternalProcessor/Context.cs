namespace TotalDischargeExternalProcessor
{
    public class Context
    {
        public string Server { get; set; }
        public string Username { get; set; } = "admin";
        public string Password { get; set; } = "admin";
        public string ConfigPath { get; set; }
        public bool CreateMissingTimeSeries { get; set; } = true;
    }
}
