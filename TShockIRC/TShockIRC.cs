﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Hooks;
using IrcDotNet;
using IrcDotNet.Ctcp;
using Terraria;
using TShockAPI;
using TShockAPI.DB;

namespace TShockIRC
{
	[APIVersion(1, 12)]
	public class TShockIRC : TerrariaPlugin
	{
		public override string Author
		{
			get { return "MarioE"; }
		}
		private Config config = new Config();
		private CtcpClient ctcpClient;
		public override string Description
		{
			get { return "Acts as an IRC bot for the server."; }
		}
		private List<IrcChannel> ircChannels = new List<IrcChannel>();
		private IrcClient ircClient = new IrcClient();
		private List<IrcUser> ircUsers = new List<IrcUser>();
		private Dictionary<IrcUser, Group> loggedIn = new Dictionary<IrcUser, Group>();
		public override string Name
		{
			get { return "TShockIRC"; }
		}
		public override Version Version
		{
			get { return Assembly.GetExecutingAssembly().GetName().Version; }
		}

		public TShockIRC(Main game)
			: base(game)
		{
			Order = -1;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				GameHooks.Initialize -= OnInitialize;
				NetHooks.GetData -= OnGetData;
				ServerHooks.Chat -= OnChat;
				ServerHooks.Leave -= OnLeave;

				ircClient.Dispose();
			}
		}
		public override void Initialize()
		{
			GameHooks.Initialize += OnInitialize;
			NetHooks.GetData += OnGetData;
			ServerHooks.Chat += OnChat;
			ServerHooks.Leave += OnLeave;
		}

		void OnChat(messageBuffer msg, int plr, string text, HandledEventArgs e)
		{
			TSPlayer tsPlr = TShock.Players[plr];
			if (text.StartsWith("/"))
			{
				if (text.Length > 1)
				{
					if (text.StartsWith("/me ") && tsPlr.Group.HasPermission(Permissions.cantalkinthird) && !e.Handled && !tsPlr.mute)
					{
						ircClient.LocalUser.SendMessage(config.Channel, String.Format(config.ServerActionMessageFormat, tsPlr.Name, text.Substring(4)));
					}
					else if (config.LogCommands)
					{
						IEnumerable<Command> commands = Commands.ChatCommands.Where(c => c.HasAlias(IRCCommand.Parse(text.Substring(1))[0]));
						foreach (Command command in commands)
						{
							if (!command.DoLog)
							{
								return;
							}
						}

						ircClient.LocalUser.SendMessage(config.AdminChannel, String.Format(config.ServerCommandMessageFormat, tsPlr.Group.Prefix, tsPlr.Name, text.Substring(1)));
					}
				}
			}
			else
			{
                if (e.Handled | tsPlr.mute) return;
                ircClient.LocalUser.SendMessage(config.Channel, String.Format(config.ServerChatMessageFormat, tsPlr.Group.Prefix, tsPlr.Name, text));
			}
		}
		void OnGetData(GetDataEventArgs e)
		{
			switch (e.MsgID)
			{
				case PacketTypes.TileGetSection:
					TSPlayer tsPlr = TShock.Players[e.Msg.whoAmI];
					ircClient.LocalUser.SendMessage(config.Channel, String.Format(config.ServerJoinMessageFormat, tsPlr.Name));
                    if (config.LogIPs)
                    {
                        ircClient.LocalUser.SendMessage(config.AdminChannel, String.Format(config.ServerJoinIPMessageFormat, tsPlr.Name, tsPlr.IP));
                    }
					break;
			}
		}
		void OnInitialize()
		{
			Commands.ChatCommands.Add(new Command("tshockirc.op", IRCKick, "irckick"));
			Commands.ChatCommands.Add(new Command("tshockirc.manage", IRCReload, "ircreload"));
			Commands.ChatCommands.Add(new Command("tshockirc.manage", IRCRestart, "ircrestart"));
			Commands.ChatCommands.Add(new Command("tshockirc.op", IRCKickban, "irckickban"));
			IRCCommand.Commands.Add(new IRCCommand("tshockirc.command", Command, "c", "command", "exec", "execute"));
			IRCCommand.Commands.Add(new IRCCommand("", Login, "login"));
			IRCCommand.Commands.Add(new IRCCommand("", Logout, "logout"));
			IRCCommand.Commands.Add(new IRCCommand("", Players, "online", "players", "who"));

			string configPath = Path.Combine(TShock.SavePath, "tshockircconfig.json");
			if (File.Exists(configPath))
			{
				config = Config.Read(configPath);
			}
			else
			{
				config.Write(configPath);
			}

			IrcUserRegistrationInfo ircInfo = new IrcUserRegistrationInfo()
			{
				NickName = config.Nick,
				RealName = config.RealName,
				UserName = config.UserName,
				UserModes = new List<char> { 'i', 'w' }
			};
			ircClient.Connect(config.Server, config.Port, config.SSL, ircInfo);
			ircClient.Registered += OnIRCRegistered;
			ctcpClient = new CtcpClient(ircClient) { ClientVersion = "TShockIRC v" + Assembly.GetExecutingAssembly().GetName().Version };
		}
		void OnLeave(int plr)
		{
			if (TShock.Players[plr] != null && !String.IsNullOrEmpty(TShock.Players[plr].Name))
			{
				ircClient.LocalUser.SendMessage(config.Channel, String.Format(config.ServerLeaveMessageFormat, TShock.Players[plr].Name));
			}
		}

		void IRCKick(CommandArgs e)
		{
			if (e.Parameters.Count == 0)
			{
				e.Player.SendErrorMessage("Invalid syntax! Proper syntax: /irckick <user> [reason]");
				return;
			}

			int counts = 0;
			string reason = e.Parameters.Count > 1 ? String.Join(" ", e.Parameters.ToArray(), 1, e.Parameters.Count - 1) : "Misbehavior.";
			IrcUser toKick = null;

			foreach (IrcUser ircUser in ircUsers)
			{
				if (ircUser.NickName == e.Parameters[0])
				{
					ircChannels.Find(ic => ic.Name == config.Channel).Kick(ircUser.NickName, reason);
					e.Player.SendSuccessMessage("You have kicked " + ircUser.NickName + " (" + reason + ").");
					return;
				}
				if (ircUser.NickName.ToLower().StartsWith(e.Parameters[0]))
				{
					counts++;
					toKick = ircUser;
				}
			}

			if (counts == 0)
			{
				e.Player.SendErrorMessage("Invalid user.");
			}
			else if (counts > 1)
			{
				e.Player.SendErrorMessage("More than one user matched the input (" + counts + ").");
			}
			else
			{
				ircChannels.Find(ic => ic.Name == config.Channel).Kick(toKick.NickName, reason);
				e.Player.SendSuccessMessage("You have kicked " + toKick.NickName + " (" + reason + ").");
			}
		}
		void IRCKickban(CommandArgs e)
		{
			if (e.Parameters.Count == 0)
			{
				e.Player.SendErrorMessage("Invalid syntax! Proper syntax: /irckickban <user> [reason]");
				return;
			}

			int counts = 0;
			string reason = e.Parameters.Count > 1 ? String.Join(" ", e.Parameters.ToArray(), 1, e.Parameters.Count - 1) : "Misbehavior.";
			IrcUser toKick = null;

			foreach (IrcUser ircUser in ircUsers)
			{
				if (ircUser.NickName == e.Parameters[0])
				{
					ircChannels.Find(ic => ic.Name == config.Channel).SetModes("+b", "*!*@" + ircUser.HostName);
					ircChannels.Find(ic => ic.Name == config.Channel).Kick(ircUser.NickName, reason);
					e.Player.SendSuccessMessage("You have kickbanned " + ircUser.NickName + " (" + reason + ").");
					return;
				}
				if (ircUser.NickName.ToLower().StartsWith(e.Parameters[0]))
				{
					counts++;
					toKick = ircUser;
				}
			}

			if (counts == 0)
			{
				e.Player.SendErrorMessage("Invalid user.");
			}
			else if (counts > 1)
			{
				e.Player.SendErrorMessage("More than one user matched the input (" + counts + ").");
			}
			else
			{
				ircChannels.Find(ic => ic.Name == config.Channel).SetModes("+b", "*!*@" + toKick.HostName);
				ircChannels.Find(ic => ic.Name == config.Channel).Kick(toKick.NickName, reason);
				e.Player.SendSuccessMessage("You have kickbanned " + toKick.NickName + " (" + reason + ").");
			}
		}
		void IRCReload(CommandArgs e)
		{
			string configPath = Path.Combine(TShock.SavePath, "tshockircconfig.json");
			if (File.Exists(configPath))
			{
				config = Config.Read(configPath);
			}
			else
			{
				config.Write(configPath);
			}
			e.Player.SendSuccessMessage("Reloaded IRC config.");
		}
		void IRCRestart(CommandArgs e)
		{
			ircClient.Quit("Restarting...");
			IrcUserRegistrationInfo ircInfo = new IrcUserRegistrationInfo()
			{
				NickName = config.Nick,
				RealName = config.RealName,
				UserName = config.UserName,
				UserModes = new List<char> { 'i', 'w' }
			};
			ircClient = new IrcClient();
			ircClient.Connect(config.Server, config.Port, config.SSL, ircInfo);
			ircClient.Registered += OnIRCRegistered;
			ctcpClient = new CtcpClient(ircClient) { ClientVersion = "TShockIRC v" + Assembly.GetExecutingAssembly().GetName().Version };

			ircChannels.Clear();
			ircUsers.Clear();
			loggedIn.Clear();
			e.Player.SendInfoMessage("Restarted the IRC bot.");
		}

		void Command(object sender, IRCCommandEventArgs e)
		{
			if (e.Length == 1)
			{
				ircClient.LocalUser.SendMessage(e.SendTo, "\u00035Syntax: " + config.BotPrefix + e[0] + " <command> [arguments...]");
				return;
			}

			TSIRCPlayer tsIrcPlayer = new TSIRCPlayer(e.Sender.Name, e.SenderGroup);
			IEnumerable<Command> commands = Commands.ChatCommands.Where(c => c.HasAlias(e[1]));

			if (commands.Count() == 0)
			{
				ircClient.LocalUser.SendMessage(e.SendTo, "\u00035Invalid command.");
			}
			else
			{
				foreach (Command command in commands)
				{
					if (!command.CanRun(tsIrcPlayer))
					{
						ircClient.LocalUser.SendMessage(e.SendTo, "\u00035You do not have access to that command.");
					}
					else if (!command.AllowServer)
					{
						ircClient.LocalUser.SendMessage(e.SendTo, "\u00035You must use this command in-game.");
					}
					else
					{
						command.Run(e.RawText, tsIrcPlayer, e.ParameterRange(2, e.Length - 2).ToList());
					}
				}
				foreach (string msg in tsIrcPlayer.messages)
				{
					ircClient.LocalUser.SendMessage(e.SendTo, msg);
				}
			}
		}
		void Login(object sender, IRCCommandEventArgs e)
		{
			if (e.Length != 3)
			{
				ircClient.LocalUser.SendMessage(e.SendTo, "\u00035Syntax: " + config.BotPrefix + e[0] + " <user> <password>");
				return;
			}

			User user = TShock.Users.GetUserByName(e[1]);

			if (user == null)
			{
				ircClient.LocalUser.SendMessage(e.SendTo, "\u00035Invalid user.");
			}
			else
			{
				if (user.Password.ToUpper() == TShock.Utils.HashPassword(e[2]).ToUpper())
				{
					ircClient.LocalUser.SendMessage(e.SendTo, "\u00033You have logged in as " + e[1] + ".");
					loggedIn.Remove((IrcUser)e.Sender);
					loggedIn.Add((IrcUser)e.Sender, TShock.Utils.GetGroup(user.Group));
				}
				else
				{
					ircClient.LocalUser.SendMessage(e.SendTo, "\u00035Incorrect password!");
				}
			}
		}
		void Logout(object sender, IRCCommandEventArgs e)
		{
			if (loggedIn.ContainsKey((IrcUser)e.Sender))
			{
				loggedIn.Remove((IrcUser)e.Sender);
				ircClient.LocalUser.SendMessage(e.SendTo, "\u00033You have logged out.");
			}
			else
			{
				ircClient.LocalUser.SendMessage(e.SendTo, "\u00035You are not logged in.");
			}
		}
		void Players(object sender, IRCCommandEventArgs e)
		{
			int numPlayers = 0;
			string players = "";

			foreach (TSPlayer tsPlr in TShock.Players)
			{
				if (tsPlr != null && tsPlr.Active)
				{
					numPlayers++;
					if (players != "")
					{
						players += ", " + tsPlr.Name;
					}
					else
					{
						players = tsPlr.Name;
					}
				}
			}

			if (numPlayers == 0)
			{
				ircClient.LocalUser.SendMessage(e.SendTo, "0 players currently on.");
			}
			else
			{
				ircClient.LocalUser.SendMessage(e.SendTo, numPlayers + " player(s) currently on:");
				ircClient.LocalUser.SendMessage(e.SendTo, players + ".");
			}
		}

		void OnIRCRegistered(object sender, EventArgs e)
		{
			ircClient.SendRawMessage("PRIVMSG NickServ :IDENTIFY " + config.NickServPassword);
			ircClient.Channels.Join(new List<Tuple<string, string>> { Tuple.Create(config.Channel, config.ChannelKey) });
			ircClient.Channels.Join(new List<Tuple<string, string>> { Tuple.Create(config.AdminChannel, config.AdminChannelKey) });
			ircClient.LocalUser.JoinedChannel += OnIRCJoinedChannel;
			ircClient.LocalUser.MessageReceived += OnIRCMessageReceived;
		}
		void OnIRCJoinedChannel(object sender, IrcChannelEventArgs e)
		{
			IrcChannel channel = e.Channel;
			channel.MessageReceived += OnChannelMessage;
			channel.UserJoined += OnChannelJoined;
			channel.UserKicked += OnChannelKicked;
			channel.UserLeft += OnChannelLeft;
			channel.UsersListReceived += OnChannelUsersList;
			ircChannels.Add(channel);
		}
		void OnIRCMessageReceived(object sender, IrcMessageEventArgs e)
		{
			Group senderGroup;
			if (!loggedIn.TryGetValue((IrcUser)e.Source, out senderGroup))
			{
				senderGroup = TShock.Utils.GetGroup(TShock.Config.DefaultGuestGroupName);
			}

			IRCCommand.Execute(ircClient, e.Source, senderGroup, (IIrcMessageTarget)((IrcUser)e.Source), e.Text);
		}

		void OnChannelJoined(object sender, IrcChannelUserEventArgs e)
		{
			if (e.ChannelUser.Channel.Name == config.Channel)
			{
				ircUsers.Add(e.ChannelUser.User);
				e.ChannelUser.User.Quit += OnUserQuit;

				TSPlayer.Server.SendSuccessMessage(String.Format(config.IRCJoinMessageFormat, e.ChannelUser.User.NickName));
				TSPlayer.All.SendSuccessMessage(String.Format(config.IRCJoinMessageFormat, e.ChannelUser.User.NickName));
			}
		}
		void OnChannelKicked(object sender, IrcChannelUserEventArgs e)
		{
			if (e.ChannelUser.Channel.Name == config.Channel)
			{
				ircUsers.Remove(e.ChannelUser.User);
				loggedIn.Remove(e.ChannelUser.User);

				TSPlayer.Server.SendErrorMessage(String.Format(config.IRCKickMessageFormat, e.ChannelUser.User.NickName, e.Comment));
				TSPlayer.All.SendErrorMessage(String.Format(config.IRCKickMessageFormat, e.ChannelUser.User.NickName, e.Comment));
			}
		}
		void OnChannelLeft(object sender, IrcChannelUserEventArgs e)
		{
			if (e.ChannelUser.Channel.Name == config.Channel)
			{
				ircUsers.Remove(e.ChannelUser.User);
				loggedIn.Remove(e.ChannelUser.User);

				TSPlayer.Server.SendErrorMessage(String.Format(config.IRCLeaveMessageFormat, e.ChannelUser.User.NickName, e.Comment));
				TSPlayer.All.SendErrorMessage(String.Format(config.IRCLeaveMessageFormat, e.ChannelUser.User.NickName, e.Comment));
			}
		}
		void OnChannelMessage(object sender, IrcMessageEventArgs e)
		{
			if (e.Text.StartsWith(config.BotPrefix))
			{
				Group senderGroup;
				if (!loggedIn.TryGetValue((IrcUser)e.Source, out senderGroup))
				{
					senderGroup = TShock.Utils.GetGroup(TShock.Config.DefaultGuestGroupName);
				}

				IRCCommand.Execute(ircClient, e.Source, senderGroup, (IIrcMessageTarget)sender, e.Text.Substring(config.BotPrefix.Length));
			}
			else if (((IrcChannel)e.Targets[0]).Name == config.Channel)
			{
				string text = e.Text;
				text = System.Text.RegularExpressions.Regex.Replace(text, "\u0003[0-9]{1,2}", "");
				text = text.Replace("\u0002", "");
				text = text.Replace("\u000f", "");
				text = text.Replace("\u000f", "");

				if (text.StartsWith("\u0001ACTION"))
				{
					TSPlayer.Server.SendMessage(String.Format(config.IRCActionMessageFormat, e.Source.Name, text.Substring(8, text.Length - 9)), 205, 133, 63);
					TSPlayer.All.SendMessage(String.Format(config.IRCActionMessageFormat, e.Source.Name, text.Substring(8, text.Length - 9)), 205, 133, 63);
				}
				else if (loggedIn.ContainsKey((IrcUser)e.Source))
				{
					Group group = loggedIn[(IrcUser)e.Source];
					TSPlayer.Server.SendMessage(String.Format(config.IRCChatMessageFormat, group.Prefix, e.Source.Name, text), group.R, group.G, group.B);
					TSPlayer.All.SendMessage(String.Format(config.IRCChatMessageFormat, group.Prefix, e.Source.Name, text), group.R, group.G, group.B);
				}
				else
				{
					TSPlayer.Server.SendMessage(String.Format(config.IRCChatMessageFormat, "", e.Source.Name, text), Color.White);
					TSPlayer.All.SendMessage(String.Format(config.IRCChatMessageFormat, "", e.Source.Name, text), Color.White);
				}
			}
		}
		void OnChannelUsersList(object sender, EventArgs e)
		{
			if (((IrcChannel)sender).Name == config.Channel)
			{
				foreach (IrcChannelUser ircChannelUser in ((IrcChannel)sender).Users)
				{
					ircUsers.Add(ircChannelUser.User);
					ircChannelUser.User.Quit += OnUserQuit;
				}
			}
		}

		void OnUserQuit(object sender, IrcCommentEventArgs e)
		{
			ircUsers.Remove((IrcUser)sender);
			loggedIn.Remove((IrcUser)sender);

			TSPlayer.Server.SendErrorMessage(String.Format(config.IRCQuitMessageFormat, ((IrcUser)sender).NickName, e.Comment));
			TSPlayer.All.SendErrorMessage(String.Format(config.IRCQuitMessageFormat, ((IrcUser)sender).NickName, e.Comment));
		}
	}
}