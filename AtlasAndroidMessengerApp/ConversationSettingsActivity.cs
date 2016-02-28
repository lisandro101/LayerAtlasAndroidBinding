using Android.App;
using Android.Views;
using Com.Layer.Sdk.Changes;
using Com.Layer.Sdk.Listeners;
using Android.Widget;
using Android.Support.V7.Widget;
using Com.Layer.Sdk.Messaging;
using Android.OS;
using Android.Net;
using Android.Views.InputMethods;
using Com.Layer.Atlas.Util;
using Android.Content;
using System.Collections.Generic;
using Com.Layer.Sdk;
using Com.Layer.Sdk.Policy;
using Com.Layer.Atlas.Provider;
using Com.Layer.Atlas;

namespace Com.Layer.Messenger
{
    [Activity(WindowSoftInputMode = SoftInput.AdjustResize)]
    public class ConversationSettingsActivity : BaseActivity, ILayerPolicyListener, ILayerChangeEventListener
    {
        private EditText mConversationName;
        private Switch mShowNotifications;
        private RecyclerView mParticipantRecyclerView;
        private Button mLeaveButton;
        private Button mAddParticipantsButton;

        private Conversation mConversation;
        private ParticipantAdapter mParticipantAdapter;

        public ConversationSettingsActivity()
            : base(Resource.Layout.activity_conversation_settings, Resource.Menu.menu_conversation_details, Resource.String.title_conversation_details, true)
        {
        }

        protected override void OnCreate(Bundle savedInstanceState) {
            base.OnCreate(savedInstanceState);
            mConversationName = FindViewById<EditText>(Resource.Id.conversation_name);
            mShowNotifications = FindViewById<Switch>(Resource.Id.show_notifications_switch);
            mParticipantRecyclerView = FindViewById<RecyclerView>(Resource.Id.participants);
            mLeaveButton = FindViewById<Button>(Resource.Id.leave_button);
            mAddParticipantsButton = FindViewById<Button>(Resource.Id.add_participant_button);

            // Get Conversation from Intent extras
            Uri conversationId = Intent.GetParcelableExtra(PushNotificationReceiver.LAYER_CONVERSATION_KEY) as Uri;
            mConversation = GetLayerClient().GetConversation(conversationId);
            if (mConversation == null && !IsFinishing) Finish();

            mParticipantAdapter = new ParticipantAdapter(this);
            mParticipantRecyclerView.SetAdapter(mParticipantAdapter);

            LinearLayoutManager manager = new LinearLayoutManager(this, LinearLayoutManager.Vertical, false);
            mParticipantRecyclerView.SetLayoutManager(manager);

            mConversationName.EditorAction += (sender, args) =>
            {
                if (args.ActionId == ImeAction.Done || (args.Event != null && args.Event.Action == KeyEventActions.Down && args.Event.KeyCode == Keycode.Enter)) {
                    string title = ((EditText) sender).Text.ToString().Trim();
                    AtlasUtil.SetConversationMetadataTitle(mConversation, title);
                    Toast.MakeText(this, Resource.String.toast_group_name_updated, ToastLength.Short).Show();
                    args.Handled = true;
                }
                args.Handled = false;
            };

            mShowNotifications.CheckedChange += (sender, args) =>
            {
                PushNotificationReceiver.GetNotifications(this).SetEnabled(mConversation.Id, args.IsChecked);
            };

            mLeaveButton.Click += (sender, args) =>
            {
                SetEnabled(false);
                mConversation.RemoveParticipants(GetLayerClient().AuthenticatedUserId);
                Refresh();
                Intent intent = new Intent(this, typeof(ConversationsListActivity));
                intent.SetFlags(ActivityFlags.ClearTop);
                SetEnabled(true);
                StartActivity(intent);
            };

            mAddParticipantsButton.Click += (sender, args) =>
            {
                // TODO
                Toast.MakeText(this, "Coming soon", ToastLength.Long).Show();
            };
        }

        public void SetEnabled(bool enabled) {
            mShowNotifications.Enabled = enabled;
            mLeaveButton.Enabled = enabled;
        }

        private void Refresh() {
            if (!GetLayerClient().IsAuthenticated) return;

            mConversationName.Text = AtlasUtil.GetConversationMetadataTitle(mConversation);
            mShowNotifications.Checked = PushNotificationReceiver.GetNotifications(this).IsEnabled(mConversation.Id);

            ISet<string> participantsMinusMe = new HashSet<string>(mConversation.Participants);
            participantsMinusMe.Remove(GetLayerClient().AuthenticatedUserId);

            if (participantsMinusMe.Count == 0) {
                // I've been removed
                mConversationName.Enabled = false;
                mLeaveButton.Visibility = ViewStates.Gone;
            } else if (participantsMinusMe.Count == 1) {
                // 1-on-1
                mConversationName.Enabled = false;
                mLeaveButton.Visibility = ViewStates.Gone;
            } else {
                // Group
                mConversationName.Enabled = true;
                mLeaveButton.Visibility = ViewStates.Visible;
            }
            mParticipantAdapter.Refresh();
        }

        protected override void OnResume() {
            base.OnResume();
            GetLayerClient().RegisterPolicyListener(this).RegisterEventListener(this);
            SetEnabled(true);
            Refresh();
        }

        protected override void OnPause() {
            GetLayerClient().UnregisterPolicyListener(this).UnregisterEventListener(this);
            base.OnPause();
        }

        public void OnPolicyListUpdate(LayerClient layerClient, IList<LayerPolicy> list, IList<LayerPolicy> list1) {
            Refresh();
        }

        public void OnChangeEvent(LayerChangeEvent layerChangeEvent) {
            Refresh();
        }

        private class ViewHolder : RecyclerView.ViewHolder
        {
            internal AtlasAvatar mAvatar;
            internal TextView mTitle;
            internal ImageView mBlocked;
            internal IParticipant mParticipant;
            internal LayerPolicy mBlockPolicy;

            public ViewHolder(ViewGroup parent)
                : base(LayoutInflater.From(parent.Context).Inflate(Resource.Layout.participant_item, parent, false))
            {
                mAvatar = ItemView.FindViewById<AtlasAvatar>(Resource.Id.avatar);
                mTitle = ItemView.FindViewById<TextView>(Resource.Id.title);
                mBlocked = ItemView.FindViewById<ImageView>(Resource.Id.blocked);
            }
        }

        private class ParticipantAdapter : RecyclerView.Adapter
        {
            private readonly ConversationSettingsActivity _activity;
            List<IParticipant> mParticipants = new List<IParticipant>();

            internal ParticipantAdapter(ConversationSettingsActivity activity)
            {
                _activity = activity;
            }

            public void Refresh() {
                // Get new sorted list of Participants
                IParticipantProvider provider = App.GetParticipantProvider();
                mParticipants.Clear();
                foreach (string participantId in _activity.mConversation.Participants) {
                    if (participantId.Equals(_activity.GetLayerClient().AuthenticatedUserId)) continue;
                    IParticipant participant = provider.GetParticipant(participantId);
                    if (participant == null) continue;
                    mParticipants.Add(participant);
                }
                mParticipants.Sort();

                // Adjust participant container height
                int height = (int) System.Math.Round((double) mParticipants.Count * _activity.Resources.GetDimensionPixelSize(Resource.Dimension.atlas_secondary_item_height));
                LinearLayout.LayoutParams params_ = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, height);
                _activity.mParticipantRecyclerView.LayoutParameters = params_;

                // Notify changes
                NotifyDataSetChanged();
            }

            public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType) {
                ViewHolder viewHolder = new ViewHolder(parent);
                viewHolder.mAvatar.Init(App.GetParticipantProvider(), App.GetPicasso());
                viewHolder.ItemView.Tag = viewHolder;

                // Click to display remove / block dialog
                viewHolder.ItemView.Click += (sender, args) =>
                {
                    ViewHolder holder = (sender as View).Tag as ViewHolder;

                    AlertDialog.Builder builder = new AlertDialog.Builder((sender as View).Context)
                            .SetMessage(holder.mTitle.Text.ToString());

                    if (_activity.mConversation.Participants.Count > 2) {
                        builder.SetNeutralButton(Resource.String.alert_button_remove, (sender_, args_) =>
                        {
                            _activity.mConversation.RemoveParticipants(holder.mParticipant.Id);
                        });
                    }

                    builder.SetPositiveButton(holder.mBlockPolicy != null ? Resource.String.alert_button_unblock : Resource.String.alert_button_block,
                            (sender_, args_) =>
                    {
                        IParticipant participant = holder.mParticipant;
                        if (holder.mBlockPolicy == null) {
                            // Block
                            holder.mBlockPolicy = new LayerPolicy.Builder(LayerPolicy.PolicyType.Block).SentByUserId(participant.Id).Build();
                            _activity.GetLayerClient().AddPolicy(holder.mBlockPolicy);
                            holder.mBlocked.Visibility = ViewStates.Visible;
                        } else {
                            _activity.GetLayerClient().RemovePolicy(holder.mBlockPolicy);
                            holder.mBlockPolicy = null;
                            holder.mBlocked.Visibility = ViewStates.Invisible;
                        }
                    })
                    .SetNegativeButton(Resource.String.alert_button_cancel, (sender_, args_) => 
                    {
                        (sender as IDialogInterface).Dismiss();
                    })
                    .Show();
                };

                return viewHolder;
            }

            public override void OnBindViewHolder(RecyclerView.ViewHolder viewHolderObj, int position) {
                IParticipant participant = mParticipants[position];
                var viewHolder = viewHolderObj as ViewHolder;
                viewHolder.mTitle.Text = participant.Name;
                viewHolder.mAvatar.SetParticipants(participant.Id);
                viewHolder.mParticipant = participant;

                LayerPolicy block = null;
                foreach (LayerPolicy policy in _activity.GetLayerClient().Policies) {
                    if (policy.GetPolicyType() != LayerPolicy.PolicyType.Block) continue;
                    if (!policy.SentByUserID.Equals(participant.Id)) continue;
                    block = policy;
                    break;
                }

                viewHolder.mBlockPolicy = block;
                viewHolder.mBlocked.Visibility = block == null ? ViewStates.Invisible : ViewStates.Visible;
            }

            public override int ItemCount {
                get { return mParticipants.Count; }
            }
        }
    }
}