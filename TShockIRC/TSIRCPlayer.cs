using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TShockAPI;
using IrcDotNet;

namespace TShockIRC
{
	public class TSIrcPlayer : TSPlayer
	{
		const int MAX_CHARS_PER_LINE = 400;

		IIrcMessageTarget Target;

		public TSIrcPlayer(string name, Group group, IIrcMessageTarget target)
			: base(name)
		{
			Group = group;
			Target = target;
			UserAccountName = name;
		}

		public override void SendMessage(string msg)
		{
			TShockIRC.SendMessage(Target, msg);
		}
		public override void SendMessage(string msg, Color color)
		{
			TShockIRC.SendMessage(Target, msg);
		}
		public override void SendMessage(string msg, byte red, byte green, byte blue)
		{
			TShockIRC.SendMessage(Target, msg);
		}
		public override void SendErrorMessage(string msg)
		{
			TShockIRC.SendMessage(Target, "\u000305" + msg);
		}
		public override void SendInfoMessage(string msg)
		{
			TShockIRC.SendMessage(Target, "\u000302" + msg);
		}
		public override void SendSuccessMessage(string msg)
		{
			TShockIRC.SendMessage(Target, "\u000303" + msg);
		}
		public override void SendWarningMessage(string msg)
		{
			TShockIRC.SendMessage(Target, "\u000305" + msg);
		}
	}
}
