using BarTenderClone.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BarTenderClone.Services
{
    /// <summary>
    /// Service for template persistence (save/load)
    /// </summary>
    public interface ITemplateService
    {
        /// <summary>
        /// Saves a template by name (App-Managed Storage)
        /// </summary>
        Task SaveTemplateAsync(
            LabelTemplate template,
            IEnumerable<LabelElement> elements,
            double widthInches,
            double heightInches,
            string name);

        /// <summary>
        /// Loads a template by name
        /// </summary>
        Task<(LabelTemplate template, List<LabelElement> elements, double widthInches, double heightInches)> LoadTemplateAsync(string name);

        /// <summary>
        /// Gets list of all saved template names
        /// </summary>
        IEnumerable<string> GetTemplateNames();

        /// <summary>
        /// Deletes a template by name
        /// </summary>
        void DeleteTemplate(string name);

        /// <summary>
        /// Gets the default templates directory
        /// </summary>
        string GetTemplatesDirectory();
    }
}

