using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Net;
using Android.OS;
using Android.Text;
using Android.Views;
using Com.Layer.Atlas;
using Com.Layer.Atlas.Messagetypes.Generic;
using Com.Layer.Atlas.Messagetypes.Location;
using Com.Layer.Atlas.Messagetypes.Singlepartimage;
using Com.Layer.Atlas.Messagetypes.Text;
using Com.Layer.Atlas.Messagetypes.Threepartimage;
using Com.Layer.Atlas.Typingindicators;
using Com.Layer.Atlas.Util;
using Com.Layer.Sdk;
using Com.Layer.Sdk.Exceptions;
using Com.Layer.Sdk.Messaging;
using System.Collections.Generic;
using Android.Widget;
using Android.Views.InputMethods;

namespace Com.Layer.Messenger
{
    [Activity(WindowSoftInputMode = SoftInput.AdjustResize)]
    public class MessagesListActivity : BaseActivity
    {
        private UiState mState;
        private Conversation mConversation;

        private AtlasAddressBar mAddressBar;
        private AtlasHistoricMessagesFetchLayout mHistoricFetchLayout;
        private AtlasMessagesRecyclerView mMessagesList;
        private AtlasTypingIndicator mTypingIndicator;
        private AtlasMessageComposer mMessageComposer;

        public MessagesListActivity()
            : base(Resource.Layout.activity_messages_list, Resource.Menu.menu_messages_list, Resource.String.title_select_conversation, true)
        {
        }

        private void SetUiState(UiState state) {
            if (mState == state) return;
            mState = state;
            switch (state) {
                case UiState.ADDRESS:
                    mAddressBar.Visibility = ViewStates.Visible;
                    mAddressBar.SetSuggestionsVisibility((int) ViewStates.Visible);
                    mHistoricFetchLayout.Visibility = ViewStates.Gone;
                    mMessageComposer.Visibility = ViewStates.Gone;
                    break;

                case UiState.ADDRESS_COMPOSER:
                    mAddressBar.Visibility = ViewStates.Visible;
                    mAddressBar.SetSuggestionsVisibility((int) ViewStates.Visible);
                    mHistoricFetchLayout.Visibility = ViewStates.Gone;
                    mMessageComposer.Visibility = ViewStates.Visible;
                    break;

                case UiState.ADDRESS_CONVERSATION_COMPOSER:
                    mAddressBar.Visibility = ViewStates.Visible;
                    mAddressBar.SetSuggestionsVisibility((int) ViewStates.Gone);
                    mHistoricFetchLayout.Visibility = ViewStates.Visible;
                    mMessageComposer.Visibility = ViewStates.Visible;
                    break;

                case UiState.CONVERSATION_COMPOSER:
                    mAddressBar.Visibility = ViewStates.Gone;
                    mAddressBar.SetSuggestionsVisibility((int) ViewStates.Gone);
                    mHistoricFetchLayout.Visibility = ViewStates.Visible;
                    mMessageComposer.Visibility = ViewStates.Visible;
                    break;
            }
        }

        protected override void OnCreate(Bundle savedInstanceState) {
            base.OnCreate(savedInstanceState);
            if (App.RouteLogin(this)) {
                if (!IsFinishing) Finish();
                return;
            }

            mAddressBar = FindViewById<AtlasAddressBar>(Resource.Id.conversation_launcher)
                    .Init(GetLayerClient(), GetParticipantProvider(), GetPicasso())
                    .SetOnConversationClickListener(new AtlasAddressBarOnConversationClickListener(this))
                    .SetOnParticipantSelectionChangeListener(new AtlasAddressBarOnParticipantSelectionChangeListener(this))
                    .AddTextChangedListener(new AtlasAddressBarTextWatcher(this))
                    .SetOnEditorActionListener(new AtlasAddressBarOnEditorActionListener(this));

            mHistoricFetchLayout = FindViewById<AtlasHistoricMessagesFetchLayout>(Resource.Id.historic_sync_layout)
                    .Init(GetLayerClient())
                    .SetHistoricMessagesPerFetch(20);

            mMessagesList = FindViewById<AtlasMessagesRecyclerView>(Resource.Id.messages_list)
                    .Init(GetLayerClient(), GetParticipantProvider(), GetPicasso())
                    .AddCellFactories(
                            new TextCellFactory(),
                            new ThreePartImageCellFactory(this, GetLayerClient(), GetPicasso()),
                            new LocationCellFactory(this, GetPicasso()),
                            new SinglePartImageCellFactory(this, GetLayerClient(), GetPicasso()),
                            new GenericCellFactory())
                    .SetOnMessageSwipeListener(new SwipeableItemOnSwipeListener(this));

            mTypingIndicator = new AtlasTypingIndicator(this)
                    .Init(GetLayerClient())
                    .SetTypingIndicatorFactory(new BubbleTypingIndicatorFactory())
                    .SetTypingActivityListener(new AtlasTypingIndicatorTypingActivityListener(this));

            mMessageComposer = FindViewById<AtlasMessageComposer>(Resource.Id.message_composer)
                    .Init(GetLayerClient(), GetParticipantProvider())
                    .SetTextSender(new TextSender())
                    .AddAttachmentSenders(
                            new CameraSender(Resource.String.attachment_menu_camera, Java.Lang.Integer.ValueOf(Resource.Drawable.ic_photo_camera_white_24dp), this),
                            new GallerySender(Resource.String.attachment_menu_gallery, Java.Lang.Integer.ValueOf(Resource.Drawable.ic_photo_white_24dp), this),
                            new LocationSender(Resource.String.attachment_menu_location, Java.Lang.Integer.ValueOf(Resource.Drawable.ic_place_white_24dp), this));
            mMessageComposer.FocusChange += (sender, args) =>
            {
                if (args.HasFocus)
                {
                    SetUiState(UiState.CONVERSATION_COMPOSER);
                    SetTitle(true);
                }
            };

            // Get or create Conversation from Intent extras
            Conversation conversation = null;
            Intent intent = Intent;
            if (intent != null) {
                if (intent.HasExtra(PushNotificationReceiver.LAYER_CONVERSATION_KEY)) {
                    Uri conversationId = intent.GetParcelableExtra(PushNotificationReceiver.LAYER_CONVERSATION_KEY) as Uri;
                    conversation = GetLayerClient().GetConversation(conversationId);
                } else if (intent.HasExtra("participantIds")) {
                    string[] participantIds = intent.GetStringArrayExtra("participantIds");
                    try {
                        conversation = GetLayerClient().NewConversation(new ConversationOptions().Distinct(true), participantIds);
                    } catch (LayerConversationException e) {
                        conversation = e.Conversation;
                    }
                }
            }
            SetConversation(conversation, conversation != null);
        }

        protected override void OnResume() {
            // Clear any notifications for this conversation
            PushNotificationReceiver.GetNotifications(this).Clear(mConversation);
            base.OnResume();
            SetTitle(mConversation != null);
        }

        protected override void OnPause() {
            // Update the notification position to the latest seen
            PushNotificationReceiver.GetNotifications(this).Clear(mConversation);
            base.OnPause();
        }

        public void SetTitle(bool useConversation) {
            if (!useConversation) {
                SetTitle(Resource.String.title_select_conversation);
            } else {
                Title = AtlasUtil.GetConversationTitle(GetLayerClient(), GetParticipantProvider(), mConversation);
            }
        }

        private void SetConversation(Conversation conversation, bool hideLauncher) {
            mConversation = conversation;
            mHistoricFetchLayout.SetConversation(conversation);
            mMessagesList.SetConversation(conversation);
            mTypingIndicator.SetConversation(conversation);
            mMessageComposer.SetConversation(conversation);

            // UI state
            if (conversation == null) {
                SetUiState(UiState.ADDRESS);
                return;
            }

            if (hideLauncher) {
                SetUiState(UiState.CONVERSATION_COMPOSER);
                return;
            }

            if (conversation.GetHistoricSyncStatus() == Conversation.HistoricSyncStatus.Invalid) {
                // New "temporary" conversation
                SetUiState(UiState.ADDRESS_COMPOSER);
            } else {
                SetUiState(UiState.ADDRESS_CONVERSATION_COMPOSER);
            }
        }

        public override bool OnOptionsItemSelected(IMenuItem item) {
            switch (item.ItemId) {
                case Resource.Id.action_details:
                    if (mConversation == null) return true;
                    Intent intent = new Intent(this, typeof(ConversationSettingsActivity));
                    intent.PutExtra(PushNotificationReceiver.LAYER_CONVERSATION_KEY, mConversation.Id);
                    StartActivity(intent);
                    return true;

                case Resource.Id.action_sendlogs:
                    LayerClient.SendLogs(GetLayerClient(), this);
                    return true;
            }
            return base.OnOptionsItemSelected(item);
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data) {
            mMessageComposer.OnActivityResult(this, requestCode, (int) resultCode, data);
            base.OnActivityResult(requestCode, resultCode, data);
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults) {
            int[] intGrantResults = new int[grantResults.Length];
            for (int i=0; i < grantResults.Length; i++)
            {
                intGrantResults[i] = (int) grantResults[i];
            }
            mMessageComposer.OnRequestPermissionsResult(requestCode, permissions, intGrantResults);
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        private enum UiState {
            ADDRESS,
            ADDRESS_COMPOSER,
            ADDRESS_CONVERSATION_COMPOSER,
            CONVERSATION_COMPOSER
        }

        private class AtlasAddressBarOnConversationClickListener : Java.Lang.Object, AtlasAddressBar.IOnConversationClickListener
        {
            private MessagesListActivity _activity;

            public AtlasAddressBarOnConversationClickListener(MessagesListActivity messagesListActivity)
            {
                this._activity = messagesListActivity;
            }

            public void OnConversationClick(AtlasAddressBar addressBar, Conversation conversation)
            {
                _activity.SetConversation(conversation, true);
                _activity.SetTitle(true);
            }
        }

        private class AtlasAddressBarOnParticipantSelectionChangeListener : Java.Lang.Object, AtlasAddressBar.IOnParticipantSelectionChangeListener
        {
            private MessagesListActivity _activity;

            public AtlasAddressBarOnParticipantSelectionChangeListener(MessagesListActivity messagesListActivity)
            {
                this._activity = messagesListActivity;
            }

            public void OnParticipantSelectionChanged(AtlasAddressBar addressBar, IList<string> participantIds)
            {
                if (participantIds.Count == 0)
                {
                    _activity.SetConversation(null, false);
                    return;
                }
                try
                {
                    _activity.SetConversation(_activity.GetLayerClient().NewConversation(new ConversationOptions().Distinct(true), participantIds), false);
                }
                catch (LayerConversationException e)
                {
                    _activity.SetConversation(e.Conversation, false);
                }
            }
        }

        private class AtlasAddressBarTextWatcher : Java.Lang.Object, ITextWatcher
        {
            private MessagesListActivity _activity;

            public AtlasAddressBarTextWatcher(MessagesListActivity messagesListActivity)
            {
                this._activity = messagesListActivity;
            }

            public void BeforeTextChanged(Java.Lang.ICharSequence s, int start, int count, int after)
            {
            }

            public void OnTextChanged(Java.Lang.ICharSequence s, int start, int before, int count)
            {
            }

            public void AfterTextChanged(IEditable s)
            {
                if (_activity.mState == UiState.ADDRESS_CONVERSATION_COMPOSER)
                {
                    _activity.mAddressBar.SetSuggestionsVisibility((int) (string.IsNullOrEmpty(s.ToString()) ? ViewStates.Gone : ViewStates.Visible));
                }
            }
        }

        private class AtlasAddressBarOnEditorActionListener : Java.Lang.Object, TextView.IOnEditorActionListener
        {
            private MessagesListActivity _activity;

            public AtlasAddressBarOnEditorActionListener(MessagesListActivity messagesListActivity)
            {
                this._activity = messagesListActivity;
            }

            public bool OnEditorAction(TextView v, ImeAction actionId, KeyEvent event_)
            {
                if (actionId == ImeAction.Done || event_.KeyCode == Keycode.Enter) {
                    _activity.SetUiState(UiState.CONVERSATION_COMPOSER);
                    _activity.SetTitle(true);
                    return true;
                }
                return false;
            }
        }

        private class SwipeableItemOnSwipeListener : Atlas.Util.Views.SwipeableItem.OnSwipeListener
        {
            private MessagesListActivity _activity;

            public SwipeableItemOnSwipeListener(MessagesListActivity activity)
            {
                _activity = activity;
            }

            public override void OnSwipe(Java.Lang.Object message, int direction)
            {
                new AlertDialog.Builder(_activity)
                    .SetMessage(Resource.String.alert_message_delete_message)
                    .SetNegativeButton(Resource.String.alert_button_cancel, (sender, args) => 
                    {
                        // TODO: simply update this one message
                        _activity.mMessagesList.GetAdapter().NotifyDataSetChanged();
                        (sender as IDialogInterface).Dismiss();
                    })
                    .SetPositiveButton(Resource.String.alert_button_delete, (sender, args) =>
                    {
                        (message as IMessage).Delete(LayerClient.DeletionMode.AllParticipants);

                    }).Show();
            }
        }

        private class AtlasTypingIndicatorTypingActivityListener : Java.Lang.Object, AtlasTypingIndicator.ITypingActivityListener
        {
            private MessagesListActivity _activity;

            public AtlasTypingIndicatorTypingActivityListener(MessagesListActivity messagesListActivity)
            {
                this._activity = messagesListActivity;
            }

            public void OnTypingActivityChange(AtlasTypingIndicator typingIndicator, bool active)
            {
                _activity.mMessagesList.SetFooterView(active ? typingIndicator : null);
            }
        }
    }
}