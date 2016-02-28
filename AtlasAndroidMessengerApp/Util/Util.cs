using System.IO;
using System.Text;

namespace Com.Layer.Messenger.Util
{
    public class CUtil
    {
        public static string StreamToString(Stream stream)
        {
            StreamReader reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }
    }
}