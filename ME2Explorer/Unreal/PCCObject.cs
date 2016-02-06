using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ManagedLZO;
using ME2Explorer;
using ME2Explorer.Helper;
using Gibbed.IO;
using AmaroK86.MassEffect3.ZlibBlock;
using System.Diagnostics;
using KFreonLib.Debugging;
using UsefulThings;

namespace ME2Explorer
{
    public class PCCObject
    {
        public struct NameEntry
        {
            public string name;
            public int Unk;
            public int flags;
        }
        public class ExportEntry
        {
            internal byte[] info; //Properties, not raw data
            public int ClassNameID { get { return BitConverter.ToInt32(info, 0); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 0, sizeof(int)); } }
            public int LinkID { get { return BitConverter.ToInt32(info, 8); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 8, sizeof(int)); } }
            public int PackageNameID;
            public int ObjectNameID { get { return BitConverter.ToInt32(info, 12); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 12, sizeof(int)); } }
            public string ObjectName;
            public int exportid;

            public byte[] exportIdByte
            {
                get 
                {
                    byte[] nameByte = System.BitConverter.GetBytes(exportid);
                    return nameByte;
                }
            }

            public string PackageFullName;
            public string ClassName;
            public byte[] flag
            {
                get
                {
                    byte[] val = new byte[4];
                    Buffer.BlockCopy(info, 28, val, 0, 4);
                    return val;
                }
            }

            public int flagint
            {
                get
                {
                    byte[] val = new byte[4];
                    Buffer.BlockCopy(info, 28, val, 0, 4);
                    return BitConverter.ToInt32(val, 0);
                }
            }

            public string GetFullPath
            {
                get
                {
                    string s = "";
                    if (PackageFullName != "Class" && PackageFullName != "Package")
                        s += PackageFullName + ".";
                    s += ObjectName;
                    return s;
                }
            }

            public PCCObject pccRef;
            public int DataSize { get { return BitConverter.ToInt32(info, 32); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 32, sizeof(int)); } }
            public int DataOffset { get { return BitConverter.ToInt32(info, 36); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 36, sizeof(int)); } }
            public byte[] Data
            {
                get { byte[] val = new byte[DataSize]; pccRef.listsStream.Seek(DataOffset, SeekOrigin.Begin); val = pccRef.listsStream.ReadBytes(DataSize); return val; }
                set
                {
                    if (value.Length > DataSize)
                    {
                        pccRef.listsStream.Seek(0, SeekOrigin.End);
                        DataOffset = (int)pccRef.listsStream.Position;
                        pccRef.listsStream.WriteBytes(value);
                        pccRef.LastExport = this;
                    }
                    else
                    {
                        pccRef.listsStream.Seek(DataOffset, SeekOrigin.Begin);
                        pccRef.listsStream.WriteBytes(value);
                    }
                    if (value.Length != DataSize)
                    {
                        DataSize = value.Length;
                    }
                }
            }

            public byte[] Info
            {

                get { return info; }
                set
                {
                    if (info == null)
                    {
                        info = new byte[value.Length];
                    }

                    if (value.Length != info.Length)
                    {
                        throw new Exception("New info length does not match old one");
                    }

                    for (int i = 0; i < value.Length; i++)
                    {
                        info[i] = value[i];
                    }
                }
            }

            public void setObjectName(string name)
            {
                byte[] nameByte = pccRef.getNameByte(name);
                info[12] = nameByte[0];
                info[13] = nameByte[1];
                info[14] = nameByte[2];
                info[15] = nameByte[3];
                ObjectName = name;
            }

            public void setPackageName(string name)
            {
                if (name == "")
                {
                    PackageFullName = "Base Package";
                    info[8] = 0;
                    info[9] = 0;
                    info[10] = 0;
                    info[11] = 0;
                }
                else
                {
                    PackageFullName = name;
                    byte[] packageByte = pccRef.getExportIdByte(name);
                    info[8] = packageByte[0];
                    info[9] = packageByte[1];
                    info[10] = packageByte[2];
                    info[11] = packageByte[3];
                }
            }

            public bool hasChanged;
        }
        public struct ImportEntry
        {
            public string Package;
            public int link;
            public string Name;
            public byte[] raw;
        }

        public byte[] header;
        private uint magic { get { return BitConverter.ToUInt32(header, 0); } }
        private ushort lowVers { get { return BitConverter.ToUInt16(header, 4); } }
        private ushort highVers { get { return BitConverter.ToUInt16(header, 6); } }
        public int expDataBegOffset { get { return BitConverter.ToInt32(header, 8); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 8, sizeof(int)); } }
        public int nameSize { get { int val = BitConverter.ToInt32(header, 12); if (val < 0) return val * -2; else return val; } }
        public uint flags { get { return BitConverter.ToUInt32(header, 16 + nameSize); } }
        public bool bCompressed
        {
            get { return (flags & 0x02000000) != 0; }
            set
            {
                if (value) // sets the compressed flag if bCompressed set equal to true
                    Buffer.BlockCopy(BitConverter.GetBytes(flags | 0x02000000), 0, header, 16 + nameSize, sizeof(int));
                else // else set to false
                    Buffer.BlockCopy(BitConverter.GetBytes(flags & ~0x02000000), 0, header, 16 + nameSize, sizeof(int));
            }
        }
        public int NameCount { get { return BitConverter.ToInt32(header, nameSize + 20); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 20, sizeof(int)); } }
        public int NameOffset { get { return BitConverter.ToInt32(header, nameSize + 24); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 24, sizeof(int)); } }
        public int ExportCount { get { return BitConverter.ToInt32(header, nameSize + 28); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 28, sizeof(int)); } }
        public int ExportOffset { get { return BitConverter.ToInt32(header, nameSize + 32); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 32, sizeof(int)); } }
        public int ImportCount { get { return BitConverter.ToInt32(header, nameSize + 36); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 36, sizeof(int)); } }
        public int ImportOffset { get { return BitConverter.ToInt32(header, nameSize + 40); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 40, sizeof(int)); } }
        public int Generator { get { return BitConverter.ToInt32(header, nameSize + 64); } }
        public int Compression { get { return BitConverter.ToInt32(header, header.Length - 4); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, header.Length - 4, sizeof(int)); } }
        public int ExportDataEnd
        {
            get
            {
                return LastExport.DataOffset + LastExport.DataSize;
            }
        }

        private ExportEntry LastExport;

        public uint PackageFlags;
        public int NumChunks;
        public MemoryStream listsStream;
        public List<string> Names;
        public List<ImportEntry> Imports;
        public List<ExportEntry> Exports;
        public byte[] Header;
        public int _HeaderOff;
        public MemoryStream m;
        SaltLZOHelper lzo;
        public string fullname;
        public string pccFileName;

        public PCCObject(String path, Boolean littleEndian=true)
        {
            lzo = new SaltLZOHelper();
            fullname = path;
            BitConverter.IsLittleEndian = littleEndian;
            StreamHelpers.setIsLittleEndian(littleEndian);
            DebugOutput.PrintLn("Load file : " + path);
            pccFileName = Path.GetFullPath(path);
            MemoryStream tempStream = new MemoryStream();
            if (!File.Exists(pccFileName))
                throw new FileNotFoundException("PCC file not found");
            using (FileStream fs = new FileStream(pccFileName, FileMode.Open, FileAccess.Read))
            {
                FileInfo tempInfo = new FileInfo(pccFileName);
                tempStream.WriteFromStream(fs, tempInfo.Length);
                if (tempStream.Length != tempInfo.Length)
                {
                    throw new FileLoadException("File not fully read in. Try again later");
                }
            }

            tempStream.Seek(12, SeekOrigin.Begin);
            int tempNameSize = tempStream.ReadValueS32();
            tempStream.Seek(64 + tempNameSize, SeekOrigin.Begin);
            int tempGenerator = tempStream.ReadValueS32();
            tempStream.Seek(36 + tempGenerator * 12, SeekOrigin.Current);
            int tempPos = (int)tempStream.Position;
            NumChunks = tempStream.ReadValueS32();
            tempStream.Seek(0, SeekOrigin.Begin);
            header = tempStream.ReadBytes(tempPos);
            tempStream.Seek(0, SeekOrigin.Begin);

            if (magic != ZBlock.magic && magic.Swap() != ZBlock.magic)
            {
                DebugOutput.PrintLn("Magic number incorrect: " + magic);
                throw new FormatException("This is not a pcc file. The magic number is incorrect.");
            }

            if (bCompressed)
            {
                DebugOutput.PrintLn("File is compressed");
                {
                    listsStream = lzo.DecompressPCC(tempStream, this);

                    //Correct the header
                    bCompressed = false;
                    listsStream.Seek(0, SeekOrigin.Begin);
                    listsStream.WriteBytes(header);

                    //Set numblocks to zero
                    listsStream.WriteValueS32(0);
                    //Write the magic number
                    listsStream.WriteValueS32(1026281201);
                    //Write 8 bytes of 0
                    listsStream.WriteValueS32(0);
                    listsStream.WriteValueS32(0);
                }
            }
            else
            {
                DebugOutput.PrintLn("File already decompressed. Reading decompressed data.");
                listsStream = tempStream;
            }

            ReadNames(listsStream);
            ReadImports(listsStream);
            ReadExports(listsStream);
            LoadExports();
        }

        private void LoadExports()
        {
            DebugOutput.PrintLn("Prefetching Export Name Data...");
            for (int i = 0; i < ExportCount; i++)
            {
                Exports[i].hasChanged = false;
                Exports[i].ObjectName = Names[Exports[i].ObjectNameID];
            }
            for (int i = 0; i < ExportCount; i++)
            {
                Exports[i].PackageFullName = FollowLink(Exports[i].LinkID);
                if (String.IsNullOrEmpty(Exports[i].PackageFullName))
                    Exports[i].PackageFullName = "Base Package";
                else if (Exports[i].PackageFullName[Exports[i].PackageFullName.Length - 1] == '.')
                    Exports[i].PackageFullName = Exports[i].PackageFullName.Remove(Exports[i].PackageFullName.Length - 1);
            }
            for (int i = 0; i < ExportCount; i++)
                Exports[i].ClassName = GetClass(Exports[i].ClassNameID);
        }

        public void SaveToFile(string path)
        {
            listsStream.Seek(ExportDataEnd, SeekOrigin.Begin); // Write names
            NameOffset = (int)listsStream.Position;
            NameCount = Names.Count;
            foreach (String name in Names)
            {
                listsStream.WriteValueS32(name.Length + 1);
                listsStream.WriteString(name);
                listsStream.WriteByte(0);
                listsStream.WriteValueS32(-14);
            }

            ExportCount = Exports.Count;
            ExportOffset = (int)listsStream.Position;
            foreach (ExportEntry exp in Exports)
            {
                listsStream.WriteBytes(exp.Info);
            }

            DebugOutput.PrintLn("Writing pcc to: " + path + "\nRefreshing header to stream...");
            listsStream.Seek(0, SeekOrigin.Begin);
            listsStream.WriteBytes(header);
            DebugOutput.PrintLn("Opening filestream and writing to disk...");
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                listsStream.WriteTo(fs);
            }
        }

        public ExportEntry cloneExport(int exportid, string newPackageName, string newObjectName)
        {
            ExportEntry cloneObj = Exports[exportid-1];

            
            ExportEntry exp = new ExportEntry();
            exp.pccRef = this;
            
            exp.Info = cloneObj.Info;
            
            listsStream.Seek(ExportDataEnd, SeekOrigin.Begin);
            exp.DataSize = cloneObj.DataSize;
            exp.DataOffset = (int)listsStream.Position;
            exp.ClassName = cloneObj.ClassName;

            byte[] data = cloneObj.Data;

            listsStream.Seek(ExportDataEnd, SeekOrigin.Begin);
            listsStream.WriteBytes(data);
            exp.exportid = Exports.Count()+1;

            exp.setPackageName(newPackageName);
            exp.setObjectName(newObjectName);

            LastExport = exp;
            Exports.Add(exp);
            return exp;
        }

        public byte[] getNameByte(string name)
        {
            int i = AddName(name);
            byte[] nameByte = System.BitConverter.GetBytes(i);
            return nameByte;
        }

        public byte[] getExportIdByte(string name)
        {
            foreach (ExportEntry exp in Exports)
            {
                if (exp.PackageFullName == "Base Package" && exp.ObjectName == name)
                {
                    return exp.exportIdByte;
                }
            }
            return null;
        }

        private void ReadNames(MemoryStream fs)
        {
            DebugOutput.PrintLn("Reading Names...");
            fs.Seek(NameOffset, SeekOrigin.Begin);
            Names = new List<string>();
            for (int i = 0; i < NameCount; i++)
            {
                int len = fs.ReadValueS32();
                string s = fs.ReadString((uint)(len - 1));
                fs.Seek(5, SeekOrigin.Current);
                Names.Add(s);
            }
        }

        private void ReadImports(MemoryStream fs)
        {
            DebugOutput.PrintLn("Reading Imports...");
            Imports = new List<ImportEntry>();
            fs.Seek(ImportOffset, SeekOrigin.Begin);
            for (int i = 0; i < ImportCount; i++)
            {
                ImportEntry import = new ImportEntry();
                import.Package = Names[fs.ReadValueS32()];
                fs.Seek(12, SeekOrigin.Current);
                import.link = fs.ReadValueS32();
                import.Name = Names[fs.ReadValueS32()];
                fs.Seek(-24, SeekOrigin.Current);
                import.raw = fs.ReadBytes(28);
                Imports.Add(import);
            }
        }

        private void ReadExports(MemoryStream fs)
        {
            DebugOutput.PrintLn("Reading Exports...");
            fs.Seek(ExportOffset, SeekOrigin.Begin);
            Exports = new List<ExportEntry>();
            
            for (int i = 0; i < ExportCount; i++)
            {
                long start = fs.Position;
                ExportEntry exp = new ExportEntry();
                exp.pccRef = this;
                exp.exportid = i+1;

                fs.Seek(40, SeekOrigin.Current);
                int count = fs.ReadValueS32();
                fs.Seek(4 + count * 12, SeekOrigin.Current);
                count = fs.ReadValueS32();
                fs.Seek(4 + count * 4, SeekOrigin.Current);
                fs.Seek(16, SeekOrigin.Current);
                long end = fs.Position;
                fs.Seek(start, SeekOrigin.Begin);
                exp.info = fs.ReadBytes((int)(end - start));
                Exports.Add(exp);
                fs.Seek(end, SeekOrigin.Begin);

                if (LastExport == null || exp.DataOffset > LastExport.DataOffset)
                    LastExport = exp;
            }
        }

        public bool isName(int Index)
        {
            return (Index >= 0 && Index < NameCount);
        }

        public bool isImport(int Index)
        {
            return (Index >= 0 && Index < ImportCount);
        }

        public bool isExport(int Index)
        {
            return (Index >= 0 && Index < ExportCount);
        }

        public string GetClass(int Index)
        {
            if (Index > 0 && isExport(Index - 1))
                return Exports[Index - 1].ObjectName;
            if (Index < 0 && isImport(Index * -1 - 1))
                return Imports[Index * -1 - 1].Name;
            return "Class";
        }

        public string FollowLink(int Link)
        {
            string s = "";
            if (Link > 0 && isExport(Link - 1))
            {
                s = Exports[Link - 1].ObjectName + ".";
                s = FollowLink(Exports[Link - 1].LinkID) + s;
            }
            if (Link < 0 && isImport(Link * -1 - 1))
            {
                s = Imports[Link * -1 - 1].Name + ".";
                s = FollowLink(Imports[Link * -1 - 1].link) + s;
            }
            return s;
        }

        public string GetName(int Index)
        {
            string s = "";
            if (isName(Index))
                s = Names[Index];
            return s;
        }


        internal string getObjectName(int index)
        {
            if (index > 0 && index < ExportCount)
                return Exports[index - 1].ObjectName;
            if (index * -1 > 0 && index * -1 < ImportCount)
                return Imports[index * -1 - 1].Name;
            return "";
        }

        public int AddName(string newName)
        {
            int nameID = 0;
            //First check if name already exists
            for (int i = 0; i < NameCount; i++)
            {
                if (Names[i] == newName)
                {
                    nameID = i;
                    return nameID;
                }
            }

            Names.Add(newName);
            NameCount++;
            return Names.Count - 1;
        }

        public void DumpPCC(string path)
        {
            listsStream.Seek(0, SeekOrigin.Begin);
            byte[] stream = listsStream.ToArray();
            using (FileStream fs = new FileStream(path, FileMode.Create))
            {
                fs.WriteBytes(stream);
            }
        }

        

        public int FindExp(string name)
        {
            for (int i = 0; i < ExportCount; i++)
            {
                if (String.Compare(Exports[i].ObjectName, name, true) == 0)
                    return i;
            }
            return -1;
        }

        public int FindExp(string name, string className)
        {
            for (int i = 0; i < ExportCount; i++)
            {
                if (String.Compare(Exports[i].ObjectName, name, true) == 0 && Exports[i].ClassName == className)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Checks whether a name exists in the PCC and returns its index
        /// If it doesn't exist returns -1
        /// </summary>
        /// <param name="nameToFind">The name of the string to find</param>
        /// <returns></returns>
        public int findName(string nameToFind)
        {
            for (int i = 0; i < Names.Count; i++)
            {
                if (String.Compare(nameToFind, GetName(i)) == 0)
                    return i;
            }
            return -1;
        }
    }
}
