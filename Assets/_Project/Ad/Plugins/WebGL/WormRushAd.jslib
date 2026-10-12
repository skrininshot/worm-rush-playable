// Bridge between WebAdNetwork.cs and the page the playable runs in.
// MRAID containers hand the click to the host app; a plain page gets window.open.
mergeInto(LibraryManager.library, {

  WormRush_GameReady: function () {
    // Some networks (e.g. Mintegral) look for a global the creative calls once it is playable.
    try { if (typeof window.gameReady === 'function') window.gameReady(); } catch (e) {}
  },

  WormRush_GameEnd: function () {
    try { if (typeof window.gameEnd === 'function') window.gameEnd(); } catch (e) {}
  },

  WormRush_OpenStore: function (iosUrlPtr, androidUrlPtr) {
    var iosUrl = UTF8ToString(iosUrlPtr);
    var androidUrl = UTF8ToString(androidUrlPtr);
    var url = /iPhone|iPad|iPod/i.test(navigator.userAgent) ? iosUrl : androidUrl;

    try {
      if (window.mraid && typeof mraid.open === 'function') { mraid.open(url); return; }
    } catch (e) {}

    // Unity polls input from requestAnimationFrame, so this often runs outside the browser's user-gesture
    // window and the popup blocker eats the call. If that happens, retry on the next pointerup / click.
    var opened = null;
    try { opened = window.open(url, '_blank'); } catch (e) {}
    if (!opened) {
      var retry = function () {
        window.removeEventListener('pointerup', retry, true);
        window.removeEventListener('click', retry, true);
        try { window.open(url, '_blank'); } catch (e) {}
      };
      window.addEventListener('pointerup', retry, true);
      window.addEventListener('click', retry, true);
    }
  }
});
