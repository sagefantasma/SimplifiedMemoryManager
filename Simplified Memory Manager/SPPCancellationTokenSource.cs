using System;
using System.Threading;

namespace SimplifiedMemoryManager
{
	public class SPPCancellationTokenSource : CancellationTokenSource
	{
		public int CancellationThreshold {get;set;}
		private readonly static object CancellationLock = new object();
		
		public new void Cancel()
		{
			lock(CancellationLock)
			{
				CancellationThreshold--;
				if(CancellationThreshold == 0)
				{
					base.Cancel();
				}
			}
		}
		
		public SPPCancellationTokenSource(int cancellationThreshold = 1) : base()
		{
			CancellationThreshold = cancellationThreshold;
		}

		public SPPCancellationTokenSource(int msDelay, int cancellationThreshold = 1) : base(msDelay)
		{
			CancellationThreshold = cancellationThreshold;
		}
		
		public SPPCancellationTokenSource(TimeSpan delay, int cancellationThreshold = 1) : base(delay)
		{
			CancellationThreshold = cancellationThreshold;
		}
	}
}
