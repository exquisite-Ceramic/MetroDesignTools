namespace MetroToolKits.SectionGenerator.App.Abstractions;

public interface ILayerNameProvider
{
    IEnumerable<string> GetAllLayerNames();
}
