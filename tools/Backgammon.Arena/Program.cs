namespace Backgammon.Arena
{
    using System;
    using System.IO;

    /// <summary>
    /// The engine's workbench: test vectors, arenas between bots, level calibration, timing and speed benches. Run it
    /// in Release from a copy of the binaries (a running tool locks bin): <c>dotnet run -c Release -- &lt;command&gt;</c>.
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            var command = args.Length > 0 ? args[0] : "help";
            switch (command)
            {
                case "vectors":
                    VectorWriter.WriteAll(args.Length > 1 ? args[1] : FindVectorFolder());
                    return 0;

                default:
                    Console.WriteLine("Commands: vectors [folder]");
                    return command == "help" ? 0 : 1;
            }
        }

        public static string FindVectorFolder()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "test-vectors")))
            {
                directory = directory.Parent;
            }

            return directory == null
                ? throw new DirectoryNotFoundException("No test-vectors folder above " + AppContext.BaseDirectory)
                : Path.Combine(directory.FullName, "test-vectors");
        }
    }
}
