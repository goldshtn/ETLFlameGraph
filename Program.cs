using Microsoft.Diagnostics.Symbols;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ETLFlameGraph
{
    class Program
    {
        static bool ShouldLoadSymbolsForModule(string modulePath)
        {
            return true;

            //return modulePath.ToLower().Contains(@"c:\program files (x86)\microsoft visual studio") ||
            //    modulePath.EndsWith("clr.dll") ||
            //    modulePath.EndsWith("ntdll.dll") ||
            //    modulePath.Contains("mscorlib");
        }

        static void Main(string[] args)
        {
            string filename;
            string processName;
            if (args.Length < 2)
            {
                filename = @"C:\Temp\final.etl";
                processName = "devenv";
            }
            else
            {
                filename = args[0];
                processName = args[1];
            }

            var output = Console.Error;
            var symOutput = TextWriter.Null;

            var traceLog = TraceLog.OpenOrConvert(filename,
                new TraceLogOptions() { ConversionLog = output });

            var simpleTraceLogProcess = traceLog.Processes.LastProcessWithName(processName);
            if (simpleTraceLogProcess == null)
            {
                Console.Error.WriteLine("Could not find process with name {0}", processName);
                Environment.Exit(1);
            }

            var symbolReader = new SymbolReader(symOutput, SymbolPath.SymbolPathFromEnvironment);
            symbolReader.SecurityCheck = (path => true);

            Stopwatch sw = Stopwatch.StartNew();
            foreach (var module in simpleTraceLogProcess.LoadedModules)
            {
                if (ShouldLoadSymbolsForModule(module.FilePath))
                    traceLog.CodeAddresses.LookupSymbolsForModule(symbolReader, module.ModuleFile);
            }
            Console.Error.WriteLine($"Loaded symbols in {sw.ElapsedMilliseconds} ms");

            bool buildFlameGraphNatively = true;    // TODO Make configurable
            if (buildFlameGraphNatively)
            {
                sw.Restart();
                var stackTree = new StackTree(simpleTraceLogProcess.EventsInProcess.ByEventType<SampledProfileTraceData>());
                Console.Error.WriteLine($"Built stack tree in {sw.ElapsedMilliseconds} ms");
                // stackTree.Dump(Console.Out);

                var writer = new SVGWriter(Console.Out, 1024, stackTree.MaxDepth);
                writer.WriteHeader();
                writer.WriteEmbeddedJavaScript();
                writer.WriteStackTree(stackTree);
                writer.WriteFooter();
            }
            else
            {
                CreateFoldedStacksForFlameGraphScript(simpleTraceLogProcess.EventsInProcess.ByEventType<SampledProfileTraceData>());
            }

            // TODO Make this a PerfView UserCommand -- but don't use the PerfView 'Stacks' API
            // because that would lock us in to only using this tool from within PerfView. Instead,
            // just use CommandEnvironment.OpenETLFile("file.etl").TraceLog to get a TraceLog instance
            // and work from there as above.
        }

        public static string[] StackFramesForProfileEvent(SampledProfileTraceData profileEvent)
        {
            List<string> frames = new List<string>();
            var callStack = profileEvent.CallStack();
            while (callStack != null)
            {
                var method = callStack.CodeAddress.Method;
                var module = callStack.CodeAddress.ModuleFile;

                if (!ShouldIgnoreFrame(method, module))
                {
                    if (method != null)
                        frames.Add(String.Format("{0}!{1}", module.Name, method.FullMethodName));
                    else if (module != null)
                        frames.Add(String.Format("{0}!0x{1:x}", module.Name, callStack.CodeAddress.Address));
                    else
                        frames.Add(String.Format("?!0x{0:x}", callStack.CodeAddress.Address));
                }

                callStack = callStack.Caller;
            }
            frames.Reverse();
            return frames.ToArray();
        }

        private static void CreateFoldedStacksForFlameGraphScript(IEnumerable<SampledProfileTraceData> profileEvents)
        {
            var stacks = new Dictionary<string, int>();
            foreach (var profileEvent in profileEvents)
            {
                string stack = String.Join(";", StackFramesForProfileEvent(profileEvent));
                int count;
                if (!stacks.TryGetValue(stack, out count))
                    stacks.Add(stack, 1);
                else
                    stacks[stack] += 1;
            }

            // Sort unique stacks in descending order of frequency, and print them out
            // in the format that flamegraph.pl expects:
            //      sym1;sym2;sym3;sym4 103
            foreach (var kvp in stacks.OrderByDescending(kvp => kvp.Value))
                Console.WriteLine(kvp.Key + " " + kvp.Value);
        }

        private static bool ShouldIgnoreFrame(TraceMethod method, TraceModuleFile module)
        {
            // TODO Make it optional to ignore kernel frames, and if so -- do it more accurately
            if (module != null)
            {
                return module.FilePath.EndsWith(".sys") ||
                    module.Name == "hal" ||
                    module.Name == "ntoskrnl";
            }
            return false;
        }
    }
}
