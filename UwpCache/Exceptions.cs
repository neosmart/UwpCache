using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeoSmart.UwpCache
{
    public class KeyNotFoundException : Exception
    {
        public KeyNotFoundException(string key)
            : base($"The requested key \"{key}\" was not found in the cache")
        { }
    }

    public class IllegalKeyException : Exception
    {
        public IllegalKeyException()
            : base("A key including illegal characters was used. Change key or use a different FileNameStyle!")
        { }
    }
}
