using Android.App;
using Com.Layer.Sdk;
using Square.Picasso;
using Android.Content;
using Com.Layer.Messenger.Util;
using System;
using Android.Runtime;
using Com.Layer.Atlas.Provider;
using Com.Layer.Atlas.Util;
using Com.Layer.Atlas.Messagetypes.Text;
using Com.Layer.Atlas.Messagetypes.Threepartimage;
using System.Collections.Generic;
using Com.Layer.Atlas.Util.Picasso.Requesthandlers;

namespace Com.Layer.Messenger
{
    /**
     * App provides static access to a LayerClient and other Atlas and Messenger context, including
     * AuthenticationProvider, ParticipantProvider, Participant, and Picasso.
     *
     * App.Flavor allows build variants to target different environments, such as the Atlas Demo and the
     * open source Rails Identity Provider.  Switch flavors with the Android Studio `Build Variant` tab.
     * When using a flavor besides the Atlas Demo you must manually set your Layer App ID and GCM Sender
     * ID in that flavor's Flavor.java.
     *
     * @see com.layer.messenger.App.Flavor
     * @see com.layer.messenger.flavor.Flavor
     * @see LayerClient
     * @see ParticipantProvider
     * @see Picasso
     * @see AuthenticationProvider
     */
    [Application]
    public class App : Application
    {
        private static Application sInstance;
        private static Flavor sFlavor = new Messenger.Flavor.Flavor();

        private static LayerClient sLayerClient;
        private static IParticipantProvider sParticipantProvider;
        private static IAuthenticationProvider sAuthProvider;
        private static Picasso sPicasso;

		public App (IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
		}

        //==============================================================================================
        // Application Overrides
        //==============================================================================================

        public override void OnCreate()
        {
            base.OnCreate();

            // Enable verbose logging in debug builds
			if (BuildConfig.DEBUG)
            {
                Com.Layer.Atlas.Util.Log.SetAlwaysLoggable (true);
                Messenger.Util.Log.SetAlwaysLoggable(true);
                LayerClient.SetLoggingEnabled(this, true);
            }

            // Allow the LayerClient to track app state
            LayerClient.ApplicationCreated(this);

            sInstance = this;
        }

        public static Application Instance
        {
            get { return sInstance; }
        }


        //==============================================================================================
        // Identity Provider Methods
        //==============================================================================================

        /**
         * Routes the user to the proper Activity depending on their authenticated state.  Returns
         * `true` if the user has been routed to another Activity, or `false` otherwise.
         *
         * @param from Activity to route from.
         * @return `true` if the user has been routed to another Activity, or `false` otherwise.
         */
        public static bool RouteLogin(Activity from)
        {
            return GetAuthenticationProvider().RouteLogin(GetLayerClient(), GetLayerAppId(), from);
        }

        /**
         * Authenticates with the AuthenticationProvider and Layer, returning asynchronous results to
         * the provided callback.
         *
         * @param credentials Credentials associated with the current AuthenticationProvider.
         * @param callback    Callback to receive authentication results.
         */
        public static void Authenticate(Object credentials, IAuthenticationProviderCallback callback)
        {
            LayerClient client = GetLayerClient();
            if (client == null) return;
            String layerAppId = GetLayerAppId();
            if (layerAppId == null) return;
            GetAuthenticationProvider()
                    .SetCredentials(credentials)
                    .SetCallback(callback);
            client.Authenticate();
        }

        /**
         * Deauthenticates with Layer and clears cached AuthenticationProvider credentials.
         *
         * @param callback Callback to receive deauthentication success and failure.
         */
        public static void Deauthenticate(AtlasUtil.IDeauthenticationCallback callback)
        {
            AtlasUtil.Deauthenticate(GetLayerClient(), new DeauthenticationCallback(callback));
        }

        private class DeauthenticationCallback : Java.Lang.Object, AtlasUtil.IDeauthenticationCallback
        {
            private AtlasUtil.IDeauthenticationCallback _callback;

            public DeauthenticationCallback(AtlasUtil.IDeauthenticationCallback callback)
            {
                _callback = callback;
            }

            public void OnDeauthenticationSuccess(LayerClient client)
            {
                GetAuthenticationProvider().SetCredentials(null);
                _callback.OnDeauthenticationSuccess(client);
            }

            public void OnDeauthenticationFailed(LayerClient client, string reason)
            {
                _callback.OnDeauthenticationFailed(client, reason);
            }
        }


        //==============================================================================================
        // Getters / Setters
        //==============================================================================================

        /**
         * Gets or creates a LayerClient, using a default set of LayerClient.Options and flavor-specific
         * App ID and Options from the `generateLayerClient` method.  Returns `null` if the flavor was
         * unable to create a LayerClient (due to no App ID, etc.).
         *
         * @return New or existing LayerClient, or `null` if a LayerClient could not be constructed.
         * @see Flavor#generateLayerClient(Context, LayerClient.Options)
         */
        public static LayerClient GetLayerClient()
        {
            if (sLayerClient == null)
            {
                // Custom options for constructing a LayerClient
                LayerClient.Options options = new LayerClient.Options()

                        /* Fetch the minimum amount per conversation when first authenticated */
                        .InvokeHistoricSyncPolicy(LayerClient.Options.HistoricSyncPolicy.FromLastMessage)
                    
                        /* Automatically download text and ThreePartImage info/preview */
                        .InvokeAutoDownloadMimeTypes(new List<string>{
                                TextCellFactory.MimeType,
                                ThreePartImageUtils.MimeTypeInfo,
                                ThreePartImageUtils.MimeTypePreview
                        });

                // Allow flavor to specify Layer App ID and customize Options.
                sLayerClient = sFlavor.GenerateLayerClient(sInstance, options);

                // Flavor was unable to generate Layer Client (no App ID, etc.)
                if (sLayerClient == null)
                {
                    return null;
                }

                /* Register AuthenticationProvider for handling authentication challenges */
                sLayerClient.RegisterAuthenticationListener(GetAuthenticationProvider());
            }
            return sLayerClient;
        }

        public static string GetLayerAppId()
        {
            return sFlavor.GetLayerAppId();
        }

        public static IParticipantProvider GetParticipantProvider()
        {
            if (sParticipantProvider == null)
            {
                sParticipantProvider = sFlavor.GenerateParticipantProvider(sInstance, GetAuthenticationProvider());
            }
            return sParticipantProvider;
        }

        public static IAuthenticationProvider GetAuthenticationProvider()
        {
            if (sAuthProvider == null)
            {
                sAuthProvider = sFlavor.GenerateAuthenticationProvider(sInstance);

                // If we have cached credentials, try authenticating with Layer
                LayerClient layerClient = GetLayerClient();
                if (layerClient != null && sAuthProvider.HasCredentials()) layerClient.Authenticate();
            }
            return sAuthProvider;
        }

        public static Picasso GetPicasso()
        {
            if (sPicasso == null)
            {
                // Picasso with custom RequestHandler for loading from Layer MessageParts.
                sPicasso = new Picasso.Builder(sInstance)
                        .AddRequestHandler(new MessagePartRequestHandler(GetLayerClient()))
                        .Build();
            }
            return sPicasso;
        }

        /**
         * Flavor is used by Atlas Messenger to switch environments.
         *
         * @see com.layer.messenger.flavor.Flavor
         */
        public interface Flavor
        {
            string GetLayerAppId();

            LayerClient GenerateLayerClient(Context context, LayerClient.Options options);

            IAuthenticationProvider GenerateAuthenticationProvider(Context context);

            IParticipantProvider GenerateParticipantProvider(Context context, IAuthenticationProvider authenticationProvider);
        }
    }
}