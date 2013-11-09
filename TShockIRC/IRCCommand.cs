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
		EventHandler<IRCCommandEventArgs> command;
		string[] names;
		string permission;

		public static List<IRCCommand> Commands = new List<IRCCommand>();

		public IRCCommand(string permission, EventHandler<IRCCommandEventArgs> command, params string[] names)
		{
			this.command = command;
			this.names = names.Select(s => s.ToLower()).ToArray();
			this.permission = permission;
		}

		public static void Execute(IrcClient client, IIrcMessageSource sender, Group senderGroup, IIrcMessageTarget sendTo, string text)
		{
			var ircCommands = Commands.Where(c => c.names.Contains(Parse(text)[0]));

			if (ircCommands.Count() == 0)
				client.LocalUser.SendMessage(sendTo, "\u00035Invalid command.");
			else
			{
				foreach (IRCCommand ircCommand in ircCommands)
				{
					if (String.IsNullOrEmpty(ircCommand.permission) || senderGroup.HasPermission(ircCommand.permission))
					{
						IRCCommandEventArgs e = new IRCCommandEventArgs(text, sender, senderGroup, sendTo);
						ircCommand.command(sendTo, e);
					}
					else
						client.LocalUser.SendMessage(sendTo, "\u00035You do not have permission to use this command.");
				}
			}
		}
		public static List<string> Parse(string text)
		{
			var parameters = new List<string>();
			var temp = new StringBuilder();
			bool quotes = false;

			for (int i = 0; i < text.Length; i++)
			{
				if (text[i] == '"')
					quotes = !quotes;
				if (text[i] != ' ' || (text[i] == ' ' && quotes))
					temp.Append(text[i]);
				if ((text[i] == ' ' || i == text.Length - 1) && !quotes)
				{
					parameters.Add(temp.ToString().StartsWith("\"") ? temp.ToString().Substring(1, temp.Length - 2) : temp.ToString());
					temp.Clear();
				}
			}

			return parameters;
		}
	}
}
