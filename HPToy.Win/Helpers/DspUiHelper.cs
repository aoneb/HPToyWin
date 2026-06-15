using HPToy.Core.Objects;

namespace HPToy.Win.Helpers;

internal static class DspUiHelper
{
    public static int BassTrebleDbToInt(byte db) => unchecked((sbyte)db);

    public static byte IntToBassTrebleDb(int db)
    {
        if (db > 18) db = 18;
        if (db < -18) db = -18;
        return unchecked((byte)db);
    }

    public static void SetBassDb(BassTrebleChannel ch, int db)
    {
        ch.SetBassDb(IntToBassTrebleDb(db));
    }

    public static void SetTrebleDb(BassTrebleChannel ch, int db)
    {
        ch.SetTrebleDb(IntToBassTrebleDb(db));
    }
}
