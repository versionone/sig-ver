using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;

namespace VersionOne.SigVer
{
	static class Program
	{
		class Options
		{
			public Options(IList<string> args)
			{
				if (args.Count < 1)
					throw new CommandLineException("<input-assembly> is required");
				InputAssembly = args[0];

				if (args.Count < 2)
					throw new CommandLineException("<signing-key> is required");
				SigningKey = args[1];

				if (args.Count > 2)
					Version = args[2];
			}

			public string InputAssembly { get; private set; }
			public string SigningKey { get; private set; }
			public string Version { get; private set; }
			public string OutputAssembly { get { return InputAssembly; } }
		}

		static int Main(string[] args)
		{
			try
			{
				var options = new Options(args);

				var inputAssembly = AssemblyDefinition.ReadAssembly(options.InputAssembly);
				AddMetaInfo(inputAssembly, options.Version);

				if (options.Version != null)
				{
					ChangeAssemblyNameVersion(inputAssembly, options.Version);
				}

				StrongNameKeyPair signingKey;
				using (var snkStream = new FileStream(options.SigningKey, FileMode.Open, FileAccess.Read))
				{
					signingKey = new StrongNameKeyPair(snkStream);
				}

				var outputParameters = new WriterParameters
				{
					StrongNameKeyPair = signingKey,
				};
				inputAssembly.Write(options.OutputAssembly, outputParameters);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);
				return 2;
			}

			return 0;
		}

		private static void AddMetaInfo(AssemblyDefinition inputAssembly, string outputVersion)
		{
			if (outputVersion != null)
			{
				var infoVersion = string.Join(".", outputVersion.Split('.'), 0, 3);
				AddMetaInfo<AssemblyInformationalVersionAttribute, string>(inputAssembly, infoVersion);
			}
			AddMetaInfo<AssemblyTitleAttribute, string>(inputAssembly, "VersionOne Precompiled spark Views");
			AddMetaInfo<AssemblyCompanyAttribute, string>(inputAssembly, "VersionOne, Inc.");
			AddMetaInfo<AssemblyProductAttribute, string>(inputAssembly, "VersionOne");
			AddMetaInfo<AssemblyCopyrightAttribute, string>(inputAssembly, string.Format("Copyright {0}, VersionOne, Inc. All rights reserved.", DateTime.Today.Year));
		}

		private static void AddMetaInfo<A, T>(AssemblyDefinition inputAssembly, T value) where A : Attribute
		{
			var argumentType = inputAssembly.MainModule.Import(typeof(T));
			var argument = new CustomAttributeArgument(argumentType, value);
			var constructor = inputAssembly.MainModule.Import(typeof(A).GetConstructor(new[] { typeof(T) }));
			var attribute = new CustomAttribute(constructor) { ConstructorArguments = { argument } };
			inputAssembly.CustomAttributes.Add(attribute);
		}

		private static void ChangeAssemblyNameVersion(AssemblyDefinition inputAssembly, string outputVersion)
		{
			var oldName = inputAssembly.Name.Name;
			var newVersion = new Version(outputVersion);
			inputAssembly.Name = new AssemblyNameDefinition(oldName, newVersion);
		}
	}
}
