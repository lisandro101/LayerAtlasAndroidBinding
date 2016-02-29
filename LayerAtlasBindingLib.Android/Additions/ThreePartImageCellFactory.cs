// Fix for generics erasure on inherited methods, ie. add back the overriding methods as forwarders

using Android.Views;
using Com.Layer.Atlas.Provider;
using Com.Layer.Sdk;
using Com.Layer.Sdk.Messaging;

namespace Com.Layer.Atlas.Messagetypes.Threepartimage
{
    public partial class ThreePartImageCellFactory
    {
        public override Java.Lang.Object CreateCellHolder(ViewGroup cellView, bool isMe, LayoutInflater layoutInflater)
        {
            return DoCreateCellHolder(cellView, isMe, layoutInflater);
        }

        public override void BindCellHolder(Java.Lang.Object cellHolder, Java.Lang.Object info, IMessage message, CellHolderSpecs specs)
        {
            DoBindCellHolder(cellHolder as CellHolder, info as Info, message, specs);
        }

        public override Java.Lang.Object ParseContent(LayerClient layerClient, IParticipantProvider participantProvider, IMessage message)
        {
            return DoParseContent(layerClient, participantProvider, message);
        }
    }
}