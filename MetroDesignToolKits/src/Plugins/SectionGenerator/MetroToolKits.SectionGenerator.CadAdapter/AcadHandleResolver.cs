using Autodesk.AutoCAD.DatabaseServices;
using System;

namespace SectionGenerator.CadAdapter;

internal static class AcadHandleResolver
{
    public static ObjectId Resolve(Database db, string handle)
    {
        if (!TryParseHandle(handle, out Handle h))
            return ObjectId.Null;

        try
        {
            return db.GetObjectId(false, h, 0);
        }
        catch
        {
            return ObjectId.Null;
        }
    }

    private static bool TryParseHandle(string text, out Handle handle)
    {
        handle = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            text = text[2..];

        if (!long.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out long value))
            return false;

        handle = new Handle(value);
        return true;
    }
}

