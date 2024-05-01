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
		public byte[] Data { get; set; }
		private CancellationToken Token { get; set; }
		private SimplePattern Pattern { get; set; }
		public EventHandler<MatchFoundEventArgs> PatternMatched { get; private set;}
		public bool ThreadRequestedCancellation { get; set; } = false;
		
		public ScanThread(SimplePattern pattern, CancellationToken token, EventHandler<MatchFoundEventArgs> patternMatched)
		{
			Pattern = pattern;
			Token = token;
			PatternMatched += patternMatched;
		}
		
		public class MatchFoundEventArgs : EventArgs
		{
			public int Index { get; set; }
			public MatchFoundEventArgs(int index)
			{
				Index = index;
			}
		}

		public void ScanForPattern(ref int foundPosition)
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
								if (currentExpression.Operand != Data[dataIndex + patternIndex])
								{
									//not a match, this slice is no good
									break;
								}
								else if (patternIndex == Pattern.ParsedPattern.Count - 1)
								{
									//perfect match
									foundPosition = dataIndex;
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
