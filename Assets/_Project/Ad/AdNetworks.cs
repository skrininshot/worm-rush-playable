using WormRush.Config;

namespace WormRush.Ad
{
    /// <summary>Picks the network for the current platform. The one place that knows the concrete classes.</summary>
    public static class AdNetworks
    {
        public static IAdNetwork Create(PlayableConfig config)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new WebAdNetwork(config.StoreUrlIos, config.StoreUrlAndroid);
#else
            return new EditorAdNetwork(config.StoreUrlIos, config.StoreUrlAndroid);
#endif
        }
    }
}
