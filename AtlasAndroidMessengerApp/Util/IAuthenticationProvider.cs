using Com.Layer.Sdk.Listeners;
using Com.Layer.Sdk;
using Android.App;

namespace Com.Layer.Messenger.Util
{
	/**
	 * AuthenticationProvider implementations authenticate users with backend "Identity Providers."
	 *
	 * @param <Tcredentials> Session credentials for this AuthenticationProvider used to resume an
	 *                       authenticated session.
	 */
	public interface IAuthenticationProvider : ILayerAuthenticationListenerBackgroundThreadWeak 
	{
	    /**
	     * Sets this AuthenticationProvider's credentials.  Credentials should be cached to handle
	     * future authentication challenges.  When `credentials` is `null`, the cached credentials
	     * should be cleared.
	     *
	     * @param credentials Credentials to cache.
	     * @return This AuthenticationProvider.
	     */
	    IAuthenticationProvider SetCredentials(object credentials);


	    /**
	     * Returns `true` if this AuthenticationProvider has cached credentials, or `false` otherwise.
	     *
	     * @return `true` if this AuthenticationProvider has cached credentials, or `false` otherwise.
	     */
	    bool HasCredentials();

	    /**
	     * Sets the authentication callback for reporting authentication success and failure.
	     *
	     * @param callback Callback to receive authentication success and failure.
	     * @return This AuthenticationProvider.
	     */
	    IAuthenticationProvider SetCallback(IAuthenticationProviderCallback callback);

	    /**
	     * Routes the user to a login screen if required.  If routing, return `true` and start the
	     * desired login Activity.
	     *
	     * @param layerClient
	     * @param layerAppId
	     * @param from
	     * @return
	     */
	    bool RouteLogin(LayerClient layerClient, string layerAppId, Activity from);
	}

    /**
    * Callback for handling authentication success and failure.
    */
    public interface IAuthenticationProviderCallback
    {
        void OnSuccess(IAuthenticationProvider provider, string userId);

        void OnError(IAuthenticationProvider provider, string error);
    }

    public interface IAuthenticationProvider<Tcredentials> : IAuthenticationProvider
    {
        IAuthenticationProvider<Tcredentials> SetCredentials(Tcredentials credentials);

        IAuthenticationProvider<Tcredentials> SetCallback(IAuthenticationProviderCallback<Tcredentials> callback);
    }

    public interface IAuthenticationProviderCallback<Tcredentials> : IAuthenticationProviderCallback
    {
        void OnSuccess(IAuthenticationProvider<Tcredentials> provider, string userId);

        void OnError(IAuthenticationProvider<Tcredentials> provider, string error);
    }
}