using Android.App;
using Android.Content;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using Com.Layer.Atlas;
using Com.Layer.Atlas.Provider;
using Com.Layer.Atlas.Util;
using Com.Layer.Sdk;
using Com.Layer.Sdk.Changes;
using Com.Layer.Sdk.Exceptions;
using Com.Layer.Sdk.Listeners;
using Com.Layer.Sdk.Messaging;
using System.Collections.Generic;

namespace Com.Layer.Messenger
{
    [Activity(WindowSoftInputMode = SoftInput.AdjustResize)]
    public class AppSettingsActivity : BaseActivity, ILayerConnectionListener, ILayerAuthenticationListener, ILayerChangeEventListener, View.IOnLongClickListener {
        /* Account */
        private AtlasAvatar mAvatar;
        private TextView mUserName;
        private TextView mUserState;
        private Button mLogoutButton;

        /* Notifications */
        private Switch mShowNotifications;

        /* Debug */
        private Switch mVerboseLogging;
        private TextView mAppVersion;
        private TextView mAndroidVersion;
        private TextView mAtlasVersion;
        private TextView mLayerVersion;
        private TextView mUserId;

        /* Statistics */
        private TextView mConversationCount;
        private TextView mMessageCount;
        private TextView mUnreadMessageCount;

        /* Rich Content */
        private TextView mDiskUtilization;
        private TextView mDiskAllowance;
        private TextView mAutoDownloadMimeTypes;

        public AppSettingsActivity()
            : base(Resource.Layout.activity_app_settings, Resource.Menu.menu_settings, Resource.String.title_settings, true)
        {
        }

        protected override void OnCreate(Bundle savedInstanceState) {
            base.OnCreate(savedInstanceState);

            // View cache
            mAvatar = FindViewById<AtlasAvatar>(Resource.Id.avatar);
            mUserName = FindViewById<TextView>(Resource.Id.user_name);
            mUserState = FindViewById<TextView>(Resource.Id.user_state);
            mLogoutButton = FindViewById<Button>(Resource.Id.logout_button);
            mShowNotifications = FindViewById<Switch>(Resource.Id.show_notifications_switch);
            mVerboseLogging = FindViewById<Switch>(Resource.Id.logging_switch);
            mAppVersion = FindViewById<TextView>(Resource.Id.app_version);
            mAtlasVersion = FindViewById<TextView>(Resource.Id.atlas_version);
            mLayerVersion = FindViewById<TextView>(Resource.Id.layer_version);
            mAndroidVersion = FindViewById<TextView>(Resource.Id.android_version);
            mUserId = FindViewById<TextView>(Resource.Id.user_id);
            mConversationCount = FindViewById<TextView>(Resource.Id.conversation_count);
            mMessageCount = FindViewById<TextView>(Resource.Id.message_count);
            mUnreadMessageCount = FindViewById<TextView>(Resource.Id.unread_message_count);
            mDiskUtilization = FindViewById<TextView>(Resource.Id.disk_utilization);
            mDiskAllowance = FindViewById<TextView>(Resource.Id.disk_allowance);
            mAutoDownloadMimeTypes = FindViewById<TextView>(Resource.Id.auto_download_mime_types);
            mAvatar.Init(GetParticipantProvider(), GetPicasso());

            // Long-click copy-to-clipboard
            mUserName.SetOnLongClickListener(this);
            mUserState.SetOnLongClickListener(this);
            mAppVersion.SetOnLongClickListener(this);
            mAndroidVersion.SetOnLongClickListener(this);
            mAtlasVersion.SetOnLongClickListener(this);
            mLayerVersion.SetOnLongClickListener(this);
            mUserId.SetOnLongClickListener(this);
            mConversationCount.SetOnLongClickListener(this);
            mMessageCount.SetOnLongClickListener(this);
            mUnreadMessageCount.SetOnLongClickListener(this);
            mDiskUtilization.SetOnLongClickListener(this);
            mDiskAllowance.SetOnLongClickListener(this);
            mAutoDownloadMimeTypes.SetOnLongClickListener(this);

            // Buttons and switches
            mLogoutButton.Click += (sender, args) =>
            {
                SetEnabled(false);
                new AlertDialog.Builder(this)
                        .SetCancelable(false)
                        .SetMessage(Resource.String.alert_message_logout)
                        .SetPositiveButton(Resource.String.alert_button_logout, (sender_, args_) =>
                        {
                            if (Util.Log.IsLoggable(Util.Log.VERBOSE)) {
                                Util.Log.v("Deauthenticating");
                            }
                            ((IDialogInterface) sender_).Dismiss();
                            ProgressDialog progressDialog = new ProgressDialog(this);
                            progressDialog.SetMessage(Resources.GetString(Resource.String.alert_dialog_logout));
                            progressDialog.SetCancelable(false);
                            progressDialog.Show();
                            App.Deauthenticate(new AtlasUtilDeauthenticationCallback(this, progressDialog));
                        })
                        .SetNegativeButton(Resource.String.alert_button_cancel, (sender_, args_) => 
                        {
                            ((IDialogInterface) sender_).Dismiss();
                            SetEnabled(true);
                        })
                        .Show();
                };

            mShowNotifications.CheckedChange += (sender, args) =>
            {
                PushNotificationReceiver.GetNotifications(this).SetEnabled(args.IsChecked);
            };

            mVerboseLogging.CheckedChange += (sender, args) =>
            {
                LayerClient.SetLoggingEnabled(this, args.IsChecked);
                Atlas.Util.Log.SetAlwaysLoggable(args.IsChecked);
                Util.Log.SetAlwaysLoggable(args.IsChecked);
            };
        }

        private class AtlasUtilDeauthenticationCallback : Java.Lang.Object, AtlasUtil.IDeauthenticationCallback
        {
            private AppSettingsActivity _activity;
            private ProgressDialog _progressDialog;

            public AtlasUtilDeauthenticationCallback(AppSettingsActivity appSettingsActivity, ProgressDialog progressDialog)
            {
                _activity = appSettingsActivity;
                _progressDialog = progressDialog;
            }

            public void OnDeauthenticationSuccess(LayerClient client)
            {
                if (Util.Log.IsLoggable(Util.Log.VERBOSE))
                {
                    Util.Log.v("Successfully deauthenticated");
                }
                _progressDialog.Dismiss();
                _activity.SetEnabled(true);
                App.RouteLogin(_activity);
            }

            public void OnDeauthenticationFailed(LayerClient client, string reason)
            {
                if (Util.Log.IsLoggable(Util.Log.ERROR))
                {
                    Util.Log.e("Failed to deauthenticate: " + reason);
                }
                _progressDialog.Dismiss();
                _activity.SetEnabled(true);
                Toast.MakeText(_activity, _activity.GetString(Resource.String.toast_failed_to_deauthenticate, reason), ToastLength.Short).Show();
            }
        }

        protected override void OnResume() {
            base.OnResume();
            GetLayerClient()
                    .RegisterAuthenticationListener(this)
                    .RegisterConnectionListener(this)
                    .RegisterEventListener(this);
            Refresh();
        }

        protected override void OnPause() {
            GetLayerClient()
                    .UnregisterAuthenticationListener(this)
                    .UnregisterConnectionListener(this)
                    .UnregisterEventListener(this);
            base.OnPause();
        }

        public void SetEnabled(bool enabled) {
            RunOnUiThread(() =>
            {
                mLogoutButton.Enabled = enabled;
                mShowNotifications.Enabled = enabled;
                mVerboseLogging.Enabled = enabled;
            });
        }

        private void Refresh() {
            if (!GetLayerClient().IsAuthenticated) return;

            /* Account */
            IParticipant participant = GetParticipantProvider().GetParticipant(GetLayerClient().AuthenticatedUserId);
            mAvatar.SetParticipants(GetLayerClient().AuthenticatedUserId);
            mUserName.Text = participant.Name;
            mUserState.SetText(GetLayerClient().IsConnected ? Resource.String.settings_content_connected : Resource.String.settings_content_disconnected);

            /* Notifications */
            mShowNotifications.Checked = PushNotificationReceiver.GetNotifications(this).IsEnabled();

            /* Debug */
            // enable logging through adb: `adb shell setprop log.tag.LayerSDK VERBOSE`
            bool enabledByEnvironment = Android.Util.Log.IsLoggable("LayerSDK", LogPriority.Verbose);
            mVerboseLogging.Enabled = !enabledByEnvironment;
            mVerboseLogging.Checked = enabledByEnvironment || LayerClient.IsLoggingEnabled;
            mAppVersion.Text = GetString(Resource.String.settings_content_app_version, BuildConfig.VERSION_NAME, BuildConfig.VERSION_CODE);
            mAtlasVersion.Text = AtlasUtil.Version;
            mLayerVersion.Text = LayerClient.Version;
            mAndroidVersion.Text = GetString(Resource.String.settings_content_android_version, Build.VERSION.Release, (int) Build.VERSION.SdkInt);
            mUserId.Text = GetLayerClient().AuthenticatedUserId;
        
            /* Statistics */
            long totalMessages = 0;
            long totalUnread = 0;
            IList<Conversation> conversations = GetLayerClient().Conversations;
            foreach (Conversation conversation in conversations) {
                totalMessages += conversation.TotalMessageCount.IntValue();
                totalUnread += conversation.TotalUnreadMessageCount.IntValue();
            }
            mConversationCount.Text = string.Format("%d", conversations.Count);
            mMessageCount.Text = string.Format("%d", totalMessages);
            mUnreadMessageCount.Text = string.Format("%d", totalUnread);

            /* Rich Content */
            mDiskUtilization.Text = ReadableByteFormat(GetLayerClient().DiskUtilization);
            long allowance = GetLayerClient().DiskCapacity;
            if (allowance == 0) {
                mDiskAllowance.SetText(Resource.String.settings_content_disk_unlimited);
            } else {
                mDiskAllowance.Text = ReadableByteFormat(allowance);
            }
            mAutoDownloadMimeTypes.Text = string.Join("\n", GetLayerClient().AutoDownloadMimeTypes);
        }

        private string ReadableByteFormat(long bytes) {
            long kb = 1024;
            long mb = kb * 1024;
            long gb = mb * 1024;

            double value;
            int suffix;
            if (bytes >= gb) {
                value = (double) bytes / (double) gb;
                suffix = Resource.String.settings_content_disk_gb;
            } else if (bytes >= mb) {
                value = (double) bytes / (double) mb;
                suffix = Resource.String.settings_content_disk_mb;
            } else if (bytes >= kb) {
                value = (double) bytes / (double) kb;
                suffix = Resource.String.settings_content_disk_kb;
            } else {
                value = (double) bytes;
                suffix = Resource.String.settings_content_disk_b;
            }
            return GetString(Resource.String.settings_content_disk_usage, value, GetString(suffix));
        }


        public void OnAuthenticated(LayerClient layerClient, string s) {
            Refresh();
        }

        public void OnDeauthenticated(LayerClient layerClient) {
            Refresh();
        }

        public void OnAuthenticationChallenge(LayerClient layerClient, string s) {

        }

        public void OnAuthenticationError(LayerClient layerClient, LayerException e) {

        }

        public void OnConnectionConnected(LayerClient layerClient) {
            Refresh();
        }

        public void OnConnectionDisconnected(LayerClient layerClient) {
            Refresh();
        }

        public void OnConnectionError(LayerClient layerClient, LayerException e) {

        }

        public void OnChangeEvent(LayerChangeEvent layerChangeEvent) {
            Refresh();
        }

        public bool OnLongClick(View v) {
            if (v is TextView) {
                AtlasUtil.CopyToClipboard(v.Context, Resource.String.settings_clipboard_description, (v as TextView).Text.ToString());
                Toast.MakeText(this, Resource.String.toast_copied_to_clipboard, ToastLength.Short).Show();
                return true;
            }
            return false;
        }
    }
}