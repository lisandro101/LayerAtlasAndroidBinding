using Android.Content;
using Com.Layer.Atlas.Provider;
using Com.Layer.Messenger.Util;
using Com.Layer.Sdk;

namespace Com.Layer.Messenger.Flavor
{
    public class Flavor : App.Flavor
    {
        // Set your Layer App ID from your Layer developer dashboard to bypass the QR-Code scanner.
        private const string LAYER_APP_ID = null;
        private const string GCM_SENDER_ID = "748607264448";

        private string mLayerAppId;


        //==============================================================================================
        // Layer App ID (from LAYER_APP_ID constant or set by QR-Code scanning AppIdScanner Activity
        //==============================================================================================

        public string GetLayerAppId() {
            // In-memory cached App ID?
            if (mLayerAppId != null) {
                return mLayerAppId;
            }

            // Constant App ID?
            if (LAYER_APP_ID != null) {
#pragma warning disable CS0162 // Unreachable code detected
                if (Log.IsLoggable(Log.VERBOSE))
                {
#pragma warning restore CS0162 // Unreachable code detected
                    Log.v("Using constant `App.LAYER_APP_ID` App ID: " + LAYER_APP_ID);
                }
                mLayerAppId = LAYER_APP_ID;
                return mLayerAppId;
            }

            // Saved App ID?
            string saved = App.Instance
                    .GetSharedPreferences("layerAppId", FileCreationMode.Private)
                    .GetString("layerAppId", null);
            if (saved == null) return null;
            if (Log.IsLoggable(Log.VERBOSE)) Log.v("Loaded Layer App ID: " + saved);
            mLayerAppId = saved;
            return mLayerAppId;
        }

        /**
         * Sets the current Layer App ID, and saves it for use next time (to bypass QR code scanner).
         *
         * @param appId Layer App ID to use when generating a LayerClient.
         */
        internal static void SetLayerAppId(string appId) {
            appId = appId.Trim();
            if (Log.IsLoggable(Log.VERBOSE)) Log.v("Saving Layer App ID: " + appId);
            App.Instance.GetSharedPreferences("layerAppId", FileCreationMode.Private).Edit()
                    .PutString("layerAppId", appId).Commit();
        }


        //==============================================================================================
        // Generators
        //==============================================================================================

        public LayerClient GenerateLayerClient(Context context, LayerClient.Options options) {
            // If no App ID is set yet, return `null`; we'll launch the AppIdScanner to get one.
            string appId = GetLayerAppId();
            if (appId == null) return null;

            options.InvokeGoogleCloudMessagingSenderId(GCM_SENDER_ID);
            return LayerClient.NewInstance(context, appId, options);
        }

        public IParticipantProvider GenerateParticipantProvider(Context context, IAuthenticationProvider authenticationProvider) {
            return new DemoParticipantProvider(context).SetLayerAppId(GetLayerAppId());
        }

        public IAuthenticationProvider GenerateAuthenticationProvider(Context context) {
            return new DemoAuthenticationProvider(context);
        }
    }
}