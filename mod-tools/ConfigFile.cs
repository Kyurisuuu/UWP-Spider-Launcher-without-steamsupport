// SPDX-License-Identifier: GPL-3.0-only
// Reads and edits Insomniac .config assets in place.
// Format attribution: Tkachov/ALERT (GPL-3.0) dat1lib SerializedSection.
// Values are rewritten at their existing offsets, so the asset never changes size.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ModTools
{
    public sealed class ConfigValue
    {
        public string Path;public int Offset;public byte Type;
        public bool Editable { get { return Type!=ConfigFile.String&&Type!=ConfigFile.Null; } }
        public string Text;
    }

    public sealed class ConfigFile
    {
        public const uint Magic=0x21a56f68,ContentTag=0xe501186f,TypeTag=0x4a128222;
        public const byte UInt8=0x00,UInt16=0x01,UInt32=0x02,Int8=0x04,Int16=0x05,Int32=0x06,Float=0x08,String=0x0a,Object=0x0d,Boolean=0x0f,InstanceId=0x11,Null=0x13;
        const int Dat1=36;
        public readonly byte[] Bytes;
        public readonly List<ConfigValue> Values=new List<ConfigValue>();
        public string TypeName="";
        int sectionStart,sectionEnd;

        public ConfigFile(byte[] bytes)
        {
            Bytes=(byte[])bytes.Clone();
            Require(Bytes.Length>Dat1+16&&U32(0)==Magic&&U32(Dat1)==0x44415431,"Not a .config asset");
            int count=BitConverter.ToUInt16(Bytes,Dat1+12);
            int content=-1,type=-1,contentSize=0,typeSize=0;
            for(int i=0;i<count;i++) {
                int p=Dat1+16+12*i;uint tag=U32(p);int off=Dat1+(int)U32(p+4),size=(int)U32(p+8);
                Require(off>=Dat1&&size>=0&&off+size<=Bytes.Length,"Invalid config section");
                if(tag==ContentTag) { content=off;contentSize=size; }
                if(tag==TypeTag) { type=off;typeSize=size; }
            }
            Require(content>=0,"Config has no content section");
            if(type>=0) {
                var typeValues=new List<ConfigValue>();
                sectionStart=type;sectionEnd=type+typeSize;int pos=type;ReadObject(ref pos,"",typeValues);
                var t=typeValues.FirstOrDefault(v=>v.Path=="Type");if(t!=null)TypeName=t.Text.Trim('"');
            }
            sectionStart=content;sectionEnd=content+contentSize;
            int at=content;ReadObject(ref at,"",Values);
        }

        static void Require(bool ok,string message) { if(!ok)throw new InvalidDataException(message); }
        uint U32(int o) { Require(o>=0&&o+4<=Bytes.Length,"Config read out of range");return BitConverter.ToUInt32(Bytes,o); }
        void Need(int pos,int n) { Require(pos>=sectionStart&&pos+n<=sectionEnd,"Config value outside its section"); }
        void Align(ref int pos) { int r=(pos-sectionStart)%4;if(r!=0)pos+=4-r; }
        string Name(int offset)
        {
            int p=Dat1+offset,e=p;Require(p>=Dat1&&p<Bytes.Length,"Invalid config field name");
            while(e<Bytes.Length&&Bytes[e]!=0)e++;
            return Encoding.ASCII.GetString(Bytes,p,e-p);
        }

        void ReadObject(ref int pos,string prefix,List<ConfigValue> output)
        {
            Need(pos,16);
            Require(U32(pos)==0&&U32(pos+4)==0x03150044,"Unexpected serialized object header");
            int count=(int)U32(pos+8),length=(int)U32(pos+12);pos+=16;
            int start=pos;Need(pos,12*count);
            var kinds=new int[count];var types=new byte[count];var names=new string[count];
            for(int i=0;i<count;i++) { kinds[i]=BitConverter.ToUInt16(Bytes,pos+8*i+4)>>4;types[i]=Bytes[pos+8*i+7]; }
            for(int i=0;i<count;i++)names[i]=Name((int)U32(pos+8*count+4*i));
            pos+=12*count;
            for(int i=0;i<count;i++) {
                string path=prefix.Length==0?names[i]:prefix+"."+names[i];
                if(kinds[i]==1)ReadNode(ref pos,types[i],path,output);
                else for(int k=0;k<kinds[i];k++)ReadNode(ref pos,types[i],path+"["+k+"]",output);
            }
            Align(ref pos);
            Require(pos-start<=length,"Config object is longer than declared");
            pos=start+length;
        }

        static int Size(byte type)
        {
            switch(type) {
                case UInt8:case Int8:case Boolean:case Null:return 1;
                case UInt16:case Int16:return 2;
                case UInt32:case Int32:case Float:return 4;
                case InstanceId:return 8;
            }
            return -1;
        }

        void ReadNode(ref int pos,byte type,string path,List<ConfigValue> output)
        {
            if(type==Object) { ReadObject(ref pos,path,output);return; }
            if(type==String) {
                Need(pos,16);int length=(int)U32(pos);Need(pos+16,length+1);
                output.Add(new ConfigValue{Path=path,Offset=pos,Type=type,Text="\""+Encoding.ASCII.GetString(Bytes,pos+16,length)+"\""});
                pos+=16+length+1;Align(ref pos);return;
            }
            int size=Size(type);Require(size>0,"Unknown config value type 0x"+type.ToString("x2")+" at "+path);
            Need(pos,size);
            var value=new ConfigValue{Path=path,Offset=pos,Type=type};value.Text=Format(value);
            output.Add(value);pos+=size;
        }

        string Format(ConfigValue v)
        {
            switch(v.Type) {
                case UInt8:return Bytes[v.Offset].ToString(CultureInfo.InvariantCulture);
                case Int8:return ((sbyte)Bytes[v.Offset]).ToString(CultureInfo.InvariantCulture);
                case UInt16:return BitConverter.ToUInt16(Bytes,v.Offset).ToString(CultureInfo.InvariantCulture);
                case Int16:return BitConverter.ToInt16(Bytes,v.Offset).ToString(CultureInfo.InvariantCulture);
                case UInt32:return BitConverter.ToUInt32(Bytes,v.Offset).ToString(CultureInfo.InvariantCulture);
                case Int32:return BitConverter.ToInt32(Bytes,v.Offset).ToString(CultureInfo.InvariantCulture);
                case Float:return BitConverter.ToSingle(Bytes,v.Offset).ToString("R",CultureInfo.InvariantCulture);
                case Boolean:return Bytes[v.Offset]!=0?"true":"false";
                case InstanceId:return "0x"+BitConverter.ToUInt64(Bytes,v.Offset).ToString("X16");
                case Null:return "null";
            }
            return "?";
        }

        // "Path = 2", "Path *= 1.5" or "Path += 10". Path may use * as a wildcard.
        // Returns how many values changed.
        public int Apply(string line)
        {
            var m=Regex.Match(line,@"^\s*(?<path>.+?)\s*(?<op>\*=|\+=|=)\s*(?<value>\S+)\s*$");
            Require(m.Success,"Cannot read edit: "+line);
            string pattern="^"+Regex.Escape(m.Groups["path"].Value).Replace("\\*",".*")+"$";
            var matches=Values.Where(v=>Regex.IsMatch(v.Path,pattern,RegexOptions.IgnoreCase)).ToList();
            Require(matches.Count>0,"No config value matches "+m.Groups["path"].Value);
            string op=m.Groups["op"].Value,text=m.Groups["value"].Value;
            int changed=0;
            foreach(var v in matches) {
                if(!v.Editable) { Require(matches.Count>1,v.Path+" is text and cannot be changed");continue; }
                if(v.Type==Boolean) {
                    Require(op=="="&&(text=="true"||text=="false"),v.Path+" is true/false");
                    Bytes[v.Offset]=(byte)(text=="true"?1:0);
                } else {
                    double operand=Double.Parse(text,NumberStyles.Float,CultureInfo.InvariantCulture);
                    double current=Double.Parse(v.Type==InstanceId?"0":v.Text,NumberStyles.Float,CultureInfo.InvariantCulture);
                    Require(v.Type!=InstanceId,v.Path+" is an asset reference and cannot be changed");
                    double next=op=="*="?current*operand:op=="+="?current+operand:operand;
                    Write(v,next);
                }
                v.Text=Format(v);changed++;
            }
            return changed;
        }

        void Write(ConfigValue v,double value)
        {
            byte[] b;
            switch(v.Type) {
                case Float:b=BitConverter.GetBytes((float)value);break;
                case UInt8:Require(value>=0&&value<=255,v.Path+" must be 0-255");b=new[]{(byte)Math.Round(value)};break;
                case Int8:Require(value>=-128&&value<=127,v.Path+" must be -128-127");b=new[]{(byte)(sbyte)Math.Round(value)};break;
                case UInt16:Require(value>=0&&value<=65535,v.Path+" must be 0-65535");b=BitConverter.GetBytes((ushort)Math.Round(value));break;
                case Int16:Require(value>=-32768&&value<=32767,v.Path+" is out of range");b=BitConverter.GetBytes((short)Math.Round(value));break;
                case UInt32:Require(value>=0&&value<=uint.MaxValue,v.Path+" is out of range");b=BitConverter.GetBytes((uint)Math.Round(value));break;
                case Int32:Require(value>=int.MinValue&&value<=int.MaxValue,v.Path+" is out of range");b=BitConverter.GetBytes((int)Math.Round(value));break;
                default:throw new InvalidDataException(v.Path+" cannot be changed");
            }
            Buffer.BlockCopy(b,0,Bytes,v.Offset,b.Length);
        }

        public string Dump()
        {
            var s=new StringBuilder();
            s.AppendLine("# Type: "+TypeName);
            foreach(var v in Values)s.AppendLine(v.Path+" = "+v.Text+(v.Editable?"":"    # read-only"));
            return s.ToString();
        }
    }
}
