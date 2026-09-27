namespace Backgammon.Trainer
{
    using System;
    using System.Globalization;
    using System.Reflection;

    using Backgammon.Logic;

    /// <summary>
    /// The trainer's settings. <c>--name value</c> on the command line sets the property of that name (case and dashes
    /// ignored), so every setting is also a flag.
    /// </summary>
    internal sealed class TrainingSettings
    {
        public BackgammonVersion Version { get; set; } = BackgammonVersion.Obiknovena;

        public int Seed { get; set; } = 1;

        /// <summary>Gets or sets how many self-play games to train on.</summary>
        public long Games { get; set; } = 200_000;

        public int Hidden { get; set; } = 128;

        /// <summary>Gets or sets the step size per state at the start; it falls geometrically to <see cref="AlphaEnd"/>.</summary>
        public double Alpha { get; set; } = 0.02;

        public double AlphaEnd { get; set; } = 0.002;

        public double Lambda { get; set; } = 0.7;

        /// <summary>Gets or sets how many games are played with the same frozen network before their changes are applied.</summary>
        public int Batch { get; set; } = 32;

        /// <summary>Gets or sets the share of random plays at the start (none by default: the dice explore).</summary>
        public double Exploration { get; set; }

        public int Threads { get; set; } = Environment.ProcessorCount;

        /// <summary>Gets or sets how often (in games) to measure against the baseline.</summary>
        public long EvalEvery { get; set; } = 50_000;

        /// <summary>Gets or sets the duplicate pairs of matches (to the variant's usual target) for each measurement.</summary>
        public int EvalPairs { get; set; } = 400;

        /// <summary>Gets or sets the file the network is written to; checkpoints and the log go next to it.</summary>
        public string Out { get; set; } = "network.bin";

        /// <summary>Gets or sets a network to start from instead of random weights (for example обикновена's for челеби).</summary>
        public string? Init { get; set; }

        /// <summary>Gets or sets the network to measure (validate and stats commands).</summary>
        public string? Weights { get; set; }

        public static TrainingSettings Parse(string[] args, int start)
        {
            var settings = new TrainingSettings();
            for (var i = start; i < args.Length; i += 2)
            {
                var name = args[i].TrimStart('-').Replace("-", string.Empty, StringComparison.Ordinal);
                var property = typeof(TrainingSettings).GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                    ?? throw new ArgumentException($"Unknown setting {args[i]}.");
                var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                var value = type.IsEnum ? Enum.Parse(type, args[i + 1], ignoreCase: true) : Convert.ChangeType(args[i + 1], type, CultureInfo.InvariantCulture);
                property.SetValue(settings, value);
            }

            return settings;
        }

        public override string ToString() =>
            $"version {this.Version} seed {this.Seed} games {this.Games} hidden {this.Hidden} alpha {this.Alpha}->{this.AlphaEnd} " +
            $"lambda {this.Lambda} batch {this.Batch} exploration {this.Exploration}" + (this.Init != null ? $" init {System.IO.Path.GetFileName(this.Init)}" : string.Empty);
    }
}
