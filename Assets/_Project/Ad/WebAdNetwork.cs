#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;

namespace WormRush.Ad
{
    /// <summary>
    /// Web build: MRAID <c>mraid.open</c> when the page runs inside an ad container, <c>window.open</c> otherwise.
    /// The JavaScript side is Plugins/WebGL/WormRushAd.jslib; the store is picked there from the user agent,
    /// as in reference/worm-rush-playable.html.
    /// </summary>
    public sealed class WebAdNetwork : IAdNetwork
    {
        [DllImport("__Internal")]
        static extern void WormRush_GameReady();

        [DllImport("__Internal")]
        static extern void WormRush_GameEnd();

        [DllImport("__Internal")]
        static extern void WormRush_OpenStore(string iosUrl, string androidUrl);

        readonly string _iosUrl;
        readonly string _androidUrl;

        public WebAdNetwork(string iosUrl, string androidUrl)
        {
            _iosUrl = iosUrl;
            _androidUrl = androidUrl;
        }

        public void GameReady()
        {
            WormRush_GameReady();
        }

        public void OpenStore()
        {
            WormRush_OpenStore(_iosUrl, _androidUrl);
        }

        public void GameEnd()
        {
            WormRush_GameEnd();
        }
    }
}
#endif
