using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IrcDotNet;
using TShockAPI;
using TShockAPI.DB;

namespace TShockIRC
{
	public static class IRCCommands
	{
		static List<IRCCommand> Commands = new List<IRCCommand>();

		public static void Execute(string str, IrcUser sender, IIrcMessageTarget target)
		{
			var args = new IRCCommandEventArgs(str, sender, target);

			string commandName = args[-1].ToLowerInvariant();
			IRCCommand command = Commands.FirstOrDefault(c => c.Names.Contains(commandName));
			if (command != null)
			{
				Group senderGroup = TShockIRC.IrcUsers[sender];
				if (String.IsNullOrEmpty(command.Permission) || senderGroup.HasPermission(command.Permission))
					command.Execute(args);
				else
					TShockIRC.SendMessage(target, "\u00035You do not have access to this command.");
			}
			else
				TShockIRC.SendMessage(target, "\u00035Invalid command.");
		}
		public static void Init()
		{
			Commands.Add(new IRCCommand("", Command, "c", "command", "exec", "execute"));
			Commands.Add(new IRCCommand("", Login, "login"));
			Commands.Add(new IRCCommand("", Logout, "logout"));
			Commands.Add(new IRCCommand("", Players, "online", "players", "who"));
		}
		public static List<string> ParseParameters(string text)
		{
			var parameters = new List<string>();
			var sb = new StringBuilder();

			bool quote = false;
			for (int i = 0; i < text.Length; i++)
			{
				char c = text[i];

				if (c == '\\' && ++i < text.Length)
				{
					if (text[i] != '"' && text[i] != ' ' && text[i] != '\\')
						sb.Append('\\');
					sb.Append(text[i]);
				}
				else if (c == '"')
				{
					quote = !quote;
					if (!quote || sb.Length > 0)
					{
						parameters.Add(sb.ToString());
						sb.Clear();
					}
				}
				else if (Char.IsWhiteSpace(c) && !quote)
				{
					if (sb.Length > 0)
					{
						parameters.Add(sb.ToString());
						sb.Clear();
					}
				}
				else
					sb.Append(c);
			}
			if (sb.Length > 0 || parameters.Count == 0)
				parameters.Add(sb.ToString());
			return parameters;
		}

		static void Command(object sender, IRCCommandEventArgs e)
		{
			if (e.Length == 0)
			{
				TShockIRC.SendMessage(e.Target, "\u00035Invalid syntax! Proper syntax: " + TShockIRC.Config.BotPrefix + e[-1] + " <command> [arguments...]");
				return;
			}

			Group group = TShockIRC.IrcUsers[e.Sender];

			var tsIrcPlayer = new TSIrcPlayer(e.Sender.NickName, group, e.Target);
			var commands = TShockAPI.Commands.ChatCommands.Where(c => c.HasAlias(e[0]));

			if (commands.Count() != 0)
			{
				foreach (Command command in commands)
				{
					if (!command.CanRun(tsIrcPlayer))
						TShockIRC.SendMessage(e.Target, "\u00035You do not have access to that command.");
					else if (!command.AllowServer)
						TShockIRC.SendMessage(e.Target, "\u00035You must use this command in-game.");
					else
					{
						var args = e.ParameterRange(1, e.Length - 1);
						if (TShockAPI.Hooks.PlayerHooks.OnPlayerCommand(tsIrcPlayer, command.Name, e.RawText, args))
							return;
						command.Run(e.RawText, tsIrcPlayer, args);
					}
				}
			}
			else
				TShockIRC.SendMessage(e.Target, "\u00035Invalid command.");
		}
		static void Login(object sender, IRCCommandEventArgs e)
		{
			if (e.Length != 2)
			{
				TShockIRC.SendMessage(e.Target, "\u00035Invalid syntax! Proper syntax: " + TShockIRC.Config.BotPrefix + e[-1] + " <user> <password>");
				return;
			}

			User user = TShock.Users.GetUserByName(e[0]);
			if (user == null || e[0] == "")
				TShockIRC.SendMessage(e.Target, "\u00035Invalid user.");
			else
			{
				if (String.Equals(user.Password, TShock.Utils.HashPassword(e[1]), StringComparison.OrdinalIgnoreCase))
				{
					TShockIRC.SendMessage(e.Target, "\u00033You have logged in as " + e[0] + ".");
					TShockIRC.IrcUsers[(IrcUser)e.Sender] = TShock.Utils.GetGroup(user.Group);
				}
				else
					TShockIRC.SendMessage(e.Target, "\u00035Incorrect password!");
			}
		}
		static void Logout(object sender, IRCCommandEventArgs e)
		{
			TShockIRC.IrcUsers[(IrcUser)e.Sender] = TShock.Groups.GetGroupByName(TShock.Config.DefaultGuestGroupName);
			TShockIRC.SendMessage(e.Target, "\u00033You have logged out.");
		}
		static void Players(object sender, IRCCommandEventArgs e)
		{
			int numPlayers = TShock.Players.Where(p => p != null && p.Active).Count();
			string players = String.Join(", ", TShock.Players.Where(p => p != null && p.Active).Select(p => p.Name));
			if (numPlayers == 0)
				TShockIRC.SendMessage(e.Target, "0 players currently on.");
			else
			{
				TShockIRC.SendMessage(e.Target, numPlayers + " player(s) currently on:");
				TShockIRC.SendMessage(e.Target, players + ".");
			}
		}
	}
}
