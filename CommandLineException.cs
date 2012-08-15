using System;

namespace VersionOne.SigVer
{
	internal class CommandLineException : Exception
	{
		public CommandLineException(string message) : base(message)
		{
		}
	}
}