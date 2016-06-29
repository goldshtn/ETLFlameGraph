using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace ETLFlameGraph
{
    class SVGWriter
    {
        private TextWriter _output;
        private int _width;
        private int _height;
        private int _fontSize = 12;                 // TODO Make customizable
        private string _fontFamily = "Verdana";     // TODO Make customizable
        private int _totalTreeWeight;
        private float _minRectWidth = 1.0f;         // TODO Make customizable
        private float _textPadWidth = 2.0f;
        private float _textPadHeight = 5.0f;
        private float _rectHeight = 20.0f;
        private float _letterWidth;
        private Regex _moduleRegex = new Regex("^(.*?)!(.*)");
        private Random _random = new Random();

        public SVGWriter(TextWriter output, int width, int maxTreeDepth)
        {
            // TODO Because we are trimming nodes smaller than a certain threshold,
            //      it's no good taking the max tree depth here. Perhaps the caller
            //      should give us the min weight % in the tree instead of _minRectWidth
            //      and then we can find the height accordingly. We will also need
            //      the tree itself here.

            _output = output;
            _width = width;
            _height = (int)(maxTreeDepth * _rectHeight);
            _letterWidth = 0.59f * _fontSize; // TODO Make customizable
        }

        public void WriteHeader(string encoding = "utf-8")
        {
            _output.WriteLine($@"<?xml version=""1.0"" encoding=""{encoding}"" standalone=""no""?>
<!DOCTYPE svg PUBLIC ""-//W3C//DTD SVG 1.1//EN"" ""http://www.w3.org/Graphics/SVG/1.1/DTD/svg11.dtd"" >
<svg version=""1.1"" width=""{_width}"" height=""{_height}"" onload=""init(evt)"" viewBox=""0 0 {_width} {_height}"" xmlns=""http://www.w3.org/2000/svg"" xmlns:xlink=""http://www.w3.org/1999/xlink"">
<!-- Flame graph stack visualization. See https://github.com/brendangregg/FlameGraph for latest version, and http://www.brendangregg.com/flamegraphs.html for examples. -->
");

            // TODO Write <rect> for background
            // TODO Write title text, zoom, search box, etc.
        }

        public void WriteEmbeddedJavaScript()
        {
            // TODO
        }

        public void WriteStackTree(StackTree tree)
        {
            _totalTreeWeight = tree.Root.Weight;
            WriteStackTreeNode(tree.Root, 0, 0); // TODO Find the real offsets based on the headers
        }

        private float NameHash(string name)
        {
            float vector = 0, weight = 1, max = 1;
            int mod = 10;
            name = _moduleRegex.Replace(name, match => match.Groups[1].Value[0] + "!" + match.Groups[2].Value);
            foreach (var ch in name)
            {
                int i = (int)ch % mod;
                vector += (i / (mod++ - 1)) * weight;
                max += weight;
                weight *= 0.7f;
                if (mod > 12)
                    break;
            }
            return (1 - vector) / max;
        }

        private string NameColor(string name)
        {
            // TODO Add palette support
            float v1 = NameHash(name), v2, v3;
            v2 = v3 = NameHash(String.Join("", name.Reverse()));
            int red = 200 + (int)(55 * v3);
            int green = 50 + (int)(80 * v1);
            int blue = green;
            return $"rgb({red},{green},{blue})";
        }

        private void WriteStackTreeNode(StackTreeNode node, float startX, float startY)
        {
            float rectWidth = WidthOfRect(node), rectHeight = 20; // TODO
            float textX = startX, textY = startY + (rectHeight - _textPadHeight);
            float rectX = startX, rectY = startY;

            if (rectWidth < _minRectWidth)
                return;

            textX += _textPadWidth;
            float textWidth = rectWidth - 2 * _textPadWidth;

            string displayText = DisplayTextForNode(node);
            string rectColor = NameColor(node.Frame);

            // Figure out what can fit depending on the fonts
            string shownText;
            int numChars = (int)(textWidth / _letterWidth);
            if (numChars <= 3)
            {
                shownText = "";
            }
            else
            {
                if (numChars < displayText.Length)
                    shownText = displayText.Substring(0, numChars - 3) + "...";
                else
                {
                    shownText = displayText;
                }
            }

            _output.WriteLine($"<g onmouseover=\"s('{displayText}')\"> onmouseout=\"c()\" onclick=\"zoom(this)\"");
            _output.WriteLine($"<title>{displayText}</title>");
            _output.WriteLine($"<rect x=\"{rectX}\" y=\"{rectY}\" width=\"{rectWidth}\" height=\"{rectHeight}\" fill=\"{rectColor}\" rx=\"2\" ry=\"2\" />");
            _output.WriteLine($"<text text-anchor=\"\" x=\"{textX}\" y=\"{textY}\" font-size=\"{_fontSize}\" font-family=\"{_fontFamily}\" fill=\"rgb(0,0,0)\">{shownText}</text>");
            _output.WriteLine("</g>");

            var children = node.Children.Values.ToArray();
            float currentChildX = startX;
            for (int i = 0; i < children.Length; ++i)
            {
                WriteStackTreeNode(children[i], currentChildX, startY + rectHeight);
                currentChildX += WidthOfRect(children[i]); // TODO Add some padding probably
            }
        }

        private float WidthOfRect(StackTreeNode node)
        {
            return node.Weight * _width / (1.0f * _totalTreeWeight);
        }

        private string DisplayTextForNode(StackTreeNode node)
        {
            string weightPct = String.Format("{0:N2}", node.Weight * 100.0 / _totalTreeWeight);
            string displayText = $"{node.Frame} ({node.Weight} samples, {weightPct}%)";
            displayText = displayText.Replace("<", "&lt;");
            displayText = displayText.Replace(">", "&gt;");
            displayText = displayText.Replace("&", "&amp;");
            displayText = displayText.Replace("\"", "&quot;");
            return displayText;
        }

        public void WriteFooter()
        {
            _output.WriteLine("</svg>");
        }
    }
}
