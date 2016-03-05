using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Com.Layer.Atlas;
using Com.Layer.Atlas.Adapters;
using Com.Layer.Sdk;
using Com.Layer.Sdk.Messaging;
using Java.Interop;

namespace Com.Layer.Messenger
{
    [Activity(MainLauncher = true)]
    public class ConversationsListActivity : BaseActivity
    {
        private AtlasConversationsRecyclerView _conversationsList;

        public ConversationsListActivity()
            : base(Resource.Layout.activity_conversations_list, Resource.Menu.menu_conversations_list, Resource.String.title_conversations_list, false)
        {
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            if (App.RouteLogin(this)) {
                if (!IsFinishing) Finish();
                return;
            }

            _conversationsList = FindViewById<AtlasConversationsRecyclerView>(Resource.Id.conversations_list);

            // Atlas methods
            _conversationsList.Init(GetLayerClient(), GetParticipantProvider(), GetPicasso())
                    .SetInitialHistoricMessagesToFetch(20)
                    .SetOnConversationClickListener(new AtlasConversationsAdapterOnConversationClickListener(this))
                    .SetOnConversationSwipeListener(new SwipeableItemOnSwipeListener(this));

            FindViewById(Resource.Id.floating_action_button).Click += (sender, args) =>
            {
                StartActivity(new Intent(this, typeof(MessagesListActivity)));
            };
        }

        public override bool OnOptionsItemSelected(IMenuItem item) {
            switch (item.ItemId) {
                case Resource.Id.action_settings:
                    StartActivity(new Intent(this, typeof(AppSettingsActivity)));
                    return true;

                case Resource.Id.action_sendlogs:
                    LayerClient.SendLogs(GetLayerClient(), this);
                    return true;
            }

            return base.OnOptionsItemSelected(item);
        }

        private class AtlasConversationsAdapterOnConversationClickListener : Java.Lang.Object, AtlasConversationsAdapter.IOnConversationClickListener
        {
            private ConversationsListActivity _activity;

            public AtlasConversationsAdapterOnConversationClickListener(ConversationsListActivity conversationsListActivity)
            {
                _activity = conversationsListActivity;
            }

            public void OnConversationClick(AtlasConversationsAdapter adapter, Conversation conversation)
            {
                Intent intent = new Intent(_activity, typeof(MessagesListActivity));
                if (Util.Log.IsLoggable(Util.Log.VERBOSE)) {
                    Util.Log.v("Launching MessagesListActivity with existing conversation ID: " + conversation.Id);
                }
                intent.PutExtra(PushNotificationReceiver.LAYER_CONVERSATION_KEY, conversation.Id);
                _activity.StartActivity(intent);
            }

            public bool OnConversationLongClick(AtlasConversationsAdapter adapter, Conversation conversation)
            {
                return false;
            }
        }

        private class SwipeableItemOnSwipeListener : Atlas.Util.Views.SwipeableItem.OnSwipeListener
        {
            private ConversationsListActivity _activity;

            public SwipeableItemOnSwipeListener(ConversationsListActivity conversationsListActivity)
            {
                _activity = conversationsListActivity;
            }

            public override void OnSwipe(Java.Lang.Object conversation, int direction)
            {
                new AlertDialog.Builder(_activity)
                    .SetMessage(Resource.String.alert_message_delete_conversation)
                    .SetNegativeButton(Resource.String.alert_button_cancel, (sender, args) =>
                    {
                        // TODO: simply update this one conversation
                        _activity._conversationsList.GetAdapter().NotifyDataSetChanged();
                        ((IDialogInterface) sender).Dismiss();
                    })
                    .SetPositiveButton(Resource.String.alert_button_delete, (sender, args) =>
                    {
                        conversation.JavaCast<Conversation>().Delete(LayerClient.DeletionMode.AllParticipants);
                    })
                    .Show();
            }
        }
    }
}