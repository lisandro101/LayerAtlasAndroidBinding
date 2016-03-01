using Android.App;
using Android.Content;
using Android.OS;
using Android.Support.V7.App;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using Com.Layer.Messenger.Util;

namespace Com.Layer.Messenger.Flavor
{
    [Activity(WindowSoftInputMode = SoftInput.StateAlwaysVisible | SoftInput.AdjustResize)]
    public class DemoLoginActivity : AppCompatActivity
    {
        EditText mName;

        protected override void OnCreate(Bundle savedInstanceState) {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.providerdemo_activity_login_demo);
            Android.Support.V7.App.ActionBar actionBar = SupportActionBar;
            if (actionBar != null) actionBar.Hide();

            mName = FindViewById<EditText>(Resource.Id.name);
            mName.ImeOptions = ImeAction.Done;
            mName.SetOnEditorActionListener(new NameTextViewOnEditorActionListener(this));
        }

        private class NameTextViewOnEditorActionListener : Java.Lang.Object, TextView.IOnEditorActionListener
        {
            private DemoLoginActivity _activity;

            public NameTextViewOnEditorActionListener(DemoLoginActivity demoLoginActivity)
            {
                _activity = demoLoginActivity;
            }

            public bool OnEditorAction(TextView v, ImeAction actionId, KeyEvent event_) {
                if (actionId == ImeAction.Done || (event_ != null && event_.KeyCode == Keycode.Enter)) {
                    string name = _activity.mName.Text.ToString().Trim();
                    if (string.IsNullOrEmpty(name)) return true;
                    _activity.Login(name);
                    return true;
                }
                return false;
            }
        }

        protected override void OnResume() {
            base.OnResume();
            mName.Enabled = true;
        }

        private void Login(string name)
        {
            mName.Enabled = false;
            ProgressDialog progressDialog = new ProgressDialog(this);
            progressDialog.SetMessage(Resources.GetString(Resource.String.login_dialog_message));
            progressDialog.Show();
            App.Authenticate(new DemoAuthenticationProvider.Credentials(App.GetLayerAppId(), name), new AuthenticationProviderCallback(this, name, progressDialog));
        }

        private class AuthenticationProviderCallback : IAuthenticationProviderCallback<DemoAuthenticationProvider.Credentials>
        {
            private DemoLoginActivity _activity;
            private string _name;
            private ProgressDialog _progressDialog;

            public AuthenticationProviderCallback(DemoLoginActivity demoLoginActivity, string name, ProgressDialog progressDialog)
            {
                _activity = demoLoginActivity;
                _name = name;
                _progressDialog = progressDialog;
            }

            public void OnSuccess(IAuthenticationProvider provider, string userId)
            {
                OnSuccess(provider as IAuthenticationProvider<DemoAuthenticationProvider.Credentials>, userId);
            }

            public void OnSuccess(IAuthenticationProvider<DemoAuthenticationProvider.Credentials> provider, string userId)
            {
                _progressDialog.Dismiss();
                if (Util.Log.IsLoggable(Util.Log.VERBOSE))
                {
                    Util.Log.v("Successfully authenticated as `" + _name + "` with userId `" + userId + "`");
                }
                Intent intent = new Intent(_activity, typeof(ConversationsListActivity));
                intent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.ClearTask | ActivityFlags.NewTask);
                _activity.StartActivity(intent);
            }

            public void OnError(IAuthenticationProvider provider, string error)
            {
                OnError(provider as IAuthenticationProvider<DemoAuthenticationProvider.Credentials>, error);
            }

            public void OnError(IAuthenticationProvider<DemoAuthenticationProvider.Credentials> provider, string error)
            {
                _progressDialog.Dismiss();
                if (Util.Log.IsLoggable(Util.Log.ERROR))
                {
                    Util.Log.e("Failed to authenticate as `" + _name + "`: " + error);
                }
                _activity.RunOnUiThread(() =>
                {
                    Toast.MakeText(_activity, error, ToastLength.Long).Show();
                    _activity.mName.Enabled = true;
                });
            }
        }
    }
}