mergeInto(LibraryManager.library, {
  $MoonDeliveryStorageSync: {
    syncing: false,
    pending: false,

    flush: function () {
      if (typeof FS === 'undefined' || typeof FS.syncfs !== 'function') {
        console.warn('Moon Delivery: IndexedDB filesystem is unavailable.');
        return;
      }

      if (MoonDeliveryStorageSync.syncing) {
        MoonDeliveryStorageSync.pending = true;
        return;
      }

      MoonDeliveryStorageSync.syncing = true;
      FS.syncfs(false, function (error) {
        MoonDeliveryStorageSync.syncing = false;

        if (error) {
          console.error('Moon Delivery: IndexedDB synchronization failed.', error);
        }

        if (MoonDeliveryStorageSync.pending) {
          MoonDeliveryStorageSync.pending = false;
          MoonDeliveryStorageSync.flush();
        }
      });
    }
  },

  MoonDelivery_SyncFileSystem__deps: ['$MoonDeliveryStorageSync'],
  MoonDelivery_SyncFileSystem: function () {
    MoonDeliveryStorageSync.flush();
  }
});
