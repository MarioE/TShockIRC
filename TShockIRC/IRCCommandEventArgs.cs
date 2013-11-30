using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IrcDotNet;
using TShockAPI;

namespace TShockIRC
{
	public class IRCCommandEventArgs : EventArgs
	{
		List<string> parameters;
		public int Length { get { return parameters.Count - 1; } }
		public string RawText { get; private set; }
		public IrcUser Sender { get; private set; }
		public IIrcMessageTarget Target { get; private set; }

		public string this[int index] { get { return parameters[index + 1]; } }

		public IRCCommandEventArgs(string text, IrcUser sender, IIrcMessageTarget target)
		{
			parameters = IRCCommands.ParseParameters(text);
			RawText = text;
			Sender = sender;
			Target = target;
		}

		public string Eol(int index)
		{
			return String.Join(" ", parameters, index + 1, parameters.Count - index - 1);
		}
		public List<string> ParameterRange(int index, int count)
		{
			return parameters.GetRange(index + 1, count);
		}
	}
}
