using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Gms.Common;
using Android.OS;
using Android.Support.V4.App;
using Android.Support.V7.App;
using Android.Views;
using Android.Widget;
using System.Threading;

namespace Com.Layer.Messenger.Flavor
{
    [Activity]
    public class DemoAtlasIdScannerActivity : AppCompatActivity
    {
        private const string PERMISSION = Manifest.Permission.Camera;
        public const int PERMISSION_REQUEST_CODE = 21;

        AppIdScanner mAppIdScanner;
        private int mFoundAppIdFlag = 0;

        protected override void OnCreate(Bundle bundle) {
            base.OnCreate(bundle);
            SetContentView(Resource.Layout.providerdemo_activity_app_id_scanner);
            SetTitle(Resource.String.title_app_id_scanner);
        }

        protected override void OnResume() {
            base.OnResume();
            if (HasPermission()) {
                StartScanner();
            } else {
                RequestPermission();
            }
        }

        protected override void OnPause() {
            base.OnPause();
            if (HasPermission()) GetAppIdScanner().Stop();
        }

        protected override void OnDestroy() {
            base.OnDestroy();
            if (HasPermission()) GetAppIdScanner().Release();
        }

        private bool HasPermission() {
            return ActivityCompat.CheckSelfPermission(this, PERMISSION) == Permission.Granted;
        }

        /**
         * Dynamically add AppIdScanner to layout because dynamic permissions seem to break when added
         * ahead of time (onRequestPermissionsResult is never called).
         */
        private AppIdScanner GetAppIdScanner() {
            if (mAppIdScanner == null) {
                AppIdScanner scanner = new AppIdScanner(this);
                FrameLayout.LayoutParams layoutParams = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
                scanner.SetAppIdCallback(new AppIdScannerCallback(this));
                FindViewById<FrameLayout>(Resource.Id.app_id_scanner_layout).AddView(scanner, 0, layoutParams);
                mAppIdScanner = scanner;
            }
            return mAppIdScanner;
        }

        private class AppIdScannerCallback : Java.Lang.Object, AppIdScanner.AppIdCallback
        {
            private DemoAtlasIdScannerActivity _activity;

            public AppIdScannerCallback(DemoAtlasIdScannerActivity demoAtlasIdScannerActivity)
            {
                _activity = demoAtlasIdScannerActivity;
            }

            public void OnLayerAppIdScanned(AppIdScanner scanner, string layerAppId)
            {
                if (1 == Interlocked.CompareExchange(ref _activity.mFoundAppIdFlag, 1, 0)) return;
                if (Util.Log.IsLoggable(Util.Log.VERBOSE))
                {
                    Util.Log.v("Found App ID: " + layerAppId);
                }
                Flavor.SetLayerAppId(layerAppId);
                Intent intent = new Intent(_activity, typeof(DemoLoginActivity));
                intent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.ClearTask | ActivityFlags.NewTask);
                _activity.StartActivity(intent);
                if (!_activity.IsFinishing) _activity.Finish();
            }
        }

        private void RequestPermission() {
            if (Util.Log.IsLoggable(Util.Log.VERBOSE)) Util.Log.v("Requesting camera permission.");
            ActivityCompat.RequestPermissions(this, new string[]{PERMISSION}, PERMISSION_REQUEST_CODE);
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults) {
            if (Util.Log.IsLoggable(Util.Log.VERBOSE)) Util.Log.v("Got permission result for: " + requestCode);
            if (grantResults.Length == 0 || requestCode != PERMISSION_REQUEST_CODE) {
                base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
                return;
            }

            if (grantResults[0] == Permission.Granted) {
                if (Util.Log.IsLoggable(Util.Log.VERBOSE)) Util.Log.v("Camera permission granted.");
                StartScanner();
            } else {
                if (Util.Log.IsLoggable(Util.Log.VERBOSE)) Util.Log.v("Camera permission denied.");
            }
        }

        private void StartScanner() {
            // Check for Google Play
            Dialog errorDialog = GoogleApiAvailability.Instance.GetErrorDialog(this, GoogleApiAvailability.Instance.IsGooglePlayServicesAvailable(this), 1);
            if (errorDialog != null) {
                errorDialog.Show();
            } else {
                GetAppIdScanner().Start();
            }
        }
    }
}