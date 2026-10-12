namespace WormRush.Ad
{
    /// <summary>
    /// Everything the playable needs from an ad network. Each network's quirks (MRAID, ExitApi, Mintegral callbacks)
    /// live behind this; adding a network means adding one class and one line in <see cref="AdNetworks"/>.
    /// </summary>
    public interface IAdNetwork
    {
        /// <summary>The first frame is up and the creative is playable.</summary>
        void GameReady();

        /// <summary>The player asked for the store: the CTA button or a tap on the end card.</summary>
        void OpenStore();

        /// <summary>The creative reached its end card.</summary>
        void GameEnd();
    }
}
