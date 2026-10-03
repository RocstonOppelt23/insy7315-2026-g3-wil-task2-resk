using System.Text.Encodings.Web;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using RESK.WIL.Services;

namespace RESK.WIL.ViewComponents
{
    [ViewComponent(Name = "ReskCategoryOptions")]
    public class ReskCategoryOptionsViewComponent : ViewComponent
    {
        private readonly IWebHostEnvironment _environment;

        public ReskCategoryOptionsViewComponent(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public IViewComponentResult Invoke(string? selected = null, string? part = null)
        {
            var all = ReskCategoryStore.All(_environment.ContentRootPath)
                .Where(c => c.AvailableToProducers)
                .ToList();

            HtmlEncoder encoder = HtmlEncoder.Default;

            // Hint part: list categories that require supporting documents.
            if (string.Equals(part, "hint", StringComparison.OrdinalIgnoreCase))
            {
                var need = all.Where(c => c.RequireSupportingDocuments).ToList();
                if (!need.Any())
                    return Content(string.Empty);

                var names = need.Select(c => encoder.Encode(c.Name));
                string inner = "<small class=\"form-hint\">Supporting documents are required for: " + string.Join(", ", names) + ".</small>";
                return new HtmlContentViewComponentResult(new HtmlString(inner));
            }

            // Otherwise: render <option> elements.
            var sb = new StringBuilder();

            bool anySelected = false;
            foreach (var c in all)
            {
                bool isSelected = ReskCategoryStore.SameName(c.Name, selected);
                if (isSelected)
                    anySelected = true;

                sb.Append("<option value=\"");
                sb.Append(encoder.Encode(c.Name));
                sb.Append("\"");
                if (isSelected)
                    sb.Append(" selected");
                sb.Append(">");
                sb.Append(encoder.Encode(c.Name));
                sb.Append("</option>");
            }

            // If a selected value exists but didn't match any category, keep it as an option
            if (!string.IsNullOrWhiteSpace(selected) && !anySelected)
            {
                sb.Append("<option value=\"");
                sb.Append(encoder.Encode(selected));
                sb.Append("\" selected>");
                sb.Append(encoder.Encode(selected));
                sb.Append("</option>");
            }

            return new HtmlContentViewComponentResult(new HtmlString(sb.ToString()));
        }
    }
}
