using Android.App;
using Android.Content;
using Com.Layer.Messenger.Util;
using Com.Layer.Sdk;
using Com.Layer.Sdk.Exceptions;
using Java.Net;
using Org.Json;
using System;
using System.IO;
using System.Text;

namespace Com.Layer.Messenger.Flavor
{
    public class DemoAuthenticationProvider : Java.Lang.Object, IAuthenticationProvider<DemoAuthenticationProvider.Credentials>
    {
        private readonly ISharedPreferences mPreferences;
        private IAuthenticationProviderCallback<Credentials> mCallback;

        public DemoAuthenticationProvider(Context context) {
            mPreferences = context.GetSharedPreferences(typeof(DemoAuthenticationProvider).Name, FileCreationMode.Private);
        }

        public IAuthenticationProvider SetCredentials(object credentials)
        {
            return this.SetCredentials(credentials as Credentials);
        }

        public IAuthenticationProvider<Credentials> SetCredentials(Credentials credentials) {
            if (credentials == null) {
                mPreferences.Edit().Clear().Commit();
                return this;
            }
            mPreferences.Edit()
                    .PutString("appId", credentials.GetLayerAppId())
                    .PutString("name", credentials.GetUserName())
                    .Commit();
            return this;
        }

        public bool HasCredentials() {
            return mPreferences.Contains("appId");
        }

        public IAuthenticationProvider SetCallback(IAuthenticationProviderCallback callback)
        {
            return this.SetCallback(callback as IAuthenticationProviderCallback<Credentials>);
        }

        public IAuthenticationProvider<Credentials> SetCallback(IAuthenticationProviderCallback<Credentials> callback) {
            mCallback = callback;
            return this;
        }

        public void OnAuthenticated(LayerClient layerClient, string userId) {
            if (Log.IsLoggable(Log.VERBOSE)) Log.v("Authenticated with Layer, user ID: " + userId);
            layerClient.Connect();
            if (mCallback != null) {
                mCallback.OnSuccess(this, userId);
            }
        }

        public void OnDeauthenticated(LayerClient layerClient) {
            if (Log.IsLoggable(Log.VERBOSE)) Log.v("Deauthenticated with Layer");
        }

        public void OnAuthenticationChallenge(LayerClient layerClient, string nonce) {
            if (Log.IsLoggable(Log.VERBOSE)) Log.v("Received challenge: " + nonce);
            RespondToChallenge(layerClient, nonce);
        }

        public void OnAuthenticationError(LayerClient layerClient, LayerException e) {
            string error = "Failed to authenticate with Layer: " + e.Message;
            if (Log.IsLoggable(Log.ERROR)) Log.e(error, e);
            if (mCallback != null) {
                mCallback.OnError(this, error);
            }
        }

        public bool RouteLogin(LayerClient layerClient, string layerAppId, Activity from) {
            if (layerAppId == null) {
                // No App ID: must scan from QR code.
                if (Log.IsLoggable(Log.VERBOSE)) Log.v("Routing to QR Code scanning Activity");
                Intent intent = new Intent(from, typeof(DemoAtlasIdScannerActivity));
                intent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.ClearTask | ActivityFlags.NewTask);
                from.StartActivity(intent);
                return true;
            }
            if (layerClient != null && !layerClient.IsAuthenticated) {
                if (HasCredentials()) {
                    // Use the cached AuthenticationProvider credentials to authenticate with Layer.
                    if (Log.IsLoggable(Log.VERBOSE)) Log.v("Using cached credentials to authenticate");
                    layerClient.Authenticate();
                } else {
                    // App ID, but no user: must authenticate.
                    if (Log.IsLoggable(Log.VERBOSE)) Log.v("Routing to login Activity");
                    Intent intent = new Intent(from, typeof(DemoLoginActivity));
                    intent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.ClearTask | ActivityFlags.NewTask);
                    from.StartActivity(intent);
                    return true;
                }
            }
            if (Log.IsLoggable(Log.VERBOSE)) Log.v("No authentication routing needed");
            return false;
        }

        private void RespondToChallenge(LayerClient layerClient, string nonce) {
            Credentials credentials = new Credentials(mPreferences.GetString("appId", null), mPreferences.GetString("name", null));
            if (credentials.GetUserName() == null || credentials.GetLayerAppId() == null) {
                if (Log.IsLoggable(Log.WARN)) {
                    Log.w("No stored credentials to respond to challenge with");
                }
                return;
            }

            try {
                // Post request
                string url = "https://layer-identity-provider.herokuapp.com/apps/" + credentials.GetLayerAppId() + "/atlas_identities";
                HttpURLConnection connection = (HttpURLConnection) new URL(url).OpenConnection();
                connection.DoInput = true;
                connection.DoOutput = true;
                connection.RequestMethod = "POST";
                connection.SetRequestProperty("Content-Type", "application/json");
                connection.SetRequestProperty("Accept", "application/json");
                connection.SetRequestProperty("X_LAYER_APP_ID", credentials.GetLayerAppId());

                // Credentials
                JSONObject rootObject = new JSONObject()
                        .Put("nonce", nonce)
                        .Put("name", credentials.GetUserName());

                connection.SetRequestProperty("Content-Type", "application/json; charset=UTF-8");

                Stream os = connection.OutputStream;
                var bytes = Encoding.UTF8.GetBytes(rootObject.ToString());
                os.Write(bytes, 0, bytes.Length);
                os.Close();

                // Handle failure
                HttpStatus statusCode = connection.ResponseCode;
                if (statusCode != HttpStatus.Ok && statusCode != HttpStatus.Created) {
                    string error = string.Format("Got status %d when requesting authentication for '%s' with nonce '%s' from '%s'",
                            statusCode, credentials.GetUserName(), nonce, url);
                    if (Log.IsLoggable(Log.ERROR)) Log.e(error);
                    if (mCallback != null) mCallback.OnError(this, error);
                    return;
                }

                // Parse response
                Stream input = connection.InputStream;
                string result = CUtil.StreamToString(input);
                input.Close();
                connection.Disconnect();
                JSONObject json = new JSONObject(result);
                if (json.Has("error")) {
                    string error = json.GetString("error");
                    if (Log.IsLoggable(Log.ERROR)) Log.e(error);
                    if (mCallback != null) mCallback.OnError(this, error);
                    return;
                }

                // Answer authentication challenge.
                string identityToken = json.OptString("identity_token", null);
                if (Log.IsLoggable(Log.VERBOSE)) Log.v("Got identity token: " + identityToken);
                layerClient.AnswerAuthenticationChallenge(identityToken);
            } catch (Exception e) {
                string error = "Error when authenticating with provider: " + e.Message;
                if (Log.IsLoggable(Log.ERROR)) Log.e(error, e);
                if (mCallback != null) mCallback.OnError(this, error);
            }
        }

        public class Credentials
        {
            private string mLayerAppId;
            private string mUserName;

            public Credentials(Android.Net.Uri layerAppId, string userName) {
                _Init(layerAppId == null ? null : layerAppId.LastPathSegment, userName);
            }

            public Credentials(string layerAppId, string userName) {
                _Init(layerAppId, userName);
            }

            private void _Init(string layerAppId, string userName) {
                mLayerAppId = layerAppId == null ? null : (layerAppId.Contains("/") ? layerAppId.Substring(layerAppId.LastIndexOf("/") + 1) : layerAppId);
                mUserName = userName;
            }

            public string GetUserName() {
                return mUserName;
            }

            public string GetLayerAppId() {
                return mLayerAppId;
            }
        }
    }
}