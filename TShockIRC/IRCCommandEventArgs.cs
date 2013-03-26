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
		private string[] parameters;
		public int Length { get { return parameters.Length; } }
		public string[] ParameterWithQuotes { get; private set; }
		public string RawText { get; private set; }
		public Group SenderGroup { get; private set; }
		public IIrcMessageSource Sender { get; private set; }
		public IIrcMessageTarget SendTo { get; private set; }

		public string this[int index] { get { return parameters[index]; } }

		public IRCCommandEventArgs(string text, IIrcMessageSource sender, Group senderGroup, IIrcMessageTarget sendTo)
		{
			parameters = IRCCommand.Parse(text).ToArray();
			ParameterWithQuotes = IRCCommand.Parse(text, true).ToArray();
			RawText = text;
			Sender = sender;
			SenderGroup = senderGroup;
			SendTo = sendTo;
		}

		public string[] ParameterRange(int index, int count)
		{
			return ParameterWithQuotes.ToList().GetRange(index, count).ToArray();
		}
	}
}
