mergeInto(LibraryManager.library, {
  ExitBrowserFullscreen: function () {
    try {
      if (document.fullscreenElement) {
        document.exitFullscreen();
      } else if (document.webkitFullscreenElement) {
        document.webkitExitFullscreen();
      } else if (document.mozFullScreenElement) {
        document.mozCancelFullScreen();
      } else if (document.msFullscreenElement) {
        document.msExitFullscreen();
      }
    } catch (e) {
      // 何もしない（ブラウザ/埋め込み条件で失敗することがある）
    }
  }
});
