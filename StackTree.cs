using System.Collections.Generic;
using System.Linq;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using System.IO;
using System;

namespace ETLFlameGraph
{
    class StackTreeNode
    {
        public IDictionary<string, StackTreeNode> Children { get; private set; } = new Dictionary<string, StackTreeNode>();
        public int Weight { get; private set; }
        public string Frame { get; private set; }

        public StackTreeNode(string frame)
        {
            Frame = frame;
        }

        private StackTreeNode GetOrCreateChild(string frame)
        {
            StackTreeNode child;
            if (!Children.TryGetValue(frame, out child))
            {
                child = new StackTreeNode(frame);
                Children.Add(frame, child);
            }
            return child;
        }

        public void AddStack(string[] frames, int index = 0)
        {
            ++Weight;

            if (index >= frames.Length)
                return;

            var child = GetOrCreateChild(frames[index]);
            child.AddStack(frames, index + 1);
        }

        public void Dump(TextWriter writer, int indent = 0, int minWeight = 0)
        {
            if (Weight < minWeight)
                return;

            writer.Write(new string(' ', indent));
            writer.WriteLine($"{Weight} {Frame}");
            foreach (var child in Children.Values)
                child.Dump(writer, indent + 2, minWeight);
        }
    }

    class StackTree
    {
        public StackTreeNode Root { get; private set; }
        public int MaxDepth { get; private set; }

        public StackTree(IEnumerable<SampledProfileTraceData> profileEvents)
        {
            Root = new StackTreeNode("ROOT");
            foreach (var profileEvent in profileEvents)
            {
                var frames = Program.StackFramesForProfileEvent(profileEvent);
                MaxDepth = Math.Max(MaxDepth, frames.Length);
                Root.AddStack(frames);
            }
        }

        public void Dump(TextWriter writer)
        {
            Root.Dump(writer, minWeight: Root.Weight / 20);
        }
    }
}