using Android.OS;
using Android.Support.V7.App;
using Android.Views;
using Com.Layer.Atlas.Provider;
using Com.Layer.Sdk;
using Square.Picasso;

namespace Com.Layer.Messenger
{
    public abstract class BaseActivity : AppCompatActivity
    {
        private readonly int mLayoutResId;
        private readonly int mMenuResId;
        private readonly int mMenuTitleResId;
        private readonly bool mMenuBackEnabled;

        public BaseActivity(int layoutResId, int menuResId, int menuTitleResId, bool menuBackEnabled)
        {
            mLayoutResId = layoutResId;
            mMenuResId = menuResId;
            mMenuTitleResId = menuTitleResId;
            mMenuBackEnabled = menuBackEnabled;
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(mLayoutResId);

            ActionBar actionBar = SupportActionBar;
            if (actionBar == null) return;
            if (mMenuBackEnabled) actionBar.SetDisplayHomeAsUpEnabled(true);
            actionBar.SetTitle(mMenuTitleResId);
        }

        public new string Title
        {
            get
            {
                return base.Title;
            }
            set
            {
                ActionBar actionBar = SupportActionBar;
                if (actionBar == null)
                {
                    base.Title = value;
                }
                else {
                    actionBar.Title = value;
                }
            }
        }

        public override void SetTitle(int titleId) {
            ActionBar actionBar = SupportActionBar;
            if (actionBar == null) {
                base.SetTitle(titleId);
            } else {
                actionBar.SetTitle(titleId);
            }
        }

        protected override void OnResume() {
            base.OnResume();
            LayerClient client = App.GetLayerClient();
            if (client == null) return;
            if (client.IsAuthenticated) {
                client.Connect();
            } else {
                client.Authenticate();
            }
        }

        public override bool OnCreateOptionsMenu(IMenu menu) {
            MenuInflater.Inflate(mMenuResId, menu);
            return true;
        }

        public override bool OnOptionsItemSelected(IMenuItem item) {
            if (item.ItemId == Android.Resource.Id.Home) {
                // Menu "Navigate Up" acts like hardware back button
                OnBackPressed();
                return true;
            }
            return base.OnOptionsItemSelected(item);
        }

        protected LayerClient GetLayerClient() {
            return App.GetLayerClient();
        }

        protected IParticipantProvider GetParticipantProvider() {
            return App.GetParticipantProvider();
        }

        protected Picasso GetPicasso() {
            return App.GetPicasso();
        }
    }
}