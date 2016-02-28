using Android.Content;
using Android.Net;
using Android.OS;
using Com.Layer.Atlas.Provider;
using Com.Layer.Messenger.Util;
using Java.Lang;
using Java.Net;
using Org.Json;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Com.Layer.Messenger.Flavor
{
    public class DemoParticipantProvider : Java.Lang.Object, IParticipantProvider
    {
        private readonly Context mContext;
        private string mLayerAppIdLastPathSegment;
        private readonly ICollection<IParticipantListener> mParticipantListeners = new List<IParticipantListener>();
        private readonly IDictionary<string, DemoParticipant> mParticipantMap = new Dictionary<string, DemoParticipant>();
        private int mFetchingFlag = 0;

        public DemoParticipantProvider(Context context) {
            mContext = context.ApplicationContext;
        }

        public DemoParticipantProvider SetLayerAppId(string layerAppId) {
            if (layerAppId.Contains("/")) {
                mLayerAppIdLastPathSegment = Uri.Parse(layerAppId).LastPathSegment;
            } else {
                mLayerAppIdLastPathSegment = layerAppId;
            }
            Load();
            FetchParticipants();
            return this;
        }


        //==============================================================================================
        // Atlas ParticipantProvider
        //==============================================================================================

        public IDictionary<string, IParticipant> GetMatchingParticipants(string filter, IDictionary<string, IParticipant> result)
        {
            if (result == null) {
                result = new Dictionary<string, IParticipant>();
            }

            lock (mParticipantMap)
            {
                // With no filter, return all Participants
                if (filter == null) {
                    foreach (var entry in mParticipantMap)
                    {
                        result.Add(entry.Key, entry.Value);
                    }
                    return result;
                }

                // Filter participants by substring matching first- and last- names
                foreach (DemoParticipant p in mParticipantMap.Values) {
                    bool matches = false;
                    if (p.Name != null && p.Name.ToLowerInvariant().Contains(filter))
                        matches = true;
                    if (matches) {
                        result.Add(p.Id, p);
                    } else {
                        result.Remove(p.Id);
                    }
                }
                return result;
            }
        }

        public IParticipant GetParticipant(string userId) {
            lock (mParticipantMap) {
                DemoParticipant participant = mParticipantMap[userId];
                if (participant != null) return participant;
                FetchParticipants();
                return null;
            }
        }

        /**
         * Adds the provided Participants to this ParticipantProvider, saves the participants, and
         * returns the list of added participant IDs.
         */
        private DemoParticipantProvider SetParticipants(ICollection<DemoParticipant> participants) {
            IList<string> newParticipantIds = new List<string>(participants.Count);
            lock (mParticipantMap) {
                foreach (DemoParticipant participant in participants) {
                    string participantId = participant.Id;
                    if (!mParticipantMap.ContainsKey(participantId))
                        newParticipantIds.Add(participantId);
                    mParticipantMap.Add(participantId, participant);
                }
                save();
            }
            AlertParticipantsUpdated(newParticipantIds);
            return this;
        }


        //==============================================================================================
        // Persistence
        //==============================================================================================

        /**
         * Loads additional participants from SharedPreferences
         */
        private bool Load() {
            lock (mParticipantMap) {
                string jsonString = mContext.GetSharedPreferences("participants", FileCreationMode.Private).GetString("json", null);
                if (jsonString == null) return false;

                try {
                    foreach (DemoParticipant participant in ParticipantsFromJson(new JSONArray(jsonString))) {
                        mParticipantMap.Add(participant.Id, participant);
                    }
                    return true;
                } catch (JSONException e) {
                    if (Log.IsLoggable(Log.ERROR)) Log.e(e.Message, e);
                }
                return false;
            }
        }

        /**
         * Saves the current map of participants to SharedPreferences
         */
        private bool save() {
            lock (mParticipantMap) {
                try {
                    mContext.GetSharedPreferences("participants", FileCreationMode.Private).Edit()
                            .PutString("json", ParticipantsToJson(mParticipantMap.Values).ToString())
                            .Commit();
                    return true;
                } catch (JSONException e) {
                    if (Log.IsLoggable(Log.ERROR)) Log.e(e.Message, e);
                }
            }
            return false;
        }


        //==============================================================================================
        // Network operations
        //==============================================================================================
        private DemoParticipantProvider FetchParticipants() {
            if (0 == Interlocked.CompareExchange(ref mFetchingFlag, 1, 0)) return this;
            new FetchParticipantsAsyncTask(this).Execute();
            return this;
        }

        private class FetchParticipantsAsyncTask : AsyncTask<Void, Void, Void>
        {
            private DemoParticipantProvider _demoParticipantProvider;

            public FetchParticipantsAsyncTask(DemoParticipantProvider demoParticipantProvider)
            {
                _demoParticipantProvider = demoParticipantProvider;
            }

            protected override Void RunInBackground(params Void[] params_)
            {
                try
                {
                    // Post request
                    string url = "https://layer-identity-provider.herokuapp.com/apps/" + _demoParticipantProvider.mLayerAppIdLastPathSegment + "/atlas_identities";
                    HttpURLConnection connection = (HttpURLConnection) new URL(url).OpenConnection();
                    connection.DoInput = true;
                    connection.DoOutput = false;
                    connection.RequestMethod = "GET";
                    connection.AddRequestProperty("Content-Type", "application/json");
                    connection.AddRequestProperty("Accept", "application/json");
                    connection.AddRequestProperty("X_LAYER_APP_ID", _demoParticipantProvider.mLayerAppIdLastPathSegment);

                    // Handle failure
                    HttpStatus statusCode = connection.ResponseCode;
                    if (statusCode != HttpStatus.Ok && statusCode != HttpStatus.Created)
                    {
                        if (Log.IsLoggable(Log.ERROR))
                        {
                            Log.e(string.Format("Got status %d when fetching participants", (int) statusCode));
                        }
                        return null;
                    }

                    // Parse response
                    Stream input = connection.InputStream;
                    string result = CUtil.StreamToString(input);
                        input.Close();
                    connection.Disconnect();
                    JSONArray json = new JSONArray(result);
                    _demoParticipantProvider.SetParticipants(ParticipantsFromJson(json));
                }
                catch (Exception e)
                {
                    if (Log.IsLoggable(Log.ERROR)) Log.e(e.Message, e);
                }
                finally
                {
                    _demoParticipantProvider.mFetchingFlag = 0;
                }
                return null;
            }
        }


        //==============================================================================================
        // Utils
        //==============================================================================================

        private static IList<DemoParticipant> ParticipantsFromJson(JSONArray participantArray) {
            IList<DemoParticipant> participants = new List<DemoParticipant>(participantArray.Length());
            for (int i = 0; i < participantArray.Length(); i++) {
                JSONObject participantObject = participantArray.GetJSONObject(i);
                DemoParticipant participant = new DemoParticipant();
                participant.Id = participantObject.OptString("id");
                participant.Name = participantObject.OptString("name");
                participant.AvatarUrl = null;
                participants.Add(participant);
            }
            return participants;
        }

        private static JSONArray ParticipantsToJson(ICollection<DemoParticipant> participants) {
            JSONArray participantsArray = new JSONArray();
            foreach (DemoParticipant participant in participants) {
                JSONObject participantObject = new JSONObject();
                participantObject.Put ("id", participant.Id);
                participantObject.Put("name", participant.Name);
                participantsArray.Put(participantObject);
            }
            return participantsArray;
        }

        private DemoParticipantProvider RegisterParticipantListener(IParticipantListener participantListener) {
            if (!mParticipantListeners.Contains(participantListener)) {
                mParticipantListeners.Add(participantListener);
            }
            return this;
        }

        private DemoParticipantProvider UsnregisterParticipantListener(IParticipantListener participantListener) {
            mParticipantListeners.Remove(participantListener);
            return this;
        }

        private void AlertParticipantsUpdated(ICollection<string> updatedParticipantIds) {
            var listeners = new List<IParticipantListener>(mParticipantListeners);
            foreach (IParticipantListener listener in listeners) {
                listener.OnParticipantsUpdated(this, updatedParticipantIds);
            }
        }


        //==============================================================================================
        // Callbacks
        //==============================================================================================

        public interface IParticipantListener {
            void OnParticipantsUpdated(DemoParticipantProvider provider, ICollection<string> updatedParticipantIds);
        }
    }
}