// SPDX-License-Identifier: GPL-3.0-only
// Makes replacement-only .stage config mods from the game files already prepared
// on an Xbox in Developer Mode (read over the DevelopmentFiles share) or from a PC install.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using AssetPreparation;

namespace ModTools
{
    static class SpiderModTool
    {
        const string DefaultXbox=@"\\192.168.1.8\DevelopmentFiles\LooseApps\SpiderManRuntime-20261008";

        static int Main(string[] args)
        {
            try {
                if(args.Length==0) { Usage();return 1; }
                switch(args[0].ToLowerInvariant()) {
                    case "index":Index(Opt(args,1,DefaultXbox),Opt(args,2,Cache()));return 0;
                    case "find":Find(Need(args,1,"pattern"),Opt(args,2,Cache()));return 0;
                    case "dump":Dump(Need(args,1,"asset path"),Opt(args,2,DefaultXbox),Opt(args,3,Cache()));return 0;
                    case "build":Build(Need(args,1,"recipe"),Opt(args,2,null));return 0;
                }
                Usage();return 1;
            } catch(Exception e) {
                Console.Error.WriteLine("Error: "+e.Message);return 2;
            }
        }

        static void Usage()
        {
            Console.WriteLine(@"SpiderModTool — make .stage config mods

  index [game]            Name every asset from the game's original index and dag.
  find <text>             List named assets containing <text>.
  dump <asset> [game]     Write an asset's editable values to <asset name>.txt.
  build <recipe> [out]    Apply a recipe's edits and pack a .stage mod.

[game] is the Xbox runtime folder over the network share (default
"+DefaultXbox+@")
or a PC install folder containing asset_archive. Run index first.");
        }

        static string Need(string[] a,int i,string what) { if(a.Length<=i)throw new ArgumentException("Missing "+what+". Run with no arguments for help.");return a[i]; }
        static string Opt(string[] a,int i,string fallback) { return a.Length>i?a[i]:fallback; }
        static string Cache() { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"cache"); }

        // An Xbox runtime keeps the original index beside StartupContent; a PC install keeps it in asset_archive.
        sealed class Game { public string Archives,Index,Dag; }
        static Game Locate(string root)
        {
            var g=new Game();
            if(File.Exists(Path.Combine(root,"original-asset-toc"))) {
                g.Archives=Path.Combine(root,"StartupContent","asset_archive");g.Index=Path.Combine(root,"original-asset-toc");
            } else {
                g.Archives=Directory.Exists(Path.Combine(root,"asset_archive"))?Path.Combine(root,"asset_archive"):root;
                g.Index=Path.Combine(g.Archives,File.Exists(Path.Combine(g.Archives,"toc.BAK"))?"toc.BAK":"toc");
            }
            g.Dag=Path.Combine(g.Archives,"dag");
            if(!File.Exists(g.Index)||!File.Exists(g.Dag))throw new FileNotFoundException("Game index or dag not found under "+root+". Check the path and that the Xbox share is reachable.");
            return g;
        }

        static void Index(string root,string cache)
        {
            var game=Locate(root);Directory.CreateDirectory(cache);
            Console.WriteLine("Reading the game index…");
            byte[] toc=ModOverlay.ReadIndex(game.Index);
            var sec=new Dictionary<uint,int[]>();int count=BitConverter.ToUInt16(toc,12);
            for(int i=0;i<count;i++)sec[BitConverter.ToUInt32(toc,16+12*i)]=new[]{(int)BitConverter.ToUInt32(toc,20+12*i),(int)BitConverter.ToUInt32(toc,24+12*i)};
            int[] ids=sec[0x506d7b8a],spans=sec[0xede8ada9],archives=sec[0x398abff0],offsets=sec[0xdcd720b5],sizes=sec[0x65bcf461];
            int assets=ids[1]/8;var spanOf=new int[assets];
            for(int s=0;s<spans[1]/8;s++) { int st=(int)BitConverter.ToUInt32(toc,spans[0]+8*s),n=(int)BitConverter.ToUInt32(toc,spans[0]+8*s+4);for(int i=st;i<st+n;i++)spanOf[i]=s; }
            var byId=new Dictionary<ulong,List<int>>();
            for(int i=0;i<assets;i++) { ulong id=BitConverter.ToUInt64(toc,ids[0]+8*i);List<int> l;if(!byId.TryGetValue(id,out l))byId[id]=l=new List<int>();l.Add(i); }

            Console.WriteLine("Reading asset names from the dag…");
            byte[] dag=Inflate(game.Dag);
            var names=new Dictionary<ulong,string>();
            for(int i=0;i<dag.Length;) {
                int j=i;while(j<dag.Length&&dag[j]>=0x20&&dag[j]<0x7f)j++;
                if(j<dag.Length&&dag[j]==0&&j-i>=4) {
                    string s=Encoding.ASCII.GetString(dag,i,j-i);ulong h=ModOverlay.PathHash(s);
                    if(byId.ContainsKey(h)&&!names.ContainsKey(h))names[h]=s.Replace('/','\\');
                }
                i=j+1;
            }
            using(var w=new StreamWriter(Path.Combine(cache,"asset-names.tsv"))) {
                w.WriteLine(root);
                foreach(var kv in names.OrderBy(k=>k.Value,StringComparer.OrdinalIgnoreCase))
                    foreach(int i in byId[kv.Key]) {
                        int ar=(int)BitConverter.ToUInt32(toc,offsets[0]+8*i),at=archives[0]+72*ar+8,len=0;while(len<64&&toc[at+len]!=0)len++;
                        w.WriteLine(spanOf[i]+"\t"+Encoding.ASCII.GetString(toc,at,len)+"\t"+BitConverter.ToUInt32(toc,offsets[0]+8*i+4)+"\t"+BitConverter.ToUInt32(toc,sizes[0]+12*i+4)+"\t"+kv.Value);
                    }
            }
            Console.WriteLine("Named "+names.Count+" assets. Index saved to "+cache);
        }

        static byte[] Inflate(string path)
        {
            byte[] all=File.ReadAllBytes(path);int size=BitConverter.ToInt32(all,4);
            if(size<=0||size>256*1024*1024)throw new InvalidDataException("Unexpected dag size");
            using(var z=new DeflateStream(new MemoryStream(all,14,all.Length-14),CompressionMode.Decompress)) {
                var raw=new byte[size];int p=0,n;while(p<size&&(n=z.Read(raw,p,size-p))>0)p+=n;
                if(p!=size)throw new InvalidDataException("The dag is incomplete");
                return raw;
            }
        }

        sealed class Entry { public int Span;public string Archive,Path;public long Offset;public int Size; }
        static List<Entry> Entries(string cache,out string root)
        {
            string file=Path.Combine(cache,"asset-names.tsv");
            if(!File.Exists(file))throw new FileNotFoundException("No asset index yet. Run: SpiderModTool index");
            var lines=File.ReadAllLines(file);root=lines[0];
            return lines.Skip(1).Select(l=>{ var r=l.Split('\t');return new Entry{Span=int.Parse(r[0]),Archive=r[1],Offset=long.Parse(r[2]),Size=int.Parse(r[3]),Path=r[4]}; }).ToList();
        }
        static string Clean(string p) { return p.Replace('/','\\').Trim().TrimStart('\\'); }
        static Entry Lookup(List<Entry> entries,string asset)
        {
            asset=Clean(asset);
            var e=entries.Where(x=>String.Equals(x.Path,asset,StringComparison.OrdinalIgnoreCase)).OrderBy(x=>x.Span).FirstOrDefault();
            if(e==null)throw new ArgumentException("Asset not found: "+asset+". Use find to search.");
            return e;
        }
        static byte[] Read(Game game,Entry e)
        {
            using(var dsar=new DsarReader(Path.Combine(game.Archives,e.Archive)))return dsar.Read(e.Offset,e.Size);
        }

        static void Find(string text,string cache)
        {
            string root;int shown=0;
            foreach(var e in Entries(cache,out root).Where(x=>x.Path.IndexOf(Clean(text),StringComparison.OrdinalIgnoreCase)>=0)) {
                Console.WriteLine(e.Path+(e.Span!=0?"   (span "+e.Span+")":""));
                if(++shown==200) { Console.WriteLine("… more results; narrow the search.");break; }
            }
            if(shown==0)Console.WriteLine("No matches.");
        }

        static void Dump(string asset,string root,string cache)
        {
            string indexed;var entries=Entries(cache,out indexed);
            var e=Lookup(entries,asset);var config=new ConfigFile(Read(Locate(root),e));
            string output=Path.GetFileName(e.Path)+".txt";
            File.WriteAllText(output,"# "+e.Path+"\n"+config.Dump());
            Console.WriteLine("Wrote "+config.Values.Count+" values to "+Path.GetFullPath(output));
        }

        // Recipe:
        //   game = <Xbox runtime share or PC install>   (optional)
        //   [configs\hero\hero_swingconfig.config]
        //   MotionParamsStandard.SpeedParams.TerminalVelocityHorzMax *= 1.5
        static void Build(string recipe,string output)
        {
            string cache=Cache(),indexed;var entries=Entries(cache,out indexed);
            string root=indexed;var edits=new List<KeyValuePair<string,List<string>>>();List<string> current=null;
            int lineNo=0;
            foreach(string raw in File.ReadAllLines(recipe)) {
                lineNo++;string line=raw;int hash=line.IndexOf('#');if(hash>=0)line=line.Substring(0,hash);line=line.Trim();
                if(line.Length==0)continue;
                if(line.StartsWith("[")&&line.EndsWith("]")) { current=new List<string>();edits.Add(new KeyValuePair<string,List<string>>(line.Substring(1,line.Length-2),current));continue; }
                if(current==null&&line.StartsWith("game",StringComparison.OrdinalIgnoreCase)&&line.Contains("=")) { root=line.Substring(line.IndexOf('=')+1).Trim();continue; }
                if(current==null)throw new InvalidDataException("Line "+lineNo+": put edits under an [asset path] heading");
                current.Add(line);
            }
            if(edits.Count==0)throw new InvalidDataException("The recipe has no [asset] sections");
            var game=Locate(root);
            if(output==null)output=Path.ChangeExtension(recipe,".stage");
            string partial=output+".partial";if(File.Exists(partial))File.Delete(partial);
            using(var zip=ZipFile.Open(partial,ZipArchiveMode.Create)) {
                var info=zip.CreateEntry("info.json");
                using(var w=new StreamWriter(info.Open()))w.Write("{\"game\":\"MSMR\",\"author\":\"SpiderModTool\",\"name\":\""+Path.GetFileNameWithoutExtension(output).Replace("\"","")+"\"}");
                foreach(var section in edits) {
                    var e=Lookup(entries,section.Key);var original=Read(game,e);
                    var config=new ConfigFile(original);int changed=0;
                    foreach(string edit in section.Value) { int n=config.Apply(edit);changed+=n;Console.WriteLine("  "+edit+"  ("+n+" value"+(n==1?"":"s")+")"); }
                    var check=new ConfigFile(config.Bytes);
                    if(check.Values.Count!=config.Values.Count||config.Bytes.Length!=original.Length)throw new InvalidDataException("Edited config failed verification: "+e.Path);
                    var entry=zip.CreateEntry(e.Span+"/"+e.Path.Replace('\\','/'),CompressionLevel.Optimal);
                    using(var s=entry.Open())s.Write(config.Bytes,0,config.Bytes.Length);
                    Console.WriteLine(e.Path+": "+changed+" value(s) changed");
                }
            }
            if(File.Exists(output))File.Delete(output);
            File.Move(partial,output);
            Console.WriteLine("Built "+Path.GetFullPath(output));
        }
    }
}
