using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Simplified_Memory_Manager
{
    internal class ScanThread
    {
        public byte[] Data { get; set; }
        public CancellationToken Token { get; set; }
        public SimplePattern Pattern { get; set; }

        public void ScanForPattern()
        {
            for(int index = 0; index < Data.Length; index++)
            {
                if (Data[index])
            }
        }
    }
}
