using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using IrcDotNet;
using IrcDotNet.Ctcp;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;

namespace TShockIRC
{
	[ApiVersion(1, 14)]
	public class TShockIRC : TerrariaPlugin
	{
		public override string Author
		{
			get { return "MarioE"; }
		}
		Config config = new Config();
		CtcpClient ctcpClient;
		public override string Description
		{
			get { return "Acts as an IRC bot for the server."; }
		}
		List<IrcChannel> ircChannels = new List<IrcChannel>();
		IrcClient ircClient = new IrcClient();
		List<IrcUser> ircUsers = new List<IrcUser>();
		Dictionary<IrcUser, Group> loggedIn = new Dictionary<IrcUser, Group>();
		const int maxCharsPerLine = 400;
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
			Order = 5;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
				ServerApi.Hooks.NetGreetPlayer.Deregister(this, OnGreetPlayer);
				ServerApi.Hooks.ServerChat.Deregister(this, OnChat);
				ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);

				ircClient.Dispose();
			}
		}
		public override void Initialize()
		{
			ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
			ServerApi.Hooks.NetGreetPlayer.Register(this, OnGreetPlayer);
			ServerApi.Hooks.ServerChat.Register(this, OnChat);
			ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
		}
		void SendMessage(IIrcMessageTarget target, string msg)
		{
			msg = msg.Replace("\0", "");
			msg = msg.Replace("\r", "");
			msg = msg.Replace("\n", "");

			StringBuilder sb = new StringBuilder();
			foreach (string word in msg.Split(' '))
			{
				if (sb.Length + word.Length + 1 > maxCharsPerLine)
				{
					ircClient.LocalUser.SendMessage(target, sb.ToString());
					sb.Clear();
				}
				else
					sb.Append(word).Append(" ");
			}
			ircClient.LocalUser.SendMessage(target, sb.ToString());
		}
		void SendMessage(string target, string msg)
		{
			msg = msg.Replace("\0", "");
			msg = msg.Replace("\r", "");
			msg = msg.Replace("\n", "");

			StringBuilder sb = new StringBuilder();
			foreach (string word in msg.Split(' '))
			{
				if (sb.Length + word.Length + 1 > maxCharsPerLine)
				{
					ircClient.LocalUser.SendMessage(target, sb.ToString());
					sb.Clear();
				}
				else
					sb.Append(word).Append(" ");
			}
			ircClient.LocalUser.SendMessage(target, sb.ToString());
		}

		void OnChat(ServerChatEventArgs e)
		{
			TSPlayer tsPlr = TShock.Players[e.Who];
			if (e.Text != null && tsPlr != null)
			{
				if (e.Text.StartsWith("/"))
				{
					if (e.Text.Length > 1)
					{
						if (e.Text.StartsWith("/me ") && tsPlr.Group.HasPermission(Permissions.cantalkinthird) && !e.Handled && !tsPlr.mute)
						{
							if (!String.IsNullOrEmpty(config.ServerActionMessageFormat))
							{
								SendMessage(config.Channel, String.Format(
									config.ServerActionMessageFormat, tsPlr.Name, e.Text.Substring(4)));
							}
						}
						else if (Commands.ChatCommands.Where(c => c.HasAlias(e.Text.Substring(1).Split(' ')[0].ToLower())).Any(c => c.DoLog))
						{
							if (!String.IsNullOrEmpty(config.ServerCommandMessageFormat))
							{
								SendMessage(config.AdminChannel, String.Format(
									config.ServerCommandMessageFormat, tsPlr.Group.Prefix, tsPlr.Name, e.Text.Substring(1)));
							}
						}
					}
				}
				else
				{
					if (!e.Handled && !tsPlr.mute && tsPlr.Group.HasPermission(Permissions.canchat) &&
						!String.IsNullOrEmpty(config.ServerChatMessageFormat))
					{
						SendMessage(config.Channel, String.Format(
							config.ServerChatMessageFormat, tsPlr.Group.Prefix, tsPlr.Name, e.Text));
					}
				}
			}
		}
		void OnGreetPlayer(GreetPlayerEventArgs e)
		{
			TSPlayer tsPlr = TShock.Players[e.Who];
			if (!String.IsNullOrEmpty(config.ServerJoinMessageFormat))
			{
				SendMessage(config.Channel, String.Format(
					config.ServerJoinMessageFormat, tsPlr.Name));
			}
			if (!String.IsNullOrEmpty(config.ServerJoinIPMessageFormat))
			{
				SendMessage(config.AdminChannel, String.Format(
					config.ServerJoinIPMessageFormat, tsPlr.Name, tsPlr.IP));
			}
		}
		void OnInitialize(EventArgs e)
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
				config = Config.Read(configPath);
			else
				config.Write(configPath);

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
		void OnLeave(LeaveEventArgs e)
		{
			TSPlayer tsplr = TShock.Players[e.Who];
			if (tsplr != null && tsplr.ReceivedInfo && tsplr.State >= 3 && !tsplr.SilentKickInProgress)
			{
				if (!String.IsNullOrEmpty(config.ServerLeaveMessageFormat))
				{
					SendMessage(config.Channel, String.Format(
						config.ServerLeaveMessageFormat, TShock.Players[e.Who].Name));
				}
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
					e.Player.SendSuccessMessage("You have kicked {0} for {1}.", ircUser.NickName, reason);
					return;
				}
				if (ircUser.NickName.ToLower().StartsWith(e.Parameters[0]))
				{
					counts++;
					toKick = ircUser;
				}
			}

			if (counts == 0)
				e.Player.SendErrorMessage("Invalid user.");
			else if (counts > 1)
				e.Player.SendErrorMessage("More than one user matched the input.");
			else
			{
				ircChannels.Find(ic => ic.Name == config.Channel).Kick(toKick.NickName, reason);
				e.Player.SendSuccessMessage("You have kicked {0} for {1}.", toKick.NickName, reason);
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
					e.Player.SendSuccessMessage("You have kickbanned {0} for {1}.", ircUser.NickName, reason);
					return;
				}
				if (ircUser.NickName.ToLower().StartsWith(e.Parameters[0]))
				{
					counts++;
					toKick = ircUser;
				}
			}

			if (counts == 0)
				e.Player.SendErrorMessage("Invalid user.");
			else if (counts > 1)
				e.Player.SendErrorMessage("More than one user matched the input.");
			else
			{
				ircChannels.Find(ic => ic.Name == config.Channel).SetModes("+b", "*!*@" + toKick.HostName);
				ircChannels.Find(ic => ic.Name == config.Channel).Kick(toKick.NickName, reason);
				e.Player.SendSuccessMessage("You have kickbanned {0} for {1}.", toKick.NickName, reason);
			}
		}
		void IRCReload(CommandArgs e)
		{
			string configPath = Path.Combine(TShock.SavePath, "tshockircconfig.json");
			if (File.Exists(configPath))
				config = Config.Read(configPath);
			else
				config.Write(configPath);
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
				SendMessage(e.SendTo, "\u00035Invalid syntax! Proper syntax: " + config.BotPrefix + e[0] + " <command> [arguments...]");
				return;
			}

			var tsIrcPlayer = new TSIRCPlayer(e.Sender.Name, e.SenderGroup);
			var commands = Commands.ChatCommands.Where(c => c.HasAlias(e[1]));

			if (commands.Count() == 0)
				SendMessage(e.SendTo, "\u00035Invalid command.");
			else
			{
				foreach (Command command in commands)
				{
					if (!command.CanRun(tsIrcPlayer))
						SendMessage(e.SendTo, "\u00035You do not have access to that command.");
					else if (!command.AllowServer)
						SendMessage(e.SendTo, "\u00035You must use this command in-game.");
					else
						command.Run(e.RawText, tsIrcPlayer, e.ParameterRange(2, e.Length - 2).ToList());
				}
				foreach (string msg in tsIrcPlayer.messages)
					SendMessage(e.SendTo, msg);
			}
		}
		void Login(object sender, IRCCommandEventArgs e)
		{
			if (e.Length != 3)
			{
				SendMessage(e.SendTo, "\u00035Invalid syntax! Proper syntax: " + config.BotPrefix + e[0] + " <user> <password>");
				return;
			}

			User user = TShock.Users.GetUserByName(e[1]);
			if (user == null || e[1] == "")
				SendMessage(e.SendTo, "\u00035Invalid user.");
			else
			{
				if (user.Password.ToUpper() == TShock.Utils.HashPassword(e[2]).ToUpper())
				{
					SendMessage(e.SendTo, "\u00033You have logged in as " + e[1] + ".");
					loggedIn.Remove((IrcUser)e.Sender);
					loggedIn.Add((IrcUser)e.Sender, TShock.Utils.GetGroup(user.Group));
				}
				else
					SendMessage(e.SendTo, "\u00035Incorrect password!");
			}
		}
		void Logout(object sender, IRCCommandEventArgs e)
		{
			if (loggedIn.ContainsKey((IrcUser)e.Sender))
			{
				loggedIn.Remove((IrcUser)e.Sender);
				SendMessage(e.SendTo, "\u00033You have logged out.");
			}
			else
				SendMessage(e.SendTo, "\u00035You are not already logged in.");
		}
		void Players(object sender, IRCCommandEventArgs e)
		{
			int numPlayers = 0;
			var players = new StringBuilder();

			foreach (TSPlayer tsPlr in TShock.Players)
			{
				if (tsPlr != null && tsPlr.Active)
				{
					numPlayers++;
					if (players.Length > 0)
						players.Append(", ").Append(tsPlr.Name);
					else
						players.Append(tsPlr.Name);
				}
			}

			if (numPlayers == 0)
				SendMessage(e.SendTo, "0 players currently on.");
			else
			{
				SendMessage(e.SendTo, numPlayers + " player(s) currently on:");
				SendMessage(e.SendTo, players + ".");
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
				senderGroup = TShock.Utils.GetGroup(TShock.Config.DefaultGuestGroupName);

			IRCCommand.Execute(ircClient, e.Source, senderGroup, (IIrcMessageTarget)((IrcUser)e.Source), e.Text);
		}

		void OnChannelJoined(object sender, IrcChannelUserEventArgs e)
		{
			if (String.Equals(e.ChannelUser.Channel.Name, config.Channel, StringComparison.OrdinalIgnoreCase))
			{
				ircUsers.Add(e.ChannelUser.User);
				e.ChannelUser.User.Quit += OnUserQuit;

				if (!String.IsNullOrEmpty(config.IRCJoinMessageFormat))
				{
					TSPlayer.Server.SendSuccessMessage(
						String.Format(config.IRCJoinMessageFormat, e.ChannelUser.User.NickName));
					TSPlayer.All.SendSuccessMessage(
						String.Format(config.IRCJoinMessageFormat, e.ChannelUser.User.NickName));
				}
			}
		}
		void OnChannelKicked(object sender, IrcChannelUserEventArgs e)
		{
			if (String.Equals(e.ChannelUser.Channel.Name, config.Channel, StringComparison.OrdinalIgnoreCase))
			{
				ircUsers.Remove(e.ChannelUser.User);
				loggedIn.Remove(e.ChannelUser.User);

				if (!String.IsNullOrEmpty(config.IRCKickMessageFormat))
				{
					TSPlayer.Server.SendErrorMessage(
						String.Format(config.IRCKickMessageFormat, e.ChannelUser.User.NickName, e.Comment));
					TSPlayer.All.SendErrorMessage(
						String.Format(config.IRCKickMessageFormat, e.ChannelUser.User.NickName, e.Comment));
				}
			}
		}
		void OnChannelLeft(object sender, IrcChannelUserEventArgs e)
		{
			if (String.Equals(e.ChannelUser.Channel.Name, config.Channel, StringComparison.OrdinalIgnoreCase))
			{
				ircUsers.Remove(e.ChannelUser.User);
				loggedIn.Remove(e.ChannelUser.User);

				if (!String.IsNullOrEmpty(config.IRCLeaveMessageFormat))
				{
					TSPlayer.Server.SendErrorMessage(
						String.Format(config.IRCLeaveMessageFormat, e.ChannelUser.User.NickName, e.Comment));
					TSPlayer.All.SendErrorMessage(
						String.Format(config.IRCLeaveMessageFormat, e.ChannelUser.User.NickName, e.Comment));
				}
			}
		}
		void OnChannelMessage(object sender, IrcMessageEventArgs e)
		{
			if (e.Targets.Count == 0)
				return;

			var ircChannel = ((IrcChannel)e.Targets[0]);
			if (e.Text.StartsWith(config.BotPrefix))
			{
				Group senderGroup;
				if (!loggedIn.TryGetValue((IrcUser)e.Source, out senderGroup))
				{
					senderGroup = TShock.Utils.GetGroup(TShock.Config.DefaultGuestGroupName);
				}

				IRCCommand.Execute(ircClient, e.Source, senderGroup, (IIrcMessageTarget)sender, e.Text.Substring(config.BotPrefix.Length));
			}
			else if (String.Equals(ircChannel.Name, config.Channel, StringComparison.OrdinalIgnoreCase))
			{
				IrcChannelUser ircChannelUser = ircChannel.GetChannelUser((IrcUser)e.Source);
				if (!String.IsNullOrEmpty(config.IRCChatModesRequired) && ircChannelUser != null)
				{
					bool hasMode = false;
					foreach (char c in config.IRCChatModesRequired)
					{
						if (ircChannelUser.Modes.Contains(c))
						{
							hasMode = true;
							break;
						}
					}
					if (!hasMode)
					{
						return;
					}
				}

				string text = e.Text;
				text = System.Text.RegularExpressions.Regex.Replace(text, "\u0003[0-9]{1,2}(,[0-9]{1,2})?", "");
				text = text.Replace("\u0002", "");
				text = text.Replace("\u000f", "");
				text = text.Replace("\u000f", "");

				if (text.StartsWith("\u0001ACTION") && text.EndsWith("\u0001"))
				{
					if (!String.IsNullOrEmpty(config.IRCActionMessageFormat))
					{
						TSPlayer.Server.SendMessage(
							String.Format(config.IRCActionMessageFormat, e.Source.Name, text.Substring(8, text.Length - 9)), 205, 133, 63);
						TSPlayer.All.SendMessage(
							String.Format(config.IRCActionMessageFormat, e.Source.Name, text.Substring(8, text.Length - 9)), 205, 133, 63);
					}
				}
				else if (loggedIn.ContainsKey((IrcUser)e.Source))
				{
					if (!String.IsNullOrEmpty(config.IRCChatMessageFormat))
					{
						Group group = loggedIn[(IrcUser)e.Source];
						TSPlayer.Server.SendMessage(
							String.Format(config.IRCChatMessageFormat, group.Prefix, e.Source.Name, text), group.R, group.G, group.B);
						TSPlayer.All.SendMessage(
							String.Format(config.IRCChatMessageFormat, group.Prefix, e.Source.Name, text), group.R, group.G, group.B);
					}
				}
				else
				{
					if (!String.IsNullOrEmpty(config.IRCChatMessageFormat))
					{
						TSPlayer.Server.SendMessage(String.Format(
							config.IRCChatMessageFormat, "", e.Source.Name, text), Color.White);
						TSPlayer.All.SendMessage(String.Format(
							config.IRCChatMessageFormat, "", e.Source.Name, text), Color.White);
					}
				}
			}
		}
		void OnChannelUsersList(object sender, EventArgs e)
		{
			if (String.Equals(((IrcChannel)sender).Name, config.Channel, StringComparison.OrdinalIgnoreCase))
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

			if (!String.IsNullOrEmpty(config.IRCQuitMessageFormat))
			{
				TSPlayer.Server.SendErrorMessage(
					String.Format(config.IRCQuitMessageFormat, ((IrcUser)sender).NickName, e.Comment));
				TSPlayer.All.SendErrorMessage(
					String.Format(config.IRCQuitMessageFormat, ((IrcUser)sender).NickName, e.Comment));
			}
		}
	}
}