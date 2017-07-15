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
}
