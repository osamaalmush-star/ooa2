using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UglyToad.PdfPig;

namespace OAI.Services
{
    public static class DocumentTextExtractor
    {
        public static string Extract(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return string.Empty;

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext switch
            {
                ".pdf" => ExtractPdf(filePath),
                ".docx" => ExtractDocx(filePath),
                ".txt" => File.ReadAllText(filePath),
                ".doc" => string.Empty,
                _ => ExtractPdf(filePath)
            };
        }

        public static string ExtractPdf(string pdfFilePath)
        {
            try
            {
                using var document = PdfDocument.Open(pdfFilePath);
                var sb = new StringBuilder();
                foreach (var page in document.GetPages())
                {
                    if (!string.IsNullOrWhiteSpace(page.Text))
                        sb.AppendLine(page.Text);
                }
                return sb.ToString().Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        public static string ExtractDocx(string docxPath)
        {
            try
            {
                using var zip = ZipFile.OpenRead(docxPath);
                var entry = zip.GetEntry("word/document.xml");
                if (entry == null) return string.Empty;

                using var stream = entry.Open();
                var doc = XDocument.Load(stream);
                XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                var texts = doc.Descendants(w + "t").Select(t => t.Value);
                var raw = string.Join(" ", texts);
                return Regex.Replace(raw, @"\s+", " ").Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        public static bool IsSupported(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext is ".pdf" or ".docx" or ".txt";
        }
    }
}
