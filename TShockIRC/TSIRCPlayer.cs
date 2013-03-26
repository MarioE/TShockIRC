using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TShockAPI;

namespace TShockIRC
{
	public class TSIRCPlayer : TSPlayer
	{
		public List<string> messages = new List<string>();

		public TSIRCPlayer(string name, Group group)
			: base(name)
		{
			Group = group;
		}
		
		public override void SendMessage(string msg)
		{
			messages.Add(msg);
		}
		public override void SendMessage(string msg, Color color)
		{
			messages.Add(msg);
		}
		public override void SendMessage(string msg, byte red, byte green, byte blue)
		{
			messages.Add(msg);
		}
		public override void SendErrorMessage(string msg)
		{
			messages.Add("\u000305" + msg);
		}
		public override void SendInfoMessage(string msg)
		{
			messages.Add("\u000302" + msg);
		}
		public override void SendSuccessMessage(string msg)
		{
			messages.Add("\u000303" + msg);
		}
		public override void SendWarningMessage(string msg)
		{
			messages.Add("\u000305" + msg);
		}
	}
}
