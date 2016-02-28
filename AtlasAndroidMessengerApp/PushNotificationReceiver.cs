using Android.App;
using Android.Content;
using Android.Net;
using Android.OS;
using Android.Support.V4.Content;
using Android.Support.V7.App;
using Com.Layer.Atlas.Util;
using Com.Layer.Sdk;
using Com.Layer.Sdk.Messaging;
using Com.Layer.Sdk.Query;
using Java.Util;
using Org.Json;
using System.Collections.Generic;
using System.Threading;

namespace Com.Layer.Messenger
{
    public class PushNotificationReceiver : BroadcastReceiver
    {
        public const int MESSAGE_ID = 1;
        private static int sPendingIntentCounterFlag = 0;
        private static Notifications sNotifications;

        public const string ACTION_PUSH = "com.layer.sdk.PUSH";
        public const string ACTION_CANCEL = BuildConfig.APPLICATION_ID + ".CANCEL_PUSH";

        public const string LAYER_TEXT_KEY = "layer-push-message";
        public const string LAYER_CONVERSATION_KEY = "layer-conversation-id";
        public const string LAYER_MESSAGE_KEY = "layer-message-id";

        /**
         * Parses the `com.layer.sdk.PUSH` Intent.
         */
        public override void OnReceive(Context context, Intent intent) {
            Bundle extras = intent.Extras;
            if (extras == null) return;
            string text = extras.GetString(LAYER_TEXT_KEY, null);
            Uri conversationId = extras.GetParcelable(LAYER_CONVERSATION_KEY) as Uri;
            Uri messageId = extras.GetParcelable(LAYER_MESSAGE_KEY) as Uri;

            if (intent.Action.Equals(ACTION_PUSH)) {
                // New push from Layer
                if (Util.Log.IsLoggable(Util.Log.VERBOSE)) Util.Log.v("Received notification for: " + messageId);
                if (messageId == null) {
                    if (Util.Log.IsLoggable(Util.Log.ERROR)) Util.Log.e("No message to notify: " + extras);
                    return;
                }
                if (conversationId == null) {
                    if (Util.Log.IsLoggable(Util.Log.ERROR)) Util.Log.e("No conversation to notify: " + extras);
                    return;
                }

                if (!GetNotifications(context).IsEnabled()) {
                    if (Util.Log.IsLoggable(Util.Log.VERBOSE)) {
                        Util.Log.v("Blocking notification due to global app setting");
                    }
                    return;
                }

                if (!GetNotifications(context).IsEnabled(conversationId)) {
                    if (Util.Log.IsLoggable(Util.Log.VERBOSE)) {
                        Util.Log.v("Blocking notification due to conversation detail setting");
                    }
                    return;
                }

                // Try to have content ready for viewing before posting a Notification
                AtlasUtil.WaitForContent(App.GetLayerClient().Connect(), messageId, new ContentAvailableCallback(this, context, text));

            } else if (intent.Action.Equals(ACTION_CANCEL)) {
                // User swiped notification out
                if (Util.Log.IsLoggable(Util.Log.VERBOSE)) {
                    Util.Log.v("Cancelling notifications for: " + conversationId);
                }
                GetNotifications(context).Clear(conversationId);
            } else {
                if (Util.Log.IsLoggable(Util.Log.ERROR)) {
                    Util.Log.e("Got unknown intent action: " + intent.Action);
                }
            }
        }

        private class ContentAvailableCallback : Java.Lang.Object, AtlasUtil.IContentAvailableCallback
        {
            private Context _context;
            private PushNotificationReceiver _receiver;
            private string _text;

            public ContentAvailableCallback(PushNotificationReceiver pushNotificationReceiver, Context context, string text)
            {
                _receiver = pushNotificationReceiver;
                _context = context;
                _text = text;
            }

            public void OnContentAvailable(LayerClient client, IQueryable obj)
            {
                if (Util.Log.IsLoggable(Util.Log.VERBOSE))
                {
                    Util.Log.v("Pre-fetched notification content");
                }
                GetNotifications(_context).Add(_context, (IMessage) obj, _text);
            }

            public void OnContentFailed(LayerClient client, Uri objectId, string reason)
            {
                if (Util.Log.IsLoggable(Util.Log.ERROR))
                {
                    Util.Log.e("Failed to fetch notification content");
                }
            }
        }

        public static Notifications GetNotifications(Context context)
        {
            lock(typeof(PushNotificationReceiver))
            {
                if (sNotifications == null)
                {
                    sNotifications = new Notifications(context);
                }
                return sNotifications;
            }
        }

        /**
         * Notifications manages notifications displayed on the user's device.  Notifications are
         * grouped by Conversation, where a Conversation's notifications are rolled-up into single
         * notification summaries.
         */
        public class Notifications
        {
            private const string KEY_ALL = "all";
            private const string KEY_POSITION = "position";
            private const string KEY_TEXT = "text";

            private const int MAX_MESSAGES = 5;
            // Contains black-listed conversation IDs and the global "all" key for notifications
            private readonly ISharedPreferences mDisableds;

            // Contains positions for message IDs
            private readonly ISharedPreferences mPositions;
            private readonly ISharedPreferences mMessages;
            private readonly NotificationManager mManager;

            public Notifications(Context context) {
                mDisableds = context.GetSharedPreferences("notification_disableds", FileCreationMode.Private);
                mPositions = context.GetSharedPreferences("notification_positions", FileCreationMode.Private);
                mMessages = context.GetSharedPreferences("notification_messages", FileCreationMode.Private);
                mManager = (NotificationManager) context.GetSystemService(Context.NotificationService);
            }

            public bool IsEnabled() {
                return !mDisableds.Contains(KEY_ALL);
            }

            public bool IsEnabled(Uri conversationId) {
                if (conversationId == null) {
                    return IsEnabled();
                }
                return !mDisableds.Contains(conversationId.ToString());
            }

            public void SetEnabled(bool enabled) {
                if (enabled) {
                    mDisableds.Edit().Remove(KEY_ALL).Apply();
                } else {
                    mDisableds.Edit().PutBoolean(KEY_ALL, true).Apply();
                    mManager.CancelAll();
                }
            }

            public void SetEnabled(Uri conversationId, bool enabled) {
                if (conversationId == null) {
                    return;
                }
                if (enabled) {
                    mDisableds.Edit().Remove(conversationId.ToString()).Apply();
                } else {
                    mDisableds.Edit().PutBoolean(conversationId.ToString(), true).Apply();
                    mManager.Cancel(conversationId.ToString(), MESSAGE_ID);
                }
            }

            public void Clear(Conversation conversation) {
                if (conversation == null) return;
                Clear(conversation.Id);
            }

            /**
             * Called when a Conversation is opened or message is marked as read
             * Clears messages map; sets position to greatest position
             *
             * @param conversationId Conversation whose notifications should be cleared
             */
            public void Clear(Uri conversationId)
            {
                new Thread(() =>
                {
                    if (conversationId == null) return;
                    string key = conversationId.ToString();
                    long maxPosition = GetMaxPosition(conversationId);
                    mMessages.Edit().Remove(key).Commit();
                    mPositions.Edit().PutLong(key, maxPosition).Commit();
                    mManager.Cancel(key, MESSAGE_ID);
                }).Start();
            }

            /**
             * Called when a new message arrives
             *
             * @param message Message to add
             * @param text    Notification text for added Message
             */
            internal void Add(Context context, IMessage message, string text) {
                Conversation conversation = message.Conversation;
                string key = conversation.Id.ToString();
                long currentPosition = mPositions.GetLong(key, long.MinValue);

                // Ignore older messages
                if (message.Position <= currentPosition) return;

                string currentMessages = mMessages.GetString(key, null);

                try {
                    JSONObject messages = currentMessages == null ? new JSONObject() : new JSONObject(currentMessages);
                    string messageKey = message.Id.ToString();

                    // Ignore if we already have this message
                    if (messages.Has(messageKey)) return;

                    JSONObject messageEntry = new JSONObject();
                    messageEntry.Put(KEY_POSITION, message.Position);
                    messageEntry.Put(KEY_TEXT, text);
                    messages.Put(messageKey, messageEntry);

                    mMessages.Edit().PutString(key, messages.ToString()).Commit();
                } catch (JSONException e) {
                    if (Util.Log.IsLoggable(Util.Log.ERROR)) {
                        Util.Log.e(e.Message, e);
                    }
                    return;
                }
                Update(context, conversation, message);
            }

            private void Update(Context context, Conversation conversation, IMessage message) {
                string messagesString = mMessages.GetString(conversation.Id.ToString(), null);
                if (messagesString == null) return;

                // Get current notification texts
                IDictionary<long, string> positionText = new Dictionary<long, string>();
                try {
                    JSONObject messagesJson = new JSONObject(messagesString);
                    IIterator iterator = messagesJson.Keys();
                    while (iterator.HasNext) {
                        string messageId = (string) iterator.Next();
                        JSONObject messageJson = messagesJson.GetJSONObject(messageId);
                        long position = messageJson.GetLong(KEY_POSITION);
                        string text = messageJson.GetString(KEY_TEXT);
                        positionText.Add(position, text);
                    }
                } catch (JSONException e) {
                    if (Util.Log.IsLoggable(Util.Log.ERROR)) {
                        Util.Log.e(e.Message, e);
                    }
                    return;
                }

                // Sort by message position
                List<long> positions = new List<long>(positionText.Keys);
                positions.Sort();

                // Construct notification
                string conversationTitle = AtlasUtil.GetConversationTitle(App.GetLayerClient(), App.GetParticipantProvider(), conversation);
                NotificationCompat.InboxStyle inboxStyle = new NotificationCompat.InboxStyle().SetBigContentTitle(conversationTitle);
                int i;
                if (positions.Count <= MAX_MESSAGES) {
                    i = 0;
                    inboxStyle.SetSummaryText((string) null);
                } else {
                    i = positions.Count - MAX_MESSAGES;
                    inboxStyle.SetSummaryText(context.GetString(Resource.String.notifications_num_more, i));
                }
                while (i < positions.Count) {
                    inboxStyle.AddLine(positionText[positions[i++]]);
                }

                string collapsedSummary = positions.Count == 1 ? positionText[positions[0]] :
                        context.GetString(Resource.String.notifications_new_messages, positions.Count);

                // Construct notification
                // TODO: use large icon based on avatars
                var mBuilder = new NotificationCompat.Builder(context)
                        .SetSmallIcon(Resource.Drawable.notification)
                        .SetContentTitle(conversationTitle)
                        .SetContentText(collapsedSummary)
                        .SetAutoCancel(true)
                        .SetLights(ContextCompat.GetColor(context, Resource.Color.atlas_action_bar_background), 100, 1900)
                        .SetPriority(NotificationCompat.PriorityHigh)
                        .SetDefaults(NotificationCompat.DefaultSound | NotificationCompat.DefaultVibrate)
                        .SetStyle(inboxStyle);

                // Intent to launch when clicked
                Intent clickIntent = new Intent(context, typeof(MessagesListActivity))
                        .SetPackage(context.ApplicationContext.PackageName)
                        .PutExtra(LAYER_CONVERSATION_KEY, conversation.Id)
                        .PutExtra(LAYER_MESSAGE_KEY, message.Id)
                        .SetFlags(ActivityFlags.ClearTop);
                PendingIntent clickPendingIntent = PendingIntent.GetActivity(
                        context, Interlocked.Increment(ref sPendingIntentCounterFlag),
                        clickIntent, PendingIntentFlags.OneShot);
                mBuilder.SetContentIntent(clickPendingIntent);

                // Intent to launch when swiped out
                Intent cancelIntent = new Intent(ACTION_CANCEL)
                        .SetPackage(context.ApplicationContext.PackageName)
                        .PutExtra(LAYER_CONVERSATION_KEY, conversation.Id)
                        .PutExtra(LAYER_MESSAGE_KEY, message.Id);
                PendingIntent cancelPendingIntent = PendingIntent.GetBroadcast(
                        context, Interlocked.Increment(ref sPendingIntentCounterFlag),
                        cancelIntent, PendingIntentFlags.OneShot);
                mBuilder.SetDeleteIntent(cancelPendingIntent);

                // Show the notification
                mManager.Notify(conversation.Id.ToString(), MESSAGE_ID, mBuilder.Build());
            }

            /**
             * Returns the current maximum Message position within the given Conversation, or
             * Long.MIN_VALUE if no messages are found.
             *
             * @param conversationId Conversation whose maximum Message position to return.
             * @return the current maximum Message position or Long.MIN_VALUE.
             */
            private long GetMaxPosition(Uri conversationId) {
                LayerClient layerClient = App.GetLayerClient();

                LayerQuery query = LayerQuery.InvokeBuilder(Java.Lang.Class.FromType(typeof(IMessage)))
                        .Predicate(new Predicate(MessageProperty.Conversation, Predicate.Operator.EqualTo, conversationId))
                        .SortDescriptor(new SortDescriptor(MessageProperty.Position, SortDescriptor.Order.Descending))
                        .Limit(1)
                        .Build();

                IList<IQueryable> results = layerClient.ExecuteQueryForObjects(query);
                if (results.Count == 0) return long.MinValue;
                return ((IMessage) results[0]).Position;
            }
        }
    }
}