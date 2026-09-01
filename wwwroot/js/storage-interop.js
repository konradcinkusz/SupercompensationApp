// localStorage access, guarded.
//
// Every method here is wrapped, and not defensively-for-the-sake-of-it: reading
// `window.localStorage` THROWS a SecurityError in a private window and in any browser
// configured to block site data — before a key is even named. An unguarded getItem is
// therefore an unhandled exception on startup for a class of user who has done nothing
// wrong, and the application works perfectly well without persistence.
//
// setItem additionally throws QuotaExceededError when the origin's storage is full.
window.supercompStorage = {
    read: function (key) {
        try {
            return window.localStorage.getItem(key);
        } catch (e) {
            return null;
        }
    },

    write: function (key, value) {
        try {
            window.localStorage.setItem(key, value);
        } catch (e) {
            // Full, or unavailable. Nothing to do and nothing worth interrupting for.
        }
    },

    remove: function (key) {
        try {
            window.localStorage.removeItem(key);
        } catch (e) {
        }
    }
};
