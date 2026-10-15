// Moving between the game's downloads: each place is a separate player in its own folder on the site.
mergeInto(LibraryManager.library, {
  BB_GoTo: function (url) { window.location.href = UTF8ToString(url); }
});
