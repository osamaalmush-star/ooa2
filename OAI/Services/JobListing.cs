namespace OAI.Services
{
    public class JobListing
    {
        public string Title { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Salary { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string ApplyUrl { get; set; } = string.Empty;
        public string? HrEmail { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public bool IsOpen { get; set; } = true;
        public bool RequiresNationality { get; set; }
    }
}
