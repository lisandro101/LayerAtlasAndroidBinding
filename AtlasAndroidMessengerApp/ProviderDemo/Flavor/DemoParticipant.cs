using Android.Net;
using Com.Layer.Atlas.Provider;

namespace Com.Layer.Messenger.Flavor
{
    public class DemoParticipant : Java.Lang.Object, IParticipant
    {
        private string mId;
        private string mName;
        private Uri mAvatarUrl;

        public string Id {
            get { return mId; }
            set { mId = value; }
        }

        public string Name {
            get { return mName; }
            set { mName = value; }
        }

        public Uri AvatarUrl {
            get { return mAvatarUrl; }
            set { mAvatarUrl = value; }
        }

        public int CompareTo(IParticipant another)
        {
            return Name.ToUpperInvariant().CompareTo(another.Name.ToUpperInvariant());
        }
    }
}