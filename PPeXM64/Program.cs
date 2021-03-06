﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.MemoryMappedFiles;
using PPeX;
using System.IO;
using System.Runtime;
using System.Timers;
using System.Runtime.InteropServices;

namespace PPeXM64
{
    public class Program
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;

        public static CompressedCache Cache;

        public static bool LogFiles = false;
        public static bool IsLoaded = false;

        public static bool Preloading = true;
        public static bool TurboMode = true;

        static PipeServer server;

        static Timer timer = new Timer(5000);

        static void Main(string[] args)
        {
            server = new PipeServer("PPEX");

#if DEBUG
            LogFiles = true;
#endif

            timer = new Timer(TurboMode ? 10000 : 5000);

            if (args.Length > 0 &&
                Directory.Exists(args[0]))
            {
                Core.Settings.PPXLocation = args[0];
            }
            else
            {
                Core.Settings.PPXLocation = Utility.GetGameDir() + "\\data";
            }

            Core.Settings.PPXLocation = Core.Settings.PPXLocation.Replace("\\\\", "\\");

            if (args.Length > 0 &&
                args.Any(x => x.ToLower() == "-nowindow"))
            {
                ShowWindow(GetConsoleWindow(), SW_HIDE);
            }
            
            //Attach the handler to the server
            server.OnRequest += Server_OnRequest;
            server.OnDisconnect += Server_OnDisconnect;

            if (Directory.Exists(Core.Settings.PPXLocation))
            {
                Console.WriteLine("Loading from " + Core.Settings.PPXLocation);

                List<ExtendedArchive> ArchivesToLoad = new List<ExtendedArchive>();

                //Index all .ppx files in the location
                foreach (string arc in Directory.EnumerateFiles(Core.Settings.PPXLocation, "*.ppx", SearchOption.TopDirectoryOnly).OrderBy(x => x))
                {
                    var archive = new ExtendedArchive(arc);

                    ArchivesToLoad.Add(archive);
                }

                Cache = new CompressedCache(ArchivesToLoad, new Progress<string>((x) =>
                {
                    Console.WriteLine(x);
                }));
            }
            else
                Console.WriteLine("Invalid load directory! (" + Core.Settings.PPXLocation + ")");

            timer.Elapsed += (s, e) =>
            {
                Cache.Trim((long)2 * 1024 * 1024 * 1024);
            };

            timer.Start();

            Console.WriteLine("Finished loading " + Cache.LoadedArchives.Count + " archive(s)");

            if (Preloading)
            {
                Console.WriteLine("Preloading files...");

                foreach (var chunk in Cache.LoadedFiles.Where(x => x.Key.File.EndsWith(".lst")).Select(x => x.Value.Chunk).Distinct())
                    chunk.Allocate();

                foreach (var chunk in Cache.LoadedFiles.Where(x => x.Key.Archive.StartsWith("jg2p06")).Select(x => x.Value.Chunk).Distinct())
                    chunk.Allocate();

                //foreach (var chunk in Cache.LoadedFiles.Where(x => x.Key.Archive.StartsWith("jg2p07")).Select(x => x.Value.Chunk).Distinct())
                //    chunk.Allocate();

                Console.WriteLine("Preloading complete.");
            }

            Cache.Trim(TrimMethod.GCCompactOnly);

            IsLoaded = true;

            string line;

            //Handle arguments from the user
            while (true)
            {
                line = Console.ReadLine();

                string[] arguments = line.Split(' ');

                switch (arguments[0])
                {
                    case "exit":
                        Environment.Exit(0);
                        return;
                    case "log":
                        LogFiles = !LogFiles;
                        break;
                    case "turbo":
                        TurboMode = !TurboMode;
                        break;
                    case "size":
                        Console.WriteLine(Utility.GetBytesReadable(Cache.AllocatedMemorySize));
                        Console.WriteLine(Cache.TotalFiles.Count(x => x.Allocated) + " files allocated");
                        break;
                    case "trim":
                        Cache.Trim((long)(float.Parse(arguments[1]) * 1024 * 1024));
                        break;
                }
            }
        }
        
        

        /// <summary>
        /// Handler for any pipe requests.
        /// </summary>
        /// <param name="request">The command to execute.</param>
        /// <param name="argument">Any additional arguments.</param>
        /// <param name="handler">The streamhandler to use.</param>
        private static void Server_OnRequest(string request, string argument, StreamHandler handler)
        {
            if (request == "ready")
            {
                //Notify the AA2 instance that we are ready
                if (IsLoaded)
                    Console.WriteLine("Connected to pipe");

                handler.WriteString(IsLoaded.ToString());
            }
            else if (request == "matchfiles")
            {
                //Send a list of all loaded .pp files

                if (LogFiles)
                    Console.WriteLine("!!LOADED FILELIST!!");

                var loadedPP = Cache.TotalFiles.Select(x => x.ArchiveName).Distinct();
                
                foreach (string pp in loadedPP)
                    handler.WriteString(pp);

                handler.WriteString("");
            }
            else if (request == "load")
            {
                //Transfer the file
                lock (Cache.LoadLock)
                {
                    string[] splitNames = argument.Replace("data/", "").Split('/');
                    FileEntry entry = new FileEntry(splitNames[0], splitNames[1]);

                    //Ensure we have the file
                    if (!Cache.LoadedFiles.ContainsKey(entry))
                    {
                        //We don't have the file
                        handler.WriteString("NotAvailable");

                        if (LogFiles)
                            Console.WriteLine("!" + argument);

                        return;
                    }

                    if (LogFiles)
                        Console.WriteLine(argument);

                    //Write the data to the pipe
                    using (BinaryWriter writer = new BinaryWriter(handler.BaseStream, Encoding.Unicode, true))
                    {
                        CachedFile cached = Cache.LoadedFiles[entry];
                        
                        using (Stream output = cached.GetStream())
                        {
                            handler.WriteString(output.Length.ToString());

                            output.CopyTo(handler.BaseStream);
                        }
                    }
                }
            }
            else
            {
                //Unknown command
                //Ignore instead of throwing exception
                Console.WriteLine("Unknown request: " + request + " [:] " + argument);
            }
        }

        private static void Server_OnDisconnect(object sender, EventArgs e)
        {
            Cache.Trim(TrimMethod.All); //deallocate everything

            Console.WriteLine("Pipe disconnected, game has closed");

            System.Threading.Thread.Sleep(1500);
            Environment.Exit(0);
        }
    }
}
