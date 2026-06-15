using System.Buffers.Binary;

namespace HPToy.Core.Numbers;

public static class FloatUtility
{
    public static bool IsFloatEqualWithAccuracy(float arg0, float arg1, int accuracy)
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteSingleLittleEndian(b, arg0);
        var arg0Int = BinaryPrimitives.ReadInt32LittleEndian(b);

        BinaryPrimitives.WriteSingleLittleEndian(b, arg1);
        var arg1Int = BinaryPrimitives.ReadInt32LittleEndian(b);

        if (arg0Int < 0) arg0Int = unchecked(int.MinValue - arg0Int);
        if (arg1Int < 0) arg1Int = unchecked(int.MinValue - arg1Int);

        var diff = arg0Int > arg1Int ? arg0Int - arg1Int : arg1Int - arg0Int;
        return diff < accuracy;
    }

    public static bool IsFloatNull(float f) => IsFloatEqualWithAccuracy(f, 0.0f, 16);

    public static bool IsFloatDiffLessThan(float f0, float f1, float maxDiff) =>
        Math.Abs(f0 - f1) < maxDiff;

    public static bool IsFloatDiffLessThanPerc(float f0, float f1, float perc)
    {
        if (perc < 0.0f) perc = 0.0f;
        return Math.Abs(f0 - f1) / Math.Max(Math.Abs(f0), Math.Abs(f1)) < perc / 100.0f;
    }
}
