// Fix for generics erasure on inherited methods, ie. adding back the inherited type-erased methods as properties

namespace Com.Layer.Atlas.Util.Picasso.Transformations
{
    public partial class CircleTransform
    {
        public string Key
        {
            get
            {
                return InvokeKey();
            }
        }
    }
}