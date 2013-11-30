using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IrcDotNet;

namespace TShockIRC
{
	public static class Extensions
	{
		const int maxCharsPerLine = 400;

		public static void SendMessage(this IIrcMessageTarget target, string msg)
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
	}
}
