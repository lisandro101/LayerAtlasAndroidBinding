using System;

namespace Com.Layer.Messenger.Util
{
    /**
     * Unified Log class used by Atlas Messenger classes that maintains similar signatures to
     * `Android.Util.Log`. Logs are tagged with `Atlas`.
     */
    public class Log
    {
        public const string TAG = "AtlasMessenger";

        // Makes IDE auto-completion easy
		public const int VERBOSE = (int) Android.Util.LogPriority.Verbose;
		public const int DEBUG = (int) Android.Util.LogPriority.Debug;
		public const int INFO = (int) Android.Util.LogPriority.Info;
		public const int WARN = (int) Android.Util.LogPriority.Warn;
		public const int ERROR = (int) Android.Util.LogPriority.Error;

        private static volatile bool sAlwaysLoggable = false;

        /**
         * Returns `true` if the provided log level is loggable either through environment options or
         * a previous call to setAlwaysLoggable().
         *
         * @param level Log level to check.
         * @return `true` if the provided log level is loggable.
         * @see #setAlwaysLoggable(boolean)
         */
        public static bool IsLoggable(int level) {
            return sAlwaysLoggable || Android.Util.Log.IsLoggable(TAG, (Android.Util.LogPriority) level);
        }

        public static void SetAlwaysLoggable(bool alwaysOn) {
            sAlwaysLoggable = alwaysOn;
        }

        public static void v(string message) {
			Android.Util.Log.Verbose(TAG, message);
        }

        public static void v(string message, Exception error) {
			Android.Util.Log.Verbose(TAG, message, error);
        }

        public static void d(string message) {
			Android.Util.Log.Debug(TAG, message);
        }

        public static void d(string message, Exception error) {
			Android.Util.Log.Debug(TAG, message, error);
        }

        public static void i(string message) {
			Android.Util.Log.Info(TAG, message);
        }

        public static void i(string message, Exception error) {
			Android.Util.Log.Info(TAG, message, error);
        }

        public static void w(string message) {
			Android.Util.Log.Warn(TAG, message);
        }

        public static void w(string message, Exception error) {
			Android.Util.Log.Warn(TAG, message, error);
        }

        public static void w(Exception error) {
			Android.Util.Log.Warn(TAG, Java.Lang.Throwable.FromException(error));
        }

        public static void e(string message) {
			Android.Util.Log.Error(TAG, message);
        }

        public static void e(string message, Exception error) {
			Android.Util.Log.Error(TAG, message, error);
        }
    }
}