namespace MetroToolKits.SectionGenerator.App.Abstractions;

/// <summary>
/// 块删除服务接口 - 从图纸中删除指定句柄的块参照
/// </summary>
public interface IBlockEraseService
{
    void EraseBlock(string blockHandle);
}
