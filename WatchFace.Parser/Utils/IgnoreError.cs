using Microsoft.SqlServer.Server;
using System.Threading;

namespace WatchFace.Parser.Utils
{
    public class Ignore
    {
        public bool ignore = false;
        public bool i = false;

        public void Change()
        {
            ignore = true;
        }
    }
    public class IgnoreError
    {
        private readonly Ignore ignore = new Ignore();
        private static readonly IgnoreError ignoreError = new IgnoreError(); 
        public static bool GetDatai()
        {
            return ignoreError.ignore.i;
        }
        public static bool GetDataIgnore()
        {
            return ignoreError.ignore.ignore;
        }

        public static void Change(bool i)
        {
            if (i == true) ignoreError.ignore.ignore = true;
            ignoreError.ignore.i = true;
        }
    }
}
