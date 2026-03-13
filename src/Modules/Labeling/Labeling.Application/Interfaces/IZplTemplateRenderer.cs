using Labeling.Application.Models;
using Labeling.Domain.Entities;

namespace Labeling.Application.Interfaces;

public interface IZplTemplateRenderer
{
    string RenderProductLabel(ProductLabelData data, Printer printer);
}

