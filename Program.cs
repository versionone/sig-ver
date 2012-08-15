using System;
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
			public Options(string[] args)
			{
				if (args.Length < 1)
					throw new CommandLineException("<input-assembly> is required");
				else
					InputAssembly = args[0];

				if (args.Length < 2)
					throw new CommandLineException("<signing-key> is required");
				else
					SigningKey = args[1];

				if (args.Length > 2)
					Version = args[2];
			}

			public string InputAssembly { get; set; }
			public string SigningKey { get; set; }
			public string Version { get; set; }
		}

		static int Main(string[] args)
		{
			try
			{
				var options = new Options(args);

				string inputFilename = options.InputAssembly;
				string snkFilename = options.SigningKey;
				string outputFilename = inputFilename;
				string outputVersion = options.Version;

				ReaderParameters inputParameters = new ReaderParameters() { ReadSymbols = true };
				var inputAssembly = AssemblyDefinition.ReadAssembly(inputFilename, inputParameters);

				if (outputVersion != null)
				{
					ChangeAssemblyNameVersion(inputAssembly, outputVersion);
					ReplaceCustomAttribute<AssemblyVersionAttribute>(inputAssembly, outputVersion);
					ReplaceCustomAttribute<AssemblyFileVersionAttribute>(inputAssembly, outputVersion);
				}

				StrongNameKeyPair signingKey;
				using (var snkStream = new FileStream(snkFilename, FileMode.Open, FileAccess.Read))
				{
					signingKey = new StrongNameKeyPair(snkStream);
				}

				WriterParameters outputParameters = new WriterParameters()
				{
					WriteSymbols = true,
					StrongNameKeyPair = signingKey,
				};
				inputAssembly.Write(outputFilename, outputParameters);
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
