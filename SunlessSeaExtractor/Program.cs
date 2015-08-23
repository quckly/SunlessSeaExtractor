using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace SunlessSeaExtractor
{
    class Program
    {
        static void Main(string[] args)
        {
            ArgumentReader argsReader = new ArgumentReader(args);

            if (argsReader.Empty)
            {
                return;
            }

            switch (argsReader.GetNextArgument("Action"))
            {
                case "-import":
                    {
                        switch (argsReader.GetNextArgument("Type of export"))
                        {
                            case "json":
                                {
                                    var ssetr = new SunlessSeaExtractor();

                                    var jsonDir = argsReader.GetNextArgument("Dir with json files.");
                                    var translatedDir = argsReader.GetNextArgument("Dir with translated files.");
                                    var outputDir = argsReader.GetNextArgument("Dir to output result.");

                                    var files = Directory.GetFiles(jsonDir, "*.json");
                                    
                                    foreach (var file in files)
                                    {
                                        string trFile = ReplaceExtensionAndPath(file, translatedDir, ".txt");

                                        if (!File.Exists(trFile))
                                            continue;

                                        using (StreamWriter sw = new StreamWriter(ReplaceExtensionAndPath(file, outputDir, ".json"), false, Encoding.UTF8)) // Output file
                                        using (StreamReader sr = new StreamReader(file, Encoding.UTF8)) // Input json file
                                        using (StreamReader trsr = new StreamReader(trFile, Encoding.GetEncoding(1251))) // Input translated file
                                        using (StreamWriter testlog = new StreamWriter("TestResult.log")) // Test Result Writer
                                        {
                                            ssetr.SrtToJson(sr, trsr, sw, testlog, file);
                                        }
                                    }

                                    break;
                                }

                        }

                        break;
                    }
                case "-export":
                    {
                        switch (argsReader.GetNextArgument("Type of export"))
                        {
                            case "json":
                                {
                                    var ssetr = new SunlessSeaExtractor();
                                    var inputDir = argsReader.GetNextArgument("Dir with json files.");
                                    var outputDir = argsReader.GetNextArgument("Dir to output srt.");

                                    var files = Directory.EnumerateFiles(inputDir, "*.json");

                                    foreach (var file in files)
                                    {
                                        using (StreamWriter sw = new StreamWriter(ReplaceExtensionAndPath(file, outputDir, ".txt"), false, Encoding.GetEncoding(1251)))
                                        {
                                            using (StreamReader sr = new StreamReader(file, Encoding.UTF8))
                                            {
                                                ssetr.JsonToSrt(sr, sw);
                                            }
                                        }
                                    }

                                    break;
                                }

                        }

                        break;
                    }
            }
        }

        public static string ReplaceExtensionAndPath(string file, string newPath, string newExtension)
        {
            return GetNormalPathToFile(Path.GetFileNameWithoutExtension(file) + newExtension, newPath);
        }

        public static string GetNormalPathToFile(string file, string directory)
        {
            char last = directory.Last();
            directory = (last == '/' || last == '\\') ? directory : directory + '\\';

            //if (file.Contains('\\') || file.Contains('/'))
            //    return directory + file.Substring(file.Last(x => x == '\\' || x == '/') + 1);
            //else
                return directory + file;
        }
    }

    public class ArgumentReader
    {
        private String[] args;
        private int currentArg;

        public ArgumentReader(String[] args)
        {
            this.args = args.Clone() as String[];
            currentArg = 0;
        }

        public string GetNextArgument()
        {
            if (currentArg >= args.Length)
                throw new IndexOutOfRangeException(string.Format("Error on {0} argument", currentArg));

            return args[currentArg++].ToLower();
        }

        public string GetNextArgument(String argDescription)
        {
            if (currentArg >= args.Length)
                throw new IndexOutOfRangeException(string.Format("Error on {0} argument. Argument must be {1}", currentArg, argDescription));

            return args[currentArg++].ToLower();
        }

        public bool Empty
        {
            get
            {
                return currentArg >= args.Length;
            }
        }
    }
}
