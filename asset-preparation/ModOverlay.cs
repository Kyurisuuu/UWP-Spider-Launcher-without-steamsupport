// SPDX-License-Identifier: GPL-3.0-only
// Applies replacement-only .stage mods to a prepared game index.
// Format attribution: Tkachov/Overstrike (GPL-3.0) StageInstaller and TOC_I20,
// and Tkachov/ALERT. No game payload bytes are embedded; mods are supplied by the user.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace AssetPreparation
{
    public sealed class ModAsset { public string Mod,Path;public byte Span;public ulong Id;public byte[] Data; }
    public sealed class ModOverlayResult { public int Mods,Assets;public long Bytes; }
    public static class ModOverlay
    {
        // Same naming rules as the preparation's generated archives.
        public const string ArchiveName="local_mods.dsar";
        const uint ArchivesTag=0x398abff0,OffsetsTag=0xdcd720b5,SizesTag=0x65bcf461,IdsTag=0x506d7b8a,SpansTag=0xede8ada9;
        const int MaxAsset=64*1024*1024;

        // A mod is a .stage file (zip) or an unpacked stage folder. Names starting
        // with '_' or '.' are treated as disabled. Later names win on conflicts.
        public static List<string> FindMods(string root)
        {
            var result=new List<string>();
            if(!Directory.Exists(root))return result;
            result.AddRange(Directory.GetFiles(root,"*.stage"));
            result.AddRange(Directory.GetDirectories(root));
            return result.Where(p=>{ string n=System.IO.Path.GetFileName(p);return n.Length>0&&n[0]!='_'&&n[0]!='.'; })
                .OrderBy(p=>System.IO.Path.GetFileName(p),StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static List<ModAsset> ReadMods(IList<string> mods)
        {
            var byKey=new Dictionary<string,ModAsset>();var order=new List<string>();
            foreach(string mod in mods) {
                string name=System.IO.Path.GetFileName(mod);
                if(Directory.Exists(mod)) {
                    string root=System.IO.Path.GetFullPath(mod).TrimEnd('\\','/')+System.IO.Path.DirectorySeparatorChar;
                    foreach(string file in Directory.GetFiles(mod,"*",SearchOption.AllDirectories)) {
                        Bytes.Require((File.GetAttributes(file)&FileAttributes.ReparsePoint)==0,"Mods cannot use linked files: "+name);
                        string full=System.IO.Path.GetFullPath(file);
                        Bytes.Require(full.StartsWith(root,StringComparison.OrdinalIgnoreCase),"Mod path escaped its folder: "+name);
                        long length=new FileInfo(full).Length;
                        Add(byKey,order,name,full.Substring(root.Length),length,()=>File.ReadAllBytes(full));
                    }
                } else {
                    using(var zip=new ZipArchive(File.OpenRead(mod),ZipArchiveMode.Read,false))
                        foreach(var entry in zip.Entries) {
                            if(entry.FullName.EndsWith("/")||entry.FullName.EndsWith("\\"))continue;
                            var current=entry;
                            Add(byKey,order,name,entry.FullName,entry.Length,()=>{ using(var s=current.Open())return Bytes.Read(s,checked((int)current.Length)); });
                        }
                }
            }
            return order.Select(k=>byKey[k]).ToList();
        }

        static void Add(Dictionary<string,ModAsset> byKey,List<string> order,string mod,string path,long length,Func<byte[]> read)
        {
            byte span;ulong id;
            if(!IsAssetPath(path,out span,out id))return; // info.json, previews and readme files
            Bytes.Require(length>0&&length<=MaxAsset,"Mod asset is empty or larger than 64 MiB: "+mod+" "+path);
            string key=span+"/"+id.ToString("X16");
            if(!byKey.ContainsKey(key))order.Add(key);
            byKey[key]=new ModAsset{Mod=mod,Path=path.Replace('\\','/'),Span=span,Id=id,Data=read()};
            Bytes.Require(byKey[key].Data.Length==length,"Mod asset size changed while reading: "+mod+" "+path);
        }

        // "<span>/<asset path or 16-digit hex id>", as written by the stage tools.
        public static bool IsAssetPath(string path,out byte span,out ulong id)
        {
            span=0;id=0;
            if(String.IsNullOrEmpty(path))return false;
            int slash=path.IndexOfAny(new[]{'/','\\'});
            if(slash<=0)return false;
            int value;
            if(!Int32.TryParse(path.Substring(0,slash),NumberStyles.None,CultureInfo.InvariantCulture,out value)||value<0||value>255)return false;
            string asset=path.Substring(slash+1);
            if(asset.Length==0)return false;
            span=(byte)value;
            if(asset.Length==16&&asset.All(Uri.IsHexDigit))id=UInt64.Parse(asset,NumberStyles.HexNumber,CultureInfo.InvariantCulture);
            else id=PathHash(asset);
            return true;
        }

        static readonly ulong[] Table=BuildTable();
        static ulong[] BuildTable()
        {
            var t=new ulong[256];
            for(uint i=0;i<256;i++) { ulong c=i;for(int k=0;k<8;k++)c=(c&1)!=0?(c>>1)^0xC96C5795D7870F42UL:c>>1;t[i]=c; }
            return t;
        }
        internal static ulong TableEntry(int i) { return Table[i]; }
        public static string NormalizePath(string path)
        {
            var b=new StringBuilder();bool slash=false;
            foreach(char c in path.ToLowerInvariant().Replace('\\','/')) {
                if(c=='/') { if(slash)continue;slash=true; } else slash=false;
                b.Append(c);
            }
            string r=b.ToString();
            if(r.StartsWith("/"))r=r.Substring(1);
            if(r.EndsWith("/"))r=r.Substring(0,r.Length-1);
            return r;
        }
        public static ulong PathHash(string path)
        {
            ulong v=0xC96C5795D7870F42UL;
            foreach(byte b in Encoding.UTF8.GetBytes(NormalizePath(path)))v=(v>>8)^Table[(byte)(v^b)];
            return v>>2|0x8000000000000000UL;
        }

        public static byte[] ReadIndex(string path)
        {
            using(var f=File.OpenRead(path)) {
                byte[] h=Bytes.Read(f,10);int size=checked((int)Bytes.U32(h,4));
                Bytes.Require(Bytes.U32(h,0)==0x77af12af&&size>16&&size<=MaxAsset&&h[8]==0x78&&((h[8]<<8)+h[9])%31==0&&(h[9]&32)==0,"Invalid compressed index header");
                byte[] raw;
                using(var inflate=new DeflateStream(f,CompressionMode.Decompress)) { raw=Bytes.Read(inflate,size);Bytes.Require(inflate.ReadByte()==-1,"Unexpected index expansion"); }
                Bytes.Require(Bytes.U32(raw,0)==0x44415431&&Bytes.U32(raw,8)==raw.Length,"Invalid DAT1 index");
                return raw;
            }
        }

        // Writes beside the target and replaces it only after the file is complete.
        public static void WriteIndex(byte[] raw,string path)
        {
            string partial=path+".partial";
            if(File.Exists(partial))File.Delete(partial);
            using(var f=new FileStream(partial,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
                var writer=new BinaryWriter(f,Encoding.UTF8,true);writer.Write(0x77af12afu);writer.Write(raw.Length);writer.Write((byte)0x78);writer.Write((byte)0x9c);
                using(var deflate=new DeflateStream(f,CompressionLevel.Optimal,true))deflate.Write(raw,0,raw.Length);
                uint a=1,b=0;foreach(byte value in raw) { a=(a+value)%65521;b=(b+a)%65521; }uint adler=(b<<16)|a;
                writer.Write((byte)(adler>>24));writer.Write((byte)(adler>>16));writer.Write((byte)(adler>>8));writer.Write((byte)adler);writer.Flush();f.Flush(true);
            }
            Commit(partial,path);
        }

        static void CopyFile(string from,string to)
        {
            string partial=to+".partial";
            if(File.Exists(partial))File.Delete(partial);
            File.Copy(from,partial);
            Commit(partial,to);
        }

        // Swap in place so the game index is never missing, even if power is lost.
        static void Commit(string partial,string path)
        {
            if(File.Exists(path))File.Replace(partial,path,null);
            else File.Move(partial,path);
        }

        sealed class Layout { public int Archives,ArchivesTable,Offsets,Sizes,Ids,Spans,SpanCount,AssetCount,ArchiveCount; }
        static Layout Parse(byte[] raw)
        {
            int count=Bytes.U16(raw,12);Bytes.Require(count>0&&count<32,"Invalid section count");
            var sections=new Dictionary<uint,int[]>();
            for(int i=0;i<count;i++) {
                int p=16+12*i;uint tag=Bytes.U32(raw,p);int offset=checked((int)Bytes.U32(raw,p+4)),size=checked((int)Bytes.U32(raw,p+8));
                Bytes.Require(offset>=16+12*count&&size>=0&&offset<=raw.Length&&size<=raw.Length-offset&&!sections.ContainsKey(tag),"Invalid index section");
                sections.Add(tag,new[]{offset,size,p});
            }
            foreach(uint tag in new[]{ArchivesTag,OffsetsTag,SizesTag,IdsTag,SpansTag})Bytes.Require(sections.ContainsKey(tag),"Unsupported index: missing section "+tag.ToString("x8"));
            int[] archives=sections[ArchivesTag],offsets=sections[OffsetsTag],sizes=sections[SizesTag],ids=sections[IdsTag],spans=sections[SpansTag];
            var l=new Layout{Archives=archives[0],ArchivesTable=archives[2],Offsets=offsets[0],Sizes=sizes[0],Ids=ids[0],Spans=spans[0],SpanCount=spans[1]/8,AssetCount=ids[1]/8,ArchiveCount=archives[1]/72};
            // Appending an archive needs the archive table to be the last section, as in AssetIndex.
            Bytes.Require(archives[0]+archives[1]==raw.Length&&archives[1]%72==0&&ids[1]%8==0&&spans[1]%8==0&&offsets[1]==8*l.AssetCount&&sizes[1]==12*l.AssetCount,"Unsupported index layout");
            long total=0;
            for(int s=0;s<l.SpanCount;s++) {
                long start=Bytes.U32(raw,l.Spans+8*s),n=Bytes.U32(raw,l.Spans+8*s+4);
                Bytes.Require(start==total,"Invalid index span table");total+=n;
            }
            Bytes.Require(total==l.AssetCount,"Index spans do not cover every asset");
            return l;
        }

        static string ArchiveFilename(byte[] raw,Layout l,int index)
        {
            int p=l.Archives+72*index+8,n=0;while(n<64&&raw[p+n]!=0)n++;
            return Encoding.ASCII.GetString(raw,p,n);
        }

        static int Find(byte[] raw,Layout l,byte span,ulong id)
        {
            if(span>=l.SpanCount)return -1;
            int lo=(int)Bytes.U32(raw,l.Spans+8*span),hi=lo+(int)Bytes.U32(raw,l.Spans+8*span+4)-1;
            while(lo<=hi) {
                int mid=lo+(hi-lo)/2;ulong value=BitConverter.ToUInt64(raw,l.Ids+8*mid);
                if(value==id)return mid;
                if(value<id)lo=mid+1;else hi=mid-1;
            }
            return -1;
        }

        // The backup is the clean prepared index. Every apply starts from it, so
        // applying again replaces the previous mod set instead of stacking on it.
        public static ModOverlayResult Apply(string tocPath,string backupPath,IList<string> mods,Action<string> progress)
        {
            tocPath=System.IO.Path.GetFullPath(tocPath);backupPath=System.IO.Path.GetFullPath(backupPath);
            string folder=System.IO.Path.GetDirectoryName(tocPath),archivePath=System.IO.Path.Combine(folder,ArchiveName);
            Bytes.Require(File.Exists(tocPath),"The prepared game index is missing. Prepare the game first.");
            Bytes.Require(mods.Count>0,"No mods found. Put .stage files or stage folders in the mods folder.");
            if(progress!=null)progress("Reading "+mods.Count+" mod(s)…");
            var assets=ReadMods(mods);
            Bytes.Require(assets.Count>0,"The mods contain no game assets. Stage mods keep assets in numbered folders, such as 0/configs/…");
            if(!File.Exists(backupPath))CopyFile(tocPath,backupPath);
            byte[] raw=ReadIndex(backupPath);var l=Parse(raw);
            var targets=new int[assets.Count];
            for(int i=0;i<assets.Count;i++) {
                var a=assets[i];int index=Find(raw,l,a.Span,a.Id);
                Bytes.Require(index>=0,"\""+a.Mod+"\" adds a new asset ("+a.Path+"). Only mods that replace existing assets are supported.");
                int archive=(int)Bytes.U32(raw,l.Offsets+8*index);
                Bytes.Require(archive<l.ArchiveCount,"Index routes an asset to a missing archive");
                Bytes.Require(!ArchiveFilename(raw,l,archive).StartsWith("local_",StringComparison.Ordinal),"\""+a.Mod+"\" replaces an asset the Xbox preparation already converted ("+a.Path+"). Texture and model mods are not supported.");
                targets[i]=index;
            }
            long total=assets.Sum(a=>(long)a.Data.Length);
            Bytes.Require(total<uint.MaxValue,"Mods are larger than 4 GiB");
            Bytes.Require(DiskSpace.Available(folder)>total+64L*1024*1024,"Not enough free space for the mods");
            // Return to the clean index before touching the old mod archive, so
            // the live index never names an archive that is being replaced.
            if(File.Exists(archivePath)||File.Exists(archivePath+".partial")) {
                WriteIndex(raw,tocPath);
                if(File.Exists(archivePath))File.Delete(archivePath);
                if(File.Exists(archivePath+".partial"))File.Delete(archivePath+".partial");
            }
            if(progress!=null)progress("Packing "+assets.Count+" modded asset(s)…");
            var offsets=new long[assets.Count];
            using(var writer=new DsarWriter(archivePath+".partial",total)) {
                for(int i=0;i<assets.Count;i++) { offsets[i]=writer.Position;writer.Write(assets[i].Data); }
                writer.Complete();
            }
            File.Move(archivePath+".partial",archivePath);
            if(progress!=null)progress("Updating the game index…");
            int added=l.ArchiveCount;
            byte[] result=new byte[checked(raw.Length+72)];Buffer.BlockCopy(raw,0,result,0,raw.Length);
            Buffer.BlockCopy(raw,l.Archives,result,raw.Length,8);
            byte[] name=Encoding.ASCII.GetBytes(ArchiveName);Buffer.BlockCopy(name,0,result,raw.Length+8,name.Length);
            Bytes.Put32(result,8,result.Length);Bytes.Put32(result,l.ArchivesTable+8,72*(l.ArchiveCount+1));
            for(int i=0;i<assets.Count;i++) {
                int index=targets[i];
                Bytes.Put32(result,l.Offsets+8*index,added);
                Buffer.BlockCopy(BitConverter.GetBytes(checked((uint)offsets[i])),0,result,l.Offsets+8*index+4,4);
                Bytes.Put32(result,l.Sizes+12*index+4,assets[i].Data.Length);
            }
            WriteIndex(result,tocPath);
            return new ModOverlayResult{Mods=mods.Count,Assets=assets.Count,Bytes=total};
        }

        public static bool Remove(string tocPath,string backupPath)
        {
            tocPath=System.IO.Path.GetFullPath(tocPath);backupPath=System.IO.Path.GetFullPath(backupPath);
            string archivePath=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(tocPath),ArchiveName);
            if(!File.Exists(backupPath))return false;
            ReadIndex(backupPath); // refuse to restore a damaged backup
            CopyFile(backupPath,tocPath);
            if(File.Exists(archivePath))File.Delete(archivePath);
            if(File.Exists(archivePath+".partial"))File.Delete(archivePath+".partial");
            File.Delete(backupPath);
            return true;
        }
    }
}
