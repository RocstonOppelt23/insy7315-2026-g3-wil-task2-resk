namespace RESK.WIL.Models
{
    // A simple message page (registration closed, waiting for approval, ...).
    public class ReskNoticeViewModel
    {
        public string Title { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        // teal, amber or red
        public string Tone { get; set; } = "teal";

        public string Organisation { get; set; } = string.Empty;

        public string SupportEmail { get; set; } = string.Empty;

        public string ContactNumber { get; set; } = string.Empty;
    }


    public class ReskPasswordViewModel
    {
        public string Email { get; set; } = string.Empty;

        // "Minimum 8 characters, with a number and a special character"
        public string Rule { get; set; } = string.Empty;

        public int MinLength { get; set; } = 8;

        // True when the user can't carry on until the password is changed.
        public bool Required { get; set; }

        public string Reason { get; set; } = string.Empty;

        public List<string> Errors { get; set; } = new();
    }


    public class ReskProfileViewModel
    {
        public string Name { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string RoleName { get; set; } = string.Empty;

        public string Initials { get; set; } = string.Empty;

        public string? PhotoUrl { get; set; }

        // The Administrator's profile is fixed.
        public bool IsAdministrator { get; set; }

        public bool InAdminArea { get; set; }

        public bool IsProducer { get; set; }

        public string? Message { get; set; }

        public string? Error { get; set; }
    }
}