using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TrafficMonitoringSystem
{
    public partial class Form1 : Form
    {
        private Thread t1, t2, t3, t4, sch;
        private int running = 0;
        delegate void SetTextCallback(Label label, string text);
        delegate void SetTrafficImageCallback(PictureBox pbox, int clr, int lane);
        delegate void SetVideoImageCallback(ImageBox imageBox, Image<Rgb, Byte> img);
        private static ManualResetEvent mre = new ManualResetEvent(true);
        delegate void SetlabelCallback(Label label);
        Hashtable config_table = new Hashtable();
        Hashtable imgbox_table = new Hashtable();
        Hashtable label_table = new Hashtable();
        Hashtable vc_table = new Hashtable();
        Hashtable threshold_table = new Hashtable();
        bool fatalException = false;
        public Form1()
        {
            InitializeComponent();
            CvInvoke.UseOpenCL = true;
            t1 = new Thread(new ThreadStart(initializeThread));
            t1.Name = "t1";
            t1.IsBackground = true;
            t2 = new Thread(new ThreadStart(initializeThread));
            t2.Name = "t2";
            t2.IsBackground = true;
            t3 = new Thread(new ThreadStart(initializeThread));
            t3.Name = "t3";
            t3.IsBackground = true;
            t4 = new Thread(new ThreadStart(initializeThread));
            t4.Name = "t4";
            t4.IsBackground = true;
            sch = new Thread(new ThreadStart(scheduler));
            sch.IsBackground = true;

            config_table.Add("t1", "./Config_Files/lane1_config.txt");
            config_table.Add("t2", "./Config_Files/lane2_config.txt");
            config_table.Add("t3", "./Config_Files/lane3_config.txt");
            config_table.Add("t4", "./Config_Files/lane4_config.txt");
            imgbox_table.Add("t1", imageBox1);
            imgbox_table.Add("t2", imageBox2);
            imgbox_table.Add("t3", imageBox3);
            imgbox_table.Add("t4", imageBox4);
            label_table.Add("t1", label1);
            label_table.Add("t2", label2);
            label_table.Add("t3", label3);
            label_table.Add("t4", label4);
            vc_table.Add("t1", 0);
            vc_table.Add("t2", 0);
            vc_table.Add("t3", 0);
            vc_table.Add("t4", 0);
        }
        
        void initializeThread()
        {
            string tname = Thread.CurrentThread.Name;
            Mat frame = null;
            Image<Rgb, Byte> img = null;
            VideoCapture cap = null;
            int x = 0, y = 0, rotateCount = 0, refLineLen = 0, skipFrames = 0;
            int validVehicleThreshold = 0, rgbChangeThreshold = 0, contiguousIgnoreZero = 0, contiguousIgnoreOne = 0;
            double videoFrameCount = 0, FrameRate = 0;
            bool refLineRotate = false;
            int[,] rgbValuesInitial = new int[1, 3];

            try
            {
                string[] configParams = File.ReadAllLines(config_table[tname].ToString());
                string[] thresholdParams;
                try
                {
                    thresholdParams = File.ReadAllLines(@"./Thresholds/" + Path.GetFileName(configParams[0]).Split('.')[0] + "_threshold.txt");
                }
                catch (Exception)
                {
                    thresholdParams = File.ReadAllLines(@"./Thresholds/default_threshold.txt");
                }

                validVehicleThreshold = int.Parse(thresholdParams[0]);
                rgbChangeThreshold = int.Parse(thresholdParams[1]);
                contiguousIgnoreZero = int.Parse(thresholdParams[2]);
                contiguousIgnoreOne = int.Parse(thresholdParams[3]);


                cap = new VideoCapture(configParams[0]);
                FrameRate = cap.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.Fps);
                videoFrameCount = cap.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameCount);

                skipFrames = int.Parse(configParams[1]);
                cap.SetCaptureProperty(Emgu.CV.CvEnum.CapProp.PosFrames, skipFrames);

                rotateCount = int.Parse(configParams[2]);
                x = int.Parse(configParams[3]);
                y = int.Parse(configParams[4]);
                refLineLen = int.Parse(configParams[5]);
                int dr = int.Parse(configParams[6]);
                refLineRotate = bool.Parse(configParams[7]);

                frame = cap.QueryFrame();
                cap.Retrieve(frame);
                img = frame.ToImage<Rgb, Byte>();
                img = img.Rotate(90 * rotateCount, new Rgb(255, 255, 255), false);

                rgbValuesInitial = new int[refLineLen + 1, 3];
                if (refLineRotate)
                {
                    for (int i = 0; i < refLineLen; i++)
                    {
                        rgbValuesInitial[i, 0] = (int)img[y, x + i].Red;
                        rgbValuesInitial[i, 1] = (int)img[y, x + i].Green;
                        rgbValuesInitial[i, 2] = (int)img[y, x + i].Blue;
                    }
                }
                else
                {
                    for (int i = 0; i < refLineLen; i++)
                    {
                        rgbValuesInitial[i, 0] = (int)img[y + i, x].Red;
                        rgbValuesInitial[i, 1] = (int)img[y + i, x].Green;
                        rgbValuesInitial[i, 2] = (int)img[y + i, x].Blue;
                    }

                }
                setVideoImage((ImageBox) imgbox_table[tname], drawRefLine(img, x, y, refLineLen, refLineRotate));
            }
            catch (Exception)
            {
                MessageBox.Show("Encountered an Error while loading configuration. Please configure again and Restart the Application.");
                setVideoImage((ImageBox)imgbox_table[tname], null);
                SetText((Label)label_table[tname],"0");
                fatalException = true;
                return;
            }

            int dConnected = 0, curFrameCount = 0, vehicleCount = 0;
            while (true)
            {
                if (fatalException)
                {
                    setVideoImage((ImageBox)imgbox_table[tname], null);
                    SetText((Label)label_table[tname], "0");
                    return;
                }
                if (curFrameCount == videoFrameCount - 1)
                {
                    cap.SetCaptureProperty(Emgu.CV.CvEnum.CapProp.PosFrames, skipFrames);
                    curFrameCount = 0;
                }
                frame = cap.QueryFrame();
                if (!cap.Retrieve(frame))
                    return;
                img = frame.ToImage<Rgb, Byte>();
                img = img.Rotate(90 * rotateCount, new Rgb(255, 255, 255), false);
                vehicleCount = 0;
                dConnected = ProcessFrame(img, x, y, refLineLen, refLineRotate, dConnected, ref vehicleCount ,rgbValuesInitial, validVehicleThreshold, rgbChangeThreshold, contiguousIgnoreZero, contiguousIgnoreOne);

                setVideoImage((ImageBox) imgbox_table[tname], drawRefLine(img, x, y, refLineLen, refLineRotate));

                lock (vc_table)
                {
                    vehicleCount += (int)vc_table[tname];
                    vc_table[tname] = vehicleCount;
                }
                SetText((Label)label_table[tname], vehicleCount.ToString());
                //Thread.Sleep((int)(1000.0 / (3 * FrameRate)));
                curFrameCount++;
                mre.WaitOne();
            }
        }

        private int ProcessFrame(Image<Rgb, Byte> img, int x, int y, int refLineLen, bool refLineRotate, int prevDConnected, ref int vehicleCount, int[,] rgbValuesInitial, int validVehicleThreshold, int rgbChangeThreshold, int contiguousIgnoreZero, int contiguousIgnoreOne)
        {
            int count_of_one = 0, count_of_zero = 0, dConnected = 0;
            int[,] curRGBValues = new int[1, 3];
            int[] changedPixels = new int[refLineLen + 1];
            for (int i = 0; i < refLineLen; i++)
            {
                if (refLineRotate)
                {
                    curRGBValues[0, 0] = (int)img[y, x + i].Red;
                    curRGBValues[0, 1] = (int)img[y, x + i].Green;
                    curRGBValues[0, 2] = (int)img[y, x + i].Blue;
                }else
                {
                    curRGBValues[0, 0] = (int)img[y + i, x].Red;
                    curRGBValues[0, 1] = (int)img[y + i, x].Green;
                    curRGBValues[0, 2] = (int)img[y + i, x].Blue;
                }

                if (Math.Abs(curRGBValues[0, 0] - rgbValuesInitial[i, 0]) > rgbChangeThreshold || Math.Abs(curRGBValues[0, 1] - rgbValuesInitial[i, 1]) > rgbChangeThreshold || Math.Abs(curRGBValues[0, 2] - rgbValuesInitial[i, 2]) > rgbChangeThreshold)
                {
                    changedPixels[i] = 1;
                    count_of_one++;
                }
                else
                {
                    changedPixels[i] = 0;
                    if (count_of_one < contiguousIgnoreOne)
                        for (int j = i - 1; j >= (i - count_of_one); j--)
                        {
                            changedPixels[j] = 0;
                        }
                    count_of_one = 0;
                } 
            }
            count_of_one = 0;
            for (int i = 0; i < refLineLen; i++)
            {
                if (changedPixels[i] == 1)
                {
                    //count_of_one++;
                    if (count_of_zero < contiguousIgnoreZero)
                        for (int j = i - 1; j >= (i - count_of_zero); j--)
                        {
                            changedPixels[j] = 1;
                            //count_of_one++;
                        }
                    count_of_zero = 0;
                }
                else
                {
                    //if (count_of_one > validVehicleThreshold)
                    //    dConnected += 1;
                    //count_of_one = 0;
                    count_of_zero++;
                }
            }
            
            for (int i = 0; i < refLineLen; i++)
            {
                if (changedPixels[i] == 0)
                {
                    if (count_of_one > validVehicleThreshold)
                        dConnected += 1;
                    count_of_one = 0;
                }
                else
                    count_of_one++;
            }
            if (count_of_one > validVehicleThreshold)
                dConnected += 1;  
            vehicleCount += (prevDConnected > dConnected) ? (prevDConnected - dConnected) : 0;
            return dConnected;
        }


   

        void scheduler()
        {
            startTimer(15);
            if (fatalException)
            {
                SetText(timerLabel, "0");
                return;
            }
            setTrafficImage(pictureBox1, 1, 0);
            setTrafficImage(pictureBox2, 1, 1);
            setTrafficImage(pictureBox3, 1, 2);
            setTrafficImage(pictureBox4, 1, 3);

            double greenTimeConstant = 1;

            Hashtable pbox_table = new Hashtable();
            pbox_table.Add(0, pictureBox1);
            pbox_table.Add(1, pictureBox2);
            pbox_table.Add(2, pictureBox3);
            pbox_table.Add(3, pictureBox4);
            Hashtable indexToLabel = new Hashtable();
            indexToLabel.Add(0, label1);
            indexToLabel.Add(1, label2);
            indexToLabel.Add(2, label3);
            indexToLabel.Add(3, label4);

            while (true)
            {
                int[] vc_arr = new int[4];
                int[] scheduleStatus_arr = new int[4];
                do
                {
                    lock (vc_table)
                    {
                        vc_arr[0] = (scheduleStatus_arr[0] == 1) ? 0 : (int)vc_table["t1"];
                        vc_arr[1] = (scheduleStatus_arr[1] == 1) ? 0 : (int)vc_table["t2"];
                        vc_arr[2] = (scheduleStatus_arr[2] == 1) ? 0 : (int)vc_table["t3"];
                        vc_arr[3] = (scheduleStatus_arr[3] == 1) ? 0 : (int)vc_table["t4"];
                    }
                    int currentSchedulingLane = getMaxDensityLane(vc_arr);
                    Setlabelbckg((Label)indexToLabel[currentSchedulingLane]);
                    setTrafficImage((PictureBox)pbox_table[currentSchedulingLane], 3, currentSchedulingLane);
                    int allotedGreenTime = 5 +(int)(greenTimeConstant * vc_arr[currentSchedulingLane]);
                    decreaseVehicleCount(currentSchedulingLane, vc_arr[currentSchedulingLane]);
                    startTimer(allotedGreenTime);
                    setTrafficImage((PictureBox)pbox_table[currentSchedulingLane], 2, currentSchedulingLane);
                    Thread.Sleep(1000);
                    setTrafficImage((PictureBox)pbox_table[currentSchedulingLane], 1, currentSchedulingLane);
                    Setlabelbckg((Label)indexToLabel[currentSchedulingLane]);
                    scheduleStatus_arr[currentSchedulingLane] = 1;
                    mre.WaitOne();
                } while (yetToScheduleLane(scheduleStatus_arr));
            }
        }

        bool yetToScheduleLane(int[] scheduleStatus_arr)
        {
            bool flag = false; 
            for(int i=0;i<4;i++)
                if(scheduleStatus_arr[i] == 0)
                {
                    flag = true;
                    break;
                }
            return flag;
        }

        void decreaseVehicleCount(int lane, int value)
        {
            lock (vc_table)
            {
                if (lane == 0)
                    vc_table["t1"] = (int)vc_table["t1"] - value;
                else if (lane == 1)
                    vc_table["t2"] = (int)vc_table["t2"] - value;
                else if (lane == 2)
                    vc_table["t3"] = (int)vc_table["t3"] - value;
                else if (lane == 3)
                    vc_table["t4"] = (int)vc_table["t4"] - value;
            }
        }

        void startTimer(int sec)
        {
            for (int i = sec; i >=0; i--) {
                if(fatalException)
                    return;
                SetText(timerLabel, i.ToString());
                Thread.Sleep(1000);
                mre.WaitOne();
            }
        }
        int getMaxDensityLane(int[] vc_arr)
        {
            for (int i= 0;i < 4; i++)
            {
                int temp = vc_arr[i];
                int j = (i + 1) % 4;
                bool flag = true;
                while (j != i)
                {
                    if (temp < vc_arr[j])
                    {
                        flag = false;
                        break;
                    }
                    j = (j + 1) % 4; 
                }
                if (flag)
                    return i; 
            }
            return 0;
        }

       

        private void configureToolStripMenuItem_Click(object sender, EventArgs e)
        {
            configure c = new configure();
            c.Show();
            c.loadPrevConfigs();
        }



        private void monitorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Monitor m = new Monitor();
            m.Show();
        }

        private void startToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            pictureBox1.Image = Image.FromFile(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), @"Resources\red.PNG"));
            pictureBox2.Image = Image.FromFile(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), @"Resources\red.PNG"));
            pictureBox2.Image.RotateFlip(RotateFlipType.Rotate90FlipY);
            pictureBox3.Image = Image.FromFile(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), @"Resources\red.PNG"));
            pictureBox4.Image = Image.FromFile(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), @"Resources\red.PNG"));
            pictureBox4.Image.RotateFlip(RotateFlipType.Rotate90FlipX);
            if (running == 0)
            {
                sch.Start();
                t1.Start();
                t2.Start();
                t3.Start();
                t4.Start();
                running = 2;
                startToolStripMenuItem.Text = "Stop";
            }
            else if (running == 1)
            {
                mre.Set();
                running = 2;
                startToolStripMenuItem.Text = "Stop";
            }
            else
            {

                mre.Reset();
                running = 1;
                startToolStripMenuItem.Text = "Start";
            }
        }

        private void speedViolatorsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            viewSpeedViolators vsvForm = new viewSpeedViolators();
            vsvForm.Show();
            vsvForm.populateData();
        }

        private void SetText(Label label, string text)
        {
            if (label.InvokeRequired)
            {
                SetTextCallback d = new SetTextCallback(SetText);
                this.Invoke(d, new object[] { label, text });
            }
            else
            {
                label.Text = text;

            }
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            mre.Reset();
            t1.Abort();
            t2.Abort();
            t3.Abort();
            t4.Abort();
            sch.Abort();
        }

        void setTrafficImage(PictureBox pbox, int clr, int lane)
        {
            if (pbox.InvokeRequired)
            {
                SetTrafficImageCallback d = new SetTrafficImageCallback(setTrafficImage);
                this.Invoke(d, new object[] { pbox, clr, lane });
            }
            else
            {
                string path = "";
                if (clr == 1) path = @"Resources\red.PNG";
                else if (clr == 2) path = @"Resources\yellow.PNG";
                else path = @"Resources\green.PNG";
                pbox.Image = Image.FromFile(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), path));
                if (lane == 1)
                    pbox.Image.RotateFlip(RotateFlipType.Rotate90FlipY);
                else if (lane == 3)
                    pbox.Image.RotateFlip(RotateFlipType.Rotate90FlipX);
            }
        }
        void Setlabelbckg(Label label)
        {
            if (label.InvokeRequired)
            {
                SetlabelCallback d = new SetlabelCallback(Setlabelbckg);
                this.Invoke(d, new object[] { label });
            }
            else
            {
                if (label.BackColor == Color.Red)
                    label.BackColor = Color.Green;
                else
                    label.BackColor = Color.Red;
            }
        }

        private Image<Rgb,Byte> drawRefLine(Image<Rgb,Byte> img, int x, int y, int refLineLen, bool refLineRotate)
        {
            int numOfLines = 1;
            if (refLineRotate)
            {
                for (int k = 0; k < numOfLines; k++)
                    for (int i = 0; i < refLineLen; i++)
                         img[y + k, x + i] = new Rgb(255, 0, 0);
            }
            else
            {
               for (int k = 0; k < numOfLines; k++)
                   for (int i = 0; i < refLineLen; i++)
                        img[y + i, x + k] = new Rgb(255, 0, 0);
            }
            return img;
        }
        void setVideoImage(ImageBox imgBox, Image<Rgb, Byte> img)
        {
            if (imgBox.InvokeRequired)
            {
                SetVideoImageCallback d = new SetVideoImageCallback(setVideoImage);
                this.Invoke(d, new object[] { imgBox, img });
            }
            else
            {
                imgBox.Image = img;
            }
        }
    }
}
