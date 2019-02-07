using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.Structure;
using System.Collections;
using System.IO;

namespace TrafficMonitoringSystem
{
    public partial class UserControl1 : UserControl
    {
        VideoCapture cap = null;
        Mat frame = null;
        Hashtable ht = new Hashtable();
        int x = 200, y = 200, rotateCount = 0;
        bool refLineRotate = false, triggerEvent = false;
        public UserControl1()
        {
            InitializeComponent();
            ht.Add("tabPage1", "./Config_Files/lane1_config.txt");
            ht.Add("tabPage2", "./Config_Files/lane2_config.txt");
            ht.Add("tabPage3", "./Config_Files/lane3_config.txt");
            ht.Add("tabPage4", "./Config_Files/lane4_config.txt");
            foreach(DictionaryEntry pair in ht)
            {
                if (!File.Exists(pair.Value.ToString()))
                    using (File.Create(pair.Value.ToString())) { }
            }
        }
        public void loadPrevConfig(string tabName)
        {
           try { 
                string[] config_params = File.ReadAllLines(ht[tabName].ToString());

                if (config_params.Length == 8)
                {
                    textBox1.Text = config_params[0];
                    cap = new VideoCapture(config_params[0]);
                    int skipFrames = int.Parse(config_params[1]);
                    numericUpDown1.Maximum = (int)cap.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameCount) - 1;
                    cap.SetCaptureProperty(Emgu.CV.CvEnum.CapProp.PosFrames, skipFrames);
                    frame = cap.QueryFrame();
                    cap.Retrieve(frame);
                    Image<Rgb, Byte> img = frame.ToImage<Rgb, Byte>();
                    numericUpDown2.Maximum = img.Width;
                    numericUpDown3.Maximum = img.Height;

                    rotateCount = int.Parse(config_params[2]);
                    img = img.Rotate(90 * rotateCount, new Rgb(255, 255, 255), false);
                    frame = img.Mat;

                    textBox2.Text = config_params[5];
                    textBox3.Text = config_params[6];
                    refLineRotate = bool.Parse(config_params[7]);
                    x = int.Parse(config_params[3]);
                    y = int.Parse(config_params[4]);
                    numericUpDown1.Value = int.Parse(config_params[1]);
                    numericUpDown2.Value = x;
                    numericUpDown3.Value = y;

                    textBox2.Enabled = true;
                    textBox3.Enabled = true;
                    numericUpDown1.Enabled = true;
                    numericUpDown2.Enabled = true;
                    numericUpDown3.Enabled = true;
                    button2.Enabled = true;
                    button3.Enabled = true;
                    rotateRefLineButton.Enabled = true;
                    triggerEvent = true;
                    drawRefLines(x, y);
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Encountered an Error in Previous Configuration. Please configure again.");
                triggerEvent = false;
                revertBack();
            }
        }

        private void revertBack()
        {
            textBox1.Text = "";
            textBox2.Text = 100 + "";
            textBox3.Text = 50 + "";
            numericUpDown1.Minimum = numericUpDown1.Maximum = 0;
            numericUpDown1.Value = 0;
            numericUpDown2.Value = 0;
            numericUpDown3.Value = 0;
            rotateCount = 0;
            refLineRotate = false;
            error.Text = "";
            pictureBox1.Image = null;
            textBox2.Enabled = false;
            textBox3.Enabled = false;
            numericUpDown1.Enabled = false;
            numericUpDown2.Enabled = false;
            numericUpDown3.Enabled = false;
            button2.Enabled = false;
            button3.Enabled = false;
            rotateRefLineButton.Enabled = false;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.Filter = "(*.mp4)|*.mp4";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    rotateCount = 0;
                    refLineRotate = false;
                    textBox1.Text = ofd.FileName;
                    cap = new VideoCapture(ofd.FileName);
                    numericUpDown1.Maximum = (int)cap.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameCount) - 1;
                    numericUpDown1.Value = 0;
                    frame = cap.QueryFrame();
                    cap.Retrieve(frame);
                    Image<Rgb, Byte> img = frame.ToImage<Rgb, Byte>();
                    numericUpDown2.Maximum = img.Width;
                    numericUpDown3.Maximum = img.Height;
                    numericUpDown2.Value = x = 200;
                    numericUpDown3.Value = y = 200;
                    textBox2.Enabled = true;
                    textBox2.Text = 100 + "";
                    textBox3.Enabled = true;
                    textBox3.Text = 50 + "";
                    numericUpDown1.Enabled = true;
                    numericUpDown2.Enabled = true;
                    numericUpDown3.Enabled = true;
                    button2.Enabled = true;
                    button3.Enabled = true;
                    rotateRefLineButton.Enabled = true;
                    triggerEvent = true;
                    drawRefLines(x, y);
                }
                else
                    textBox1.Text = "";
            }catch(Exception)
            {
                MessageBox.Show("Encountered an Error while Configuration. Please configure again.");
                triggerEvent = false;
                revertBack();
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            string[] lines = {
                textBox1.Text,
                numericUpDown1.Text,
                rotateCount.ToString(),
                x.ToString(),
                y.ToString(),
                textBox2.Text,
                textBox3.Text,
                refLineRotate.ToString()
            };
            File.WriteAllLines(ht[Parent.Name].ToString(), lines);
            MessageBox.Show("Applied Successfully");
        }

        private void frame_no_changed(object sender, EventArgs e)
        {
            if (triggerEvent)
            {
                int frame_no = (int)numericUpDown1.Value;
                cap.SetCaptureProperty(Emgu.CV.CvEnum.CapProp.PosFrames, frame_no);
                frame = cap.QueryFrame();
                cap.Retrieve(frame);
                Image<Rgb, Byte> img = frame.ToImage<Rgb, Byte>();
                img = img.Rotate(90 * rotateCount, new Rgb(255, 255, 255), false);
                pictureBox1.Image = img.ToBitmap();
                frame = img.Mat;
                drawRefLines(x, y);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
                rotateCount = (rotateCount + 1) % 4;
                Image<Rgb, Byte> emgu_img = frame.ToImage<Rgb, Byte>();
                emgu_img = emgu_img.Rotate(90, new Rgb(255, 255, 255), false);
                frame = emgu_img.Mat;
                pictureBox1.Image = emgu_img.ToBitmap();
                drawRefLines(x, y);
        }
        private void coordinateXChanged(object sender, EventArgs e)
        {
            if (triggerEvent)
            {
                x = (int)numericUpDown2.Value;
                drawRefLines(x, y);
            }
        }

        private void coordinateYChanged(object sender, EventArgs e)
        {
            if (triggerEvent)
            {
                y = (int)numericUpDown3.Value;
                drawRefLines(x, y);
            }
        }
        private void textBox2_Leave(object sender, EventArgs e)
        {
            if (triggerEvent) 
                drawRefLines(x, y);
        }

        private void textBox3_Leave(object sender, EventArgs e)
        {
            if(triggerEvent)
                drawRefLines(x, y);
        }

        private void rotateRefLineButton_Click(object sender, EventArgs e)
        {
            refLineRotate = (refLineRotate) ? false : true;
            drawRefLines(x, y);
        }

        private void drawRefLines(int x, int y)
        {
            error.Visible = false;
            Image<Rgb, Byte> img = frame.ToImage<Rgb, Byte>();
            int len = int.Parse(textBox2.Text);
            int dr = int.Parse(textBox3.Text);
            if (refLineRotate)
            {
                if (x + len < img.Width - 3)
                {
                    if (y + dr < img.Height - 3)
                    {
                        for (int k = 0; k < 3; k++)
                        {
                            for (int i = 0; i < len; i++)
                            {
                                img[y + k, x + i] = new Rgb(255, 0, 0);
                                img[y + dr + k, x + i] = new Rgb(255, 0, 0);
                            }
                        }
                        pictureBox1.Image = img.ToBitmap();
                    }
                    else
                    {
                        error.Text = "Distance is Invalid";
                        error.Visible = true;
                    }
                }
                else
                {
                    error.Text = "Length is Invalid";
                    error.Visible = true;
                }

            }
            else
            {
                if (x + dr < img.Width - 3)
                {
                    if (y + len < img.Height - 3)
                    {
                        for (int k = 0; k < 3; k++)
                        {
                            for (int i = 0; i < len; i++)
                            {
                                img[y + i, x + k] = new Rgb(255, 0, 0);
                                img[y + i, x + dr + k] = new Rgb(255, 0, 0);
                            }
                        }
                        pictureBox1.Image = img.ToBitmap();
                    }
                    else
                    {
                        error.Text = "Length is Invalid";
                        error.Visible = true;
                    }

                }
                else
                {
                    error.Text = "Distance is Invalid";
                    error.Visible = true;
                }
            }
        }
    }
}
