using System;
using Terraria;
using TerrariaApi.Server;

namespace Inspector
{
	[ApiVersion(2, 0)]
	public class InspectorPlugin : TerrariaPlugin
	{
		public override string Author => "death";
		public override string Description => "Adds tools for developers";
		public override string Name => "Inspector";
		public override Version Version => new Version(1, 0, 0);

		public InspectorPlugin(Main game) : base(game)
		{
		}

		public override void Initialize()
		{
			TShockAPI.Commands.ChatCommands.Add(new Commands.InspectCommand());

			Console.WriteLine($"{this.Name} initialised");
		}
	}
}
