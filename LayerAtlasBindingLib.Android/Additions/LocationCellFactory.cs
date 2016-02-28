using Android.Views;
using Com.Layer.Atlas.Provider;
using Com.Layer.Sdk;
using Com.Layer.Sdk.Messaging;

namespace Com.Layer.Atlas.Messagetypes.Location
{
    public partial class LocationCellFactory
    {
        public override Java.Lang.Object CreateCellHolder(ViewGroup cellView, bool isMe, LayoutInflater layoutInflater)
        {
            return DoCreateCellHolder(cellView, isMe, layoutInflater);
        }

        public override void BindCellHolder(Java.Lang.Object cellHolder, Java.Lang.Object location, IMessage message, CellHolderSpecs specs)
        {
            DoBindCellHolder(cellHolder as CellHolder, location as CellFactoryLocation, message, specs);
        }

        public override Java.Lang.Object ParseContent(LayerClient layerClient, IParticipantProvider participantProvider, IMessage message)
        {
            return DoParseContent(layerClient, participantProvider, message);
        }
    }
}