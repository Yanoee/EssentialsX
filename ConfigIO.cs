using Vintagestory.API.Common;

namespace EssentialsX
{
    public static class ConfigIO
    {
        public static T ReadOrCreate<T>(ICoreAPI api, string relativePath, T @default)
        {
            var existing = api.LoadModConfig<T>(relativePath);
            if (existing != null) return existing;

            api.StoreModConfig(@default, relativePath);
            return @default;
        }
    }
}
