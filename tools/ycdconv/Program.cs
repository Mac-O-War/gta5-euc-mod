using System;
using System.IO;
using System.Xml;
using CodeWalker.GameFiles;

// usage: ycdconv <in.ycd.xml> <out.ycd>
if (args.Length != 2)
{
    Console.Error.WriteLine("usage: ycdconv <in.ycd.xml> <out.ycd>");
    return 2;
}

var doc = new XmlDocument();
doc.Load(args[0]);
byte[] data = XmlMeta.GetYcdData(doc);
if (data == null)
{
    Console.Error.WriteLine("conversion failed: no clip dictionary in " + args[0]);
    return 1;
}

File.WriteAllBytes(args[1], data);
Console.WriteLine($"wrote {args[1]} ({data.Length} bytes)");
return 0;
