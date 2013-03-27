using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TShockIRC
{
	public class Config
	{
		public string AdminChannel = "#admin";
		public string AdminChannelKey = "";
		public string Channel = "#terraria";
		public string ChannelKey = "";
		public bool LogCommands = false;
        public bool LogIPs = false;
		public string Nick = "TShock";
		public string NickServPassword = "";
		public short Port = 6667;
		public string RealName = "TShock";
		public string Server = "localhost";
		public bool SSL = false;
		public string UserName = "TShock";

		public string BotPrefix = ".ts ";
		public string IRCActionMessageFormat = "(IRC) * {0} {1}";
		public string IRCChatMessageFormat = "(IRC) {0}<{1}> {2}";
		public string IRCJoinMessageFormat = "(IRC) {0} has joined.";
		public string IRCKickMessageFormat = "(IRC) {0} was kicked ({1}).";
		public string IRCLeaveMessageFormat = "(IRC) {0} has left ({1}).";
		public string IRCQuitMessageFormat = "(IRC) {0} has quit ({1}).";
		public string ServerActionMessageFormat = "\u000302\u0002* {0}\u000f {1}";
		public string ServerCommandMessageFormat = "\u000302{0}<{1}>\u000f executed /{2}";
		public string ServerChatMessageFormat = "\u000302{0}<{1}>\u000f {2}";
		public string ServerJoinMessageFormat = "\u000303{0} has joined.";
        public string ServerJoinIPMessageFormat = "\u000303{0} has joined. IP: {1}";
		public string ServerLeaveMessageFormat = "\u000305{0} has left.";

		public void Write(string path)
		{
			File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
		}

		public static Config Read(string path)
		{
			if (!File.Exists(path))
			{
				return new Config();
			}
			return JsonConvert.DeserializeObject<Config>(File.ReadAllText(path));
		}
	}
}
