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
		}

		static int Main(string[] args)
		{
			try
			{
				var options = new Options(args);

				var inputParameters = new ReaderParameters { ReadSymbols = true };
				var inputAssembly = AssemblyDefinition.ReadAssembly(options.InputAssembly, inputParameters);

				if (options.Version != null)
				{
					ChangeAssemblyNameVersion(inputAssembly, options.Version);
					ReplaceCustomAttribute<AssemblyVersionAttribute>(inputAssembly, options.Version);
					ReplaceCustomAttribute<AssemblyFileVersionAttribute>(inputAssembly, options.Version);
				}

				StrongNameKeyPair signingKey;
				using (var snkStream = new FileStream(options.SigningKey, FileMode.Open, FileAccess.Read))
				{
					signingKey = new StrongNameKeyPair(snkStream);
				}

				var outputParameters = new WriterParameters
				{
					WriteSymbols = true,
					StrongNameKeyPair = signingKey,
				};
				inputAssembly.Write(options.InputAssembly, outputParameters);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);
				return 2;
			}

			return 0;
		}

		private static void ChangeAssemblyNameVersion(AssemblyDefinition inputAssembly, string outputVersion)
		{
			var oldName = inputAssembly.Name.Name;
			var newVersion = new Version(outputVersion);
			inputAssembly.Name = new AssemblyNameDefinition(oldName, newVersion);
		}

		private static void ReplaceCustomAttribute<A>(AssemblyDefinition inputAssembly, params object[] arguments)
		{
			var module = inputAssembly.MainModule;

			var attributeType = typeof(A);
			var attributeTypeRef = module.Import(attributeType);

			var argumentTypes = arguments.Select(argument => argument.GetType()).ToArray();
			var attributeCtor = module.Import(attributeType.GetConstructor(argumentTypes));

			var customAttribute = new CustomAttribute(attributeCtor);
			foreach (var argument in arguments)
			{
				var typeRef = module.Import(argument.GetType());
				customAttribute.ConstructorArguments.Add(new CustomAttributeArgument(typeRef, argument));
			}

			var customAttributes = inputAssembly.CustomAttributes.Where(ca => ca.AttributeType != attributeTypeRef).ToList();
			customAttributes.Add(customAttribute);

			inputAssembly.CustomAttributes.Clear();
			foreach (var ca in customAttributes)
			{
				inputAssembly.CustomAttributes.Add(ca);
			}
		}
	}
}
