using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IrcDotNet;
using TShockAPI;

namespace TShockIRC
{
	public class IRCCommand
	{
		EventHandler<IRCCommandEventArgs> callback;
		public bool DoLog { get; set; }
		public string[] Names { get; private set; }
		public string Permission { get; private set; }

		public static List<IRCCommand> Commands = new List<IRCCommand>();

		public IRCCommand(string permission, EventHandler<IRCCommandEventArgs> command, params string[] names)
		{
			callback = command;
			DoLog = true;
			Names = names.Select(s => s.ToLower()).ToArray();
			Permission = permission;
		}

		public void Execute(IRCCommandEventArgs e)
		{
			callback(this, e);
		}
	}
}
