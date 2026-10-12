using UnityEngine;

namespace WormRush.Ad
{
    /// <summary>
    /// Stand-in for the editor and desktop builds: logs what a real network would be told and opens nothing,
    /// so tapping the CTA while testing does not throw a browser window at you.
    /// </summary>
    public sealed class EditorAdNetwork : IAdNetwork
    {
        readonly string _iosUrl;
        readonly string _androidUrl;

        public EditorAdNetwork(string iosUrl, string androidUrl)
        {
            _iosUrl = iosUrl;
            _androidUrl = androidUrl;
        }

        public int OpenStoreCount { get; private set; }

        public void GameReady()
        {
            Debug.Log("[Ad] GameReady");
        }

        public void OpenStore()
        {
            OpenStoreCount++;
            Debug.Log("[Ad] OpenStore #" + OpenStoreCount + "  ios: " + _iosUrl + "  android: " + _androidUrl);
        }

        public void GameEnd()
        {
            Debug.Log("[Ad] GameEnd");
        }
    }
}
