using MetroToolKits.Foundation.Cad.Layering.Services;
using MetroToolKits.SectionGenerator.App.Abstractions;

namespace MetroToolKits.SectionGenerator.Infrastructure.Services;

public sealed class CadLayerNameProvider : ILayerNameProvider
{
    private readonly ILayerService _layerService;

    public CadLayerNameProvider(ILayerService layerService)
    {
        _layerService = layerService;
    }

    public IEnumerable<string> GetAllLayerNames()
        => _layerService.GetAllLayerNames();
}
