﻿using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Gibbed.IO;
using ME3Explorer.UnrealHelper;

namespace ME3Explorer
{
    public partial class TOCeditor : Form
    {
        public struct Inventory
        {
            public uint offset;
            public string name;
            public uint size;
        }

        public byte[] memory;
        public int memsize = 0;
        public List<Inventory> content;
        public string lastsearch = ".pcc";
        private Boolean automated = false; //Mod Manager 3 automator

        public TOCeditor()
        {
            InitializeComponent();

            //FemShep's Mod Manager 3 automator for TOCEditor.
            string[] arguments = Environment.GetCommandLineArgs();
            if (arguments.Length > 2)
            {
                //try
                //{
                string cmdCommand = arguments[1];
                if (cmdCommand.Equals("-toceditorupdate", StringComparison.Ordinal))
                {
                    if (arguments.Length % 2 != 1 || arguments.Length < 5)
                    {
                        MessageBox.Show("Wrong number of arguments for automated TOC update.\nSyntax is: <exe> -toceditorupdate <TOCFILE> <UPDATESIZEFILE> <SIZE> [<UPDATESIZEFILE> <SIZE>]...", "ME3Explorer TOCEditor Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    String tocfile = arguments[2];
                    int numfiles = (arguments.Length - 3) / 2;

                    string[] searchTerms = new String[numfiles];
                    string[] filesizes = new String[numfiles];

                    int argnum = 3; //starts at 3
                    for (int i = 0; i < searchTerms.Length; i++)
                    {
                        searchTerms[i] = arguments[argnum];
                        argnum++;
                        filesizes[i] = arguments[argnum];
                        argnum++;
                    }

                    loadTOCfile(tocfile);
                    for (int i = 0; i < numfiles; i++)
                    {
                        listBox1.SelectedIndex = -1;
                        searchFirstResult(searchTerms[i]);
                        int n = listBox1.SelectedIndex;
                        if (n == -1)
                        {
                            MessageBox.Show("The filepath in this PCConsoleTOC.bin file was not found. Unable to proceed.\nFile path in this TOC not found:\n" + tocfile + "\n" + searchTerms[i], "ME3Explorer TOCEditor Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            Environment.Exit(1);
                            Application.Exit();
                        }
                        editFilesize(n, filesizes[i]);
                    }
                    saveTOC(tocfile);
                    Environment.Exit(0);
                    Application.Exit();
                }
                //}
            } //end automation
        }

        public bool UpdateFile(string name, uint size)
        {
            System.Windows.Forms.DialogResult m = MessageBox.Show("Do you want to update PCConsoleTOC.bin?", "ME3 Explorer", MessageBoxButtons.YesNo);
            if (m == System.Windows.Forms.DialogResult.Yes)
            {
                //load file
                string path = string.Empty;
                OpenFileDialog openFileDialog1 = new OpenFileDialog();
                openFileDialog1.Filter = "PCConsoleTOC.bin|PCConsoleTOC.bin";
                if (openFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    path = openFileDialog1.FileName;
                    loadTOCfile(path);
                }
                else
                    return false;
                if (UpdateFile(name, size, path))
                {
                    MessageBox.Show("Done.");
                    return true;
                }
                else
                    return false;
            }
            return false;
        }

        public bool UpdateFile(string name, uint size, string path)
        {
            loadTOCfile(path);
            int n = -1;
            for (int i = 0; i < content.Count(); i++)
                if (content[i].name.EndsWith(name))
                    n = i;
            if (n == -1) return false;
            //edit entry
            Inventory temp = content[n];
            temp.size = size;
            content[n] = temp;
            uint pos = temp.offset;
            BitConverter.IsLittleEndian = true;
            byte[] buff = BitConverter.GetBytes(size);
            for (int i = 0; i < 4; i++)
                memory[pos + i] = buff[i];
            //write file
            FileStream fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);
            for (int i = 0; i < memsize; i++)
            {
                fileStream.WriteByte(memory[i]);
            }
            fileStream.Close();
            return true;
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string path = string.Empty;

            OpenFileDialog openFileDialog1 = new OpenFileDialog();
            openFileDialog1.Filter = "PCConsoleTOC.bin|PCConsoleTOC.bin";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                path = openFileDialog1.FileName;
                loadTOCfile(path);
            }
        }

        private void loadTOCfile(string path)
        {
            BitConverter.IsLittleEndian = true;
            listBox1.Items.Clear();
            FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            memsize = (int)fileStream.Length;
            memory = new byte[memsize];
            int count;
            int sum = 0;
            while ((count = fileStream.Read(memory, sum, memsize - sum)) > 0) sum += count;
            fileStream.Close();
            if (memsize > 0)
            {
                content = new List<Inventory>();
                Inventory temp = new Inventory();
                MemoryStream myStream = new MemoryStream(memory);
                if (myStream.ReadValueU32() == 0x3AB70C13)
                {
                    myStream.Seek(8, SeekOrigin.Begin);
                    uint jumpSize = myStream.ReadValueU32();
                    myStream.Seek(jumpSize * 8, SeekOrigin.Current);
                    uint blockSize;
                    do
                    {
                        temp = new Inventory();
                        long position = myStream.Position;
                        uint value = myStream.ReadValueU32();
                        blockSize = value & 0xFFFF;
                        uint offset = (uint)myStream.Position;
                        uint fileSize = myStream.ReadValueU32();
                        myStream.Seek(20, SeekOrigin.Current);


                        string filePath = myStream.ReadStringZ();

                        myStream.Seek(position + blockSize, SeekOrigin.Begin);

                        temp.name = filePath;
                        temp.offset = offset;
                        temp.size = fileSize;
                        content.Add(temp);

                    } while (blockSize != 0);
                }
                myStream.Close();
                for (int i = 0; i < content.Count(); i++)
                {
                    listBox1.Items.Add(content[i].offset.ToString("X") + " : " + content[i].name + " (bytes)" + content[i].size.ToString());
                }
            }
        }

        private uint getUInt(int index)
        {
            return BitConverter.ToUInt32(memory, index);
        }

        private void findStringToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string searchfor = Microsoft.VisualBasic.Interaction.InputBox("What string should be searched for?", "ME3 Explorer", lastsearch);
            search(searchfor);
        }

        private void searchFirstResult(String search)
        {
            lastsearch = search;
            int start = listBox1.SelectedIndex;
            if (start == -1) start = 0;
            for (int i = start; i < content.Count(); i++)
            {
                if (content[i].name.Contains(search))
                {
                    listBox1.SelectedIndex = i;
                    break;
                }
            }
        }

        private void search(String search)
        {
            lastsearch = search;
            int start = listBox1.SelectedIndex;
            if (start == -1) start = 0;
            for (int i = start + 1; i < content.Count(); i++)
            {
                if (content[i].name.Contains(search))
                {
                    listBox1.SelectedIndex = i;
                    break;
                }
            }
        }

        private void editFilesizeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1) return;
            string newsize = Microsoft.VisualBasic.Interaction.InputBox("Enter new size in bytes", "ME3 Explorer", content[n].size.ToString());

            editFilesize(n, newsize);
        }

        private void editFilesize(int n, String newsize)
        {
            Inventory temp = content[n];
            try
            {
                temp.size = Convert.ToUInt32(newsize);
            }
            catch (FormatException)
            {
                //exit this method
                return;
            }
            content[n] = temp;
            listBox1.Items.Clear();
            uint pos = temp.offset;
            byte[] buff = BitConverter.GetBytes(temp.size);
            for (int i = 0; i < 4; i++)
            {
                memory[pos + i] = buff[i];
            }
            for (int i = 0; i < content.Count(); i++)
            {
                listBox1.Items.Add(content[i].offset.ToString("X") + " : " + content[i].name + " (bytes)" + content[i].size.ToString());
            }
            listBox1.SelectedIndex = n;
        }

        private void searchAgainToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int start = listBox1.SelectedIndex;
            if (start == -1) start = 0;
            for (int i = start + 1; i < content.Count(); i++)
            {
                if (content[i].name.Contains(lastsearch))
                {
                    listBox1.SelectedIndex = i;
                    break;
                }
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string path;
            SaveFileDialog FileDialog1 = new SaveFileDialog();
            FileDialog1.Filter = "PCConsoleTOC.bin|PCConsoleTOC.bin";
            if (FileDialog1.ShowDialog() == DialogResult.OK)
            {

                path = FileDialog1.FileName;
                saveTOC(path);
            }
        }

        private void saveTOC(String path)
        {
            FileStream fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);
            for (int i = 0; i < memsize; i++)
            {
                fileStream.WriteByte(memory[i]);
            }
            fileStream.Close();
        }

        private void TOCeditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            taskbar.RemoveTool(this);
        }

    }
}
