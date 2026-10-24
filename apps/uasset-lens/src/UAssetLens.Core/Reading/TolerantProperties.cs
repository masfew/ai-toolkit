using UAssetAPI;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.UnrealTypes;

namespace UAssetLens.Core.Reading;

// UAssetAPI registers every PropertyData subclass in assemblies that reference it, and ours register
// after its own, so these replace its readers for the same property types.
//
// UEFN saves are newer than UAssetAPI 1.1.0 knows. Text values carry 4 extra bytes, and a few
// container values (mostly Verse item entities) don't parse. UAssetAPI never skips to the end the
// property tag gives, so one bad value misreads every property after it and the whole export is lost.
// These readers always finish at the tag's stated end, so a bad value costs only that value.

internal static class TagEnd
{
    public static bool CanSkip(AssetBinaryReader reader, bool includeHeader, long length) =>
        includeHeader && length > 0 && !reader.Asset.HasUnversionedProperties &&
        reader.Asset.ObjectVersionUE5 >= ObjectVersionUE5.PROPERTY_TAG_COMPLETE_TYPE_NAME;
}

public class LensTextPropertyData : TextPropertyData
{
    public LensTextPropertyData(FName name) : base(name) { }
    public LensTextPropertyData() { }

    public bool Unreadable { get; private set; }

    public override void Read(AssetBinaryReader reader, bool includeHeader, long leng1, long leng2 = 0, PropertySerializationContext serializationContext = PropertySerializationContext.Normal)
    {
        if (!TagEnd.CanSkip(reader, includeHeader, leng1))
        {
            base.Read(reader, includeHeader, leng1, leng2, serializationContext);
            return;
        }
        ReadEndPropertyTag(reader);
        var start = reader.BaseStream.Position;
        try { base.Read(reader, false, leng1, leng2, serializationContext); }
        catch (Exception) { Unreadable = true; }
        reader.BaseStream.Position = start + leng1;
    }
}

public class LensArrayPropertyData : ArrayPropertyData
{
    public LensArrayPropertyData(FName name) : base(name) { }
    public LensArrayPropertyData() { }

    public bool Unreadable { get; private set; }

    public override void Read(AssetBinaryReader reader, bool includeHeader, long leng1, long leng2 = 0, PropertySerializationContext serializationContext = PropertySerializationContext.Normal)
    {
        var before = reader.BaseStream.Position;
        try { base.Read(reader, includeHeader, leng1, leng2, serializationContext); }
        catch (Exception) when (TagEnd.CanSkip(reader, includeHeader, leng1))
        {
            reader.BaseStream.Position = before;
            ReadEndPropertyTag(reader);
            reader.BaseStream.Position += leng1;
            Value = [];
            Unreadable = true;
        }
    }
}

public class LensSetPropertyData : SetPropertyData
{
    public LensSetPropertyData(FName name) : base(name) { }
    public LensSetPropertyData() { }

    public bool Unreadable { get; private set; }

    public override void Read(AssetBinaryReader reader, bool includeHeader, long leng1, long leng2 = 0, PropertySerializationContext serializationContext = PropertySerializationContext.Normal)
    {
        var before = reader.BaseStream.Position;
        try { base.Read(reader, includeHeader, leng1, leng2, serializationContext); }
        catch (Exception) when (TagEnd.CanSkip(reader, includeHeader, leng1))
        {
            reader.BaseStream.Position = before;
            ReadEndPropertyTag(reader);
            reader.BaseStream.Position += leng1;
            Value = [];
            Unreadable = true;
        }
    }
}

public class LensMapPropertyData : MapPropertyData
{
    public LensMapPropertyData(FName name) : base(name) { }
    public LensMapPropertyData() { }

    public bool Unreadable { get; private set; }

    public override void Read(AssetBinaryReader reader, bool includeHeader, long leng1, long leng2 = 0, PropertySerializationContext serializationContext = PropertySerializationContext.Normal)
    {
        var before = reader.BaseStream.Position;
        try { base.Read(reader, includeHeader, leng1, leng2, serializationContext); }
        catch (Exception) when (TagEnd.CanSkip(reader, includeHeader, leng1))
        {
            reader.BaseStream.Position = before;
            ReadEndPropertyTag(reader);
            reader.BaseStream.Position += leng1;
            Value = new();
            Unreadable = true;
        }
    }
}
