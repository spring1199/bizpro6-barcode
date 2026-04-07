using BarTenderClone.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BarTenderClone.Services
{
    /// <summary>
    /// Implementation of template persistence service with tenant-aware folder isolation
    /// </summary>
    public class TemplateService : ITemplateService
    {
        private const string FILE_EXTENSION = ".btl";
        private static readonly string ROOT_TEMPLATES_FOLDER =
            System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BarTenderClone", "Templates");
        private const string DEFAULT_TENANT_FOLDER = "_default";
        private readonly ISessionService _sessionService;

        public TemplateService(ISessionService sessionService)
        {
            _sessionService = sessionService;
        }

        public string GetTemplatesDirectory()
        {
            var tenantFolder = GetTenantFolderName();
            var templatesPath = Path.Combine(ROOT_TEMPLATES_FOLDER, tenantFolder);

            if (!Directory.Exists(templatesPath))
            {
                Directory.CreateDirectory(templatesPath);
            }

            return templatesPath;
        }

        public IEnumerable<string> GetTemplateNames()
        {
            var dir = GetTemplatesDirectory();
            return Directory.GetFiles(dir, $"*{FILE_EXTENSION}")
                            .Select(Path.GetFileNameWithoutExtension)
                            .Where(n => !string.IsNullOrEmpty(n))
                            .OrderBy(n => n)
                            .Cast<string>();
        }

        public void DeleteTemplate(string name)
        {
            var filePath = Path.Combine(GetTemplatesDirectory(), $"{name}{FILE_EXTENSION}");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        public async Task SaveTemplateAsync(
            LabelTemplate template,
            IEnumerable<LabelElement> elements,
            double widthInches,
            double heightInches,
            string name)
        {
            try
            {
                var fileName = $"{name}{FILE_EXTENSION}";
                var filePath = Path.Combine(GetTemplatesDirectory(), fileName);

                var dto = new LabelTemplateDto
                {
                    Name = name,
                    Width = template.Width,
                    Height = template.Height,
                    WidthInches = widthInches,
                    HeightInches = heightInches,
                    Elements = elements.Select(e => new LabelElementDto
                    {
                        X = e.X,
                        Y = e.Y,
                        Width = e.Width,
                        Height = e.Height,
                        Content = e.Content,
                        FieldName = e.FieldName,
                        Type = e.Type,
                        FontSize = e.FontSize,
                        IsBold = e.IsBold
                    }).ToList()
                };

                var json = JsonConvert.SerializeObject(dto, Formatting.Indented);
                await File.WriteAllTextAsync(filePath, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save template: {ex.Message}", ex);
            }
        }

        public async Task<(LabelTemplate template, List<LabelElement> elements, double widthInches, double heightInches)> LoadTemplateAsync(string name)
        {
            try
            {
                var filePath = Path.Combine(GetTemplatesDirectory(), $"{name}{FILE_EXTENSION}");

                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Template '{name}' not found");
                }

                var json = await File.ReadAllTextAsync(filePath);
                var dto = JsonConvert.DeserializeObject<LabelTemplateDto>(json);

                if (dto == null)
                {
                    throw new InvalidDataException("Failed to deserialize template");
                }

                var template = new LabelTemplate
                {
                    Name = dto.Name,
                    Width = dto.Width,
                    Height = dto.Height
                };

                var elements = dto.Elements.Select(e => new LabelElement
                {
                    X = e.X,
                    Y = e.Y,
                    Width = e.Width,
                    Height = e.Height,
                    Content = e.Content,
                    FieldName = e.FieldName,
                    Type = e.Type,
                    FontSize = e.FontSize,
                    IsBold = e.IsBold,
                    IsSelected = false
                }).ToList();

                return (template, elements, dto.WidthInches, dto.HeightInches);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load template: {ex.Message}", ex);
            }
        }

        private string GetTenantFolderName()
        {
            var tenancyName = _sessionService.TenancyName;
            if (string.IsNullOrWhiteSpace(tenancyName))
                return DEFAULT_TENANT_FOLDER;

            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(tenancyName
                .Trim()
                .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
                .ToArray());

            // Block path traversal: replace ".." sequences that could escape the templates root
            sanitized = sanitized.Replace("..", "__", StringComparison.Ordinal);

            // Cap length to avoid MAX_PATH issues on Windows (leave headroom for root + filename)
            if (sanitized.Length > 64)
                sanitized = sanitized[..64];

            return string.IsNullOrWhiteSpace(sanitized) ? DEFAULT_TENANT_FOLDER : sanitized;
        }
    }
}
