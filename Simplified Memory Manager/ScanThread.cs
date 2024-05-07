using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SimplifiedMemoryManager
{

	public class ScanThread
	{
		private static ScanManager Manager { get; set; }
		public byte[] Data { get; set; }
		private CancellationToken Token { get; set; }
		private SimplePattern Pattern { get; set; }
		public EventHandler<MatchFoundEventArgs> PatternMatched { get; private set;}
		public IntPtr StartingPosition { get; set; }
		public bool ThreadRequestedCancellation { get; set; } = false;
		
		public ScanThread(SimplePattern pattern, CancellationToken token, EventHandler<MatchFoundEventArgs> patternMatched, ScanManager manager, IntPtr startingPosition = default)
		{
			Pattern = pattern;
			Token = token;
			PatternMatched += patternMatched;
			Manager = manager;
			StartingPosition = startingPosition;
		}
		
		public class MatchFoundEventArgs : EventArgs
		{
			public MatchFoundEventArgs(IntPtr index)
			{
                Manager.ScanResult.Add(index);
			}
		}

		public void ScanForPattern(ref IntPtr foundPosition)
		{
			try
			{
				for(int dataIndex = 0; dataIndex < Data.Length; dataIndex++)
				{
					for(int patternIndex = 0; patternIndex < Pattern.ParsedPattern.Count; patternIndex++)
					{
						if (!Token.IsCancellationRequested)
						{
							PatternExpression currentExpression = Pattern.ParsedPattern[patternIndex];
							if (currentExpression.Operation == Operation.SkipOne)
							{
								continue;
							}
							else if (currentExpression.Operation == Operation.Exact)
							{
								if (dataIndex + patternIndex >= Data.Length)
									break;
								if (currentExpression.Operand != Data[dataIndex + patternIndex])
								{
									//not a match, this slice is no good
									break;
								}
								else if (patternIndex == Pattern.ParsedPattern.Count - 1)
								{
									//perfect match
									foundPosition = IntPtr.Add(StartingPosition, dataIndex);
									PatternMatched?.Invoke(this, new MatchFoundEventArgs(foundPosition));
								}
								else
								{
									//looking good so far, but not a perfect match yet
									continue;
								}
							}
						}
					}
				}
			}
			catch(Exception e)
			{
				//TODO: report this error up to the spawning class
			}
		}
	}
}
