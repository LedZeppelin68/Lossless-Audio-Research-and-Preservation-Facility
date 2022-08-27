using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Text.RegularExpressions;

namespace Lossless_Audio_Research_and_Preservation_Facility
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void dataGridView1_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Copy;
        }

        private void dataGridView1_DragDrop(object sender, DragEventArgs e)
        {
            string[] allfiles = ((string[])e.Data.GetData(DataFormats.FileDrop));

            dataGridView1.Rows.Clear();

            int mode = comboBox1.SelectedIndex;

            List<string> selectedfiles = new List<string>();

            switch (mode)
            {
                case 0:
                    for (int i = 0; i < allfiles.Length; i++)
                    {
                        try
                        {
                            selectedfiles.AddRange(Directory.GetFiles(allfiles[i]).Where(x => x.EndsWith(".bin") || x.EndsWith(".cue")).ToArray());
                        }
                        catch { };
                    }
                    break;
                case 1:
                    selectedfiles.AddRange(allfiles.Where(x => new FileInfo(x).Attributes == FileAttributes.Directory).ToArray());
                    break;
            }

            for (int i = 0; i < selectedfiles.Count; i++)
            {
                dataGridView1.Rows.Add(selectedfiles[i]);
            }
        }

        static byte[] header = new byte[] { 0x52, 0x49, 0x46, 0x46, 0xFF, 0xFF, 0xFF, 0xFF, 0x57, 0x41, 0x56, 0x45, 0x66, 0x6D, 0x74, 0x20, 0x10, 0x00, 0x00, 0x00, 0x01, 0x00, 0x02, 0x00, 0x44, 0xAC, 0x00, 0x00, 0x10, 0xB1, 0x02, 0x00, 0x04, 0x00, 0x10, 0x00, 0x64, 0x61, 0x74, 0x61, 0xFF, 0xFF, 0xFF, 0xFF };

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            int maxfiles = int.Parse(numericUpDown1.Value.ToString());

            Parallel.For(0, dataGridView1.Rows.Count, new ParallelOptions { MaxDegreeOfParallelism = maxfiles }, i =>
            {
                Invoke((MethodInvoker)delegate
                {
                    dataGridView1.Rows[i].DefaultCellStyle.BackColor = Color.Yellow;
                });

                string file = dataGridView1.Rows[i].Cells[0].Value.ToString();

                switch (new DirectoryInfo(file).Attributes == FileAttributes.Directory)
                {
                    case false:
                        string ext = file.Substring(file.Length - 3);

                        switch (ext)
                        {
                            case "bin":
                                using (BinaryReader br = new BinaryReader(new FileStream(file, FileMode.Open, FileAccess.Read)))
                                {
                                    using (BinaryWriter bw = new BinaryWriter(new FileStream(file.Replace(".bin", ".wav"), FileMode.Create)))
                                    {
                                        bw.Write(header);
                                        br.BaseStream.CopyTo(bw.BaseStream);

                                        bw.BaseStream.Position = 4;
                                        bw.Write((uint)br.BaseStream.Length + 36);

                                        bw.BaseStream.Position = 40;
                                        bw.Write((uint)br.BaseStream.Length);
                                    }
                                }
                                break;
                            case "cue":
                                string cue_text = File.ReadAllText(file);
                                cue_text = cue_text.Replace("BINARY", "AUDIO");
                                File.WriteAllText(file + ".(new).cue", cue_text);
                                break;
                        }
                        break;
                    case true:
                        string[] bins = Directory.GetFiles(file, "*.bin");
                        
                        string originalcue = Directory.GetFiles(file, "*.cue").First();

                        string[] indexes = File.ReadAllLines(originalcue).Where(x => x.Contains("INDEX 01")).Select(x => Regex.Match(x, "[0-9]{2}:[0-9]{2}:[0-9]{2}").Value ).ToArray();

                        string newfilename = Path.GetFileName(file) + ".wav";
                        string newcuename = Path.GetFileName(file) + ".full.cue";

                        List<string> cuesheet = new List<string>();
                        cuesheet.Add($"FILE \"{newfilename}\" AUDIO");

                        int leadout = 0;

                        using (BinaryWriter bw = new BinaryWriter(new FileStream(Path.Combine(file, newfilename), FileMode.Create)))
                        {
                            bw.Write(header);

                            for (int j = 0; j < bins.Length; j++)
                            {
                                using (BinaryReader br = new BinaryReader(new FileStream(bins[j], FileMode.Open)))
                                {
                                    cuesheet.Add($"  TRACK {(j + 1):D2} AUDIO");
                                    cuesheet.Add($"    INDEX 01 {int2msf(leadout, indexes[j])}");

                                    leadout += (int)br.BaseStream.Length;

                                    br.BaseStream.CopyTo(bw.BaseStream);
                                }
                            }

                            bw.BaseStream.Position = 4;
                            bw.Write((uint)bw.BaseStream.Length - 8);

                            bw.BaseStream.Position = 40;
                            bw.Write((uint)bw.BaseStream.Length - 44);
                        }

                        File.WriteAllLines(Path.Combine(file, newcuename), cuesheet);
                        break;
                }

                Invoke((MethodInvoker)delegate
                {
                    dataGridView1.Rows[i].DefaultCellStyle.BackColor = Color.YellowGreen;
                });
            }
            );

            MessageBox.Show("Complete");
        }

        private object int2msf(int leadout, string index)
        {
            int gap = msf2int(index);

            leadout += gap;

            int f = leadout / 2352 % 75;
            int s = leadout / 2352 / 75 % 60;
            int m = leadout / 2352 / 75 / 60;

            return $"{m:D2}:{s:D2}:{f:D2}";
        }

        private int msf2int(string index)
        {
            string[] msf = index.Split(':');

            return (int.Parse(msf[2]) * 2352) + (int.Parse(msf[1]) * 75 * 2352) + (int.Parse(msf[0]) * 60 * 75 * 2352);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            backgroundWorker1.RunWorkerAsync();
        }
    }
}
