using Emgu.CV;
using Emgu.CV.Structure;
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
    public partial class Monitor : Form
    {
        private int running = 0;
        private Thread t;
        private static ManualResetEvent mre = new ManualResetEvent(true);
        delegate void SetTextCallback(Label label, string text);
        Hashtable config_table = new Hashtable();
        Hashtable threshold_table = new Hashtable();
        bool lane_change_flag = false;
        string current_lane = "Lane 1";

        //toBeEdited
        Rectangle roi_new = new Rectangle(0,0,0,0);
        Rectangle roi_prev = new Rectangle(0, 0, 0, 0);

        public Monitor()
        {
            InitializeComponent();
            string[] lanes = { "Lane 1", "Lane 2", "Lane 3", "Lane 4" };
            comboBox1.Items.AddRange(lanes);
            t = new Thread(new ThreadStart(startmonitor));
            t.IsBackground=true;
            config_table.Add("Lane 1", "./Config_Files/lane1_config.txt");
            config_table.Add("Lane 2", "./Config_Files/lane2_config.txt");
            config_table.Add("Lane 3", "./Config_Files/lane3_config.txt");
            config_table.Add("Lane 4", "./Config_Files/lane4_config.txt");

        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (running==0)
            {
                t.Start();
                running = 2;
                button1.Text = "Stop";
            }else if (running == 1)
            {
                running = 2;
                button1.Text = "Stop";
                mre.Set();
                if (lane_change_flag)
                {
                    t.Abort();
                    t = new Thread(new ThreadStart(startmonitor));
                    t.Start();
                    lane_change_flag = false;
                }
            }
            else
            {
                mre.Reset();
                running = 1;
                button1.Text = "Start";
            }
        }
        private void startmonitor()
        {
            Mat frame = null;
            Image<Rgb, Byte> img = null;
            VideoCapture cap = null;
            int x = 0, y = 0, rotateCount = 0, refLineLen = 0, skipFrames = 0, dr = 0;
            int validVehicleThreshold = 0, rgbChangeThreshold = 0, contiguousIgnoreZero = 0, contiguousIgnoreOne = 0;
            double videoFrameCount = 0, FrameRate = 0;
            bool refLineRotate = false;
            Random sp = new Random();
            int[,] ref1_rgbValuesInitial = new int[1, 3];
            int[,] ref2_rgbValuesInitial = new int[1, 3];
            string saveDirPath = @".\SpeedViolators\";

            try
            {
                string[] configParams = File.ReadAllLines(config_table[current_lane].ToString());
                string[] thresholdParams;
                try
                {
                    thresholdParams = File.ReadAllLines(@"./Thresholds/" + Path.GetFileName(configParams[0]).Split('.')[0] + "_threshold.txt");
                }
                catch(Exception)
                {
                    thresholdParams = File.ReadAllLines(@"./Thresholds/default_threshold.txt");
                    MessageBox.Show("Default Threshold");
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
                dr = int.Parse(configParams[6]);
                refLineRotate = bool.Parse(configParams[7]);

                frame = cap.QueryFrame();
                cap.Retrieve(frame);
                img = frame.ToImage<Rgb, Byte>();
                img = img.Rotate(90 * rotateCount, new Rgb(255, 255, 255), false);

                ref1_rgbValuesInitial = new int[refLineLen + 1, 3];
                ref2_rgbValuesInitial = new int[refLineLen + 1, 3];

                if (refLineRotate)
                {
                    for (int i = 0; i < refLineLen; i++)
                    {
                        ref1_rgbValuesInitial[i, 0] = (int)img[y, x + i].Red;
                        ref1_rgbValuesInitial[i, 1] = (int)img[y, x + i].Green;
                        ref1_rgbValuesInitial[i, 2] = (int)img[y, x + i].Blue;
                        ref2_rgbValuesInitial[i, 0] = (int)img[y + dr, x + i].Red;
                        ref2_rgbValuesInitial[i, 1] = (int)img[y + dr, x + i].Green;
                        ref2_rgbValuesInitial[i, 2] = (int)img[y + dr, x + i].Blue;
                    }
                }
                else
                {
                    for (int i = 0; i < refLineLen; i++)
                    {
                        ref1_rgbValuesInitial[i, 0] = (int)img[y + i, x].Red;
                        ref1_rgbValuesInitial[i, 1] = (int)img[y + i, x].Green;
                        ref1_rgbValuesInitial[i, 2] = (int)img[y + i, x].Blue;
                        ref2_rgbValuesInitial[i, 0] = (int)img[y + i, x + dr].Red;
                        ref2_rgbValuesInitial[i, 1] = (int)img[y + i, x + dr].Green;
                        ref2_rgbValuesInitial[i, 2] = (int)img[y + i, x + dr].Blue;
                    }

                }
                imageBox1.Image = drawRefLine(img, x, y, refLineLen, dr, refLineRotate);
            }catch(Exception)
            {
                MessageBox.Show("Encountered an Error while loading configuration for " + current_lane + "Please Configure again and Try.");
                lane_change_flag = true;
                return;
            }


            int[] frameCountAtRef1 = new int[1000];
            int ref1_dConnected = 0, ref2_dConnected = 0, curFrameCount = 0, ref1_vehicleCount = 0, ref2_vehicleCount = 0;
            int lastSpeedComputedVC = 0, lastUpdatedFCAtRef1 = 0;
            double speed = 0, refRegionLen = 7, speedLimit = 40, speedViolators = 0; 
            while (true)
            {
                if ((curFrameCount + 1) % videoFrameCount == 0)
                {
                    cap.SetCaptureProperty(Emgu.CV.CvEnum.CapProp.PosFrames, skipFrames);
                }
                frame = cap.QueryFrame();
                if (!cap.Retrieve(frame))
                    return;
                img = frame.ToImage<Rgb, Byte>();
                img = img.Rotate(90 * rotateCount, new Rgb(255, 255, 255), false);
                curFrameCount++;
                ref1_dConnected = ProcessFrame(img, x, y, refLineLen, 0, refLineRotate, ref1_dConnected, ref ref1_vehicleCount, ref1_rgbValuesInitial, validVehicleThreshold, rgbChangeThreshold, contiguousIgnoreZero, contiguousIgnoreOne,false);
                ref2_dConnected = ProcessFrame(img, x, y, refLineLen, dr, refLineRotate, ref2_dConnected, ref ref2_vehicleCount, ref2_rgbValuesInitial, validVehicleThreshold, rgbChangeThreshold, contiguousIgnoreZero, contiguousIgnoreOne,true);
                imageBox1.Image = drawRefLine(img, x, y, refLineLen, dr, refLineRotate);

                for (int i = lastUpdatedFCAtRef1 + 1; i <= ref1_vehicleCount; i++)
                {
                    frameCountAtRef1[i] = curFrameCount;
                    lastUpdatedFCAtRef1++; 
                }
                
                SetText(label5, ref1_vehicleCount.ToString());

                for (int i=lastSpeedComputedVC +1; i <= ref2_vehicleCount; i++)
                {
                    int timeTaken = (curFrameCount - frameCountAtRef1[i]);
                    timeTaken = timeTaken > 0 ? timeTaken : 1 ;

                    speed = (refRegionLen * 3.6 * FrameRate) / timeTaken ;
					
                    SetText(label6, string.Format("{0:n2}",speed) + " km/hr ");
                    if (speed > speedLimit)
                    {
                        speedViolators++;
                        img.ROI = roi_prev;
                        img = img.Copy();
                        string savePath = saveDirPath + "sv_" + current_lane + "_" + ((int)speed).ToString() + "_"; 
                        savePath += DateTime.Now.ToString("dd-MM-yyyy") + "_" + DateTime.Now.ToString("HH-mm-ss") + ".jpg";
                        img.Save(savePath);
                        SetText(label7, speedViolators.ToString());
                    }
                    lastSpeedComputedVC++;
                }
                Thread.Sleep((int)(1000.0 /( 3 * FrameRate)));
                mre.WaitOne();
            }

        }
        private int ProcessFrame(Image<Rgb, Byte> img, int x, int y, int refLineLen, int dr, bool refLineRotate, int prevDConnected, ref int vehicleCount, int[,] rgbValuesInitial, int validVehicleThreshold, int rgbChangeThreshold, int contiguousIgnoreZero, int contiguousIgnoreOne, bool computeROI)
        {
            int count_of_one = 0, count_of_zero = 0, dConnected = 0;
            int[,] curRGBValues = new int[1, 3];
            int[] changedPixels = new int[refLineLen + 1];
            for (int i = 0; i < refLineLen; i++)
            {
                if (refLineRotate)
                {
                    curRGBValues[0, 0] = (int)img[y + dr, x + i].Red;
                    curRGBValues[0, 1] = (int)img[y + dr, x + i].Green;
                    curRGBValues[0, 2] = (int)img[y + dr, x + i].Blue;
                }
                else
                {
                    curRGBValues[0, 0] = (int)img[y + i, x + dr].Red;
                    curRGBValues[0, 1] = (int)img[y + i, x + dr].Green;
                    curRGBValues[0, 2] = (int)img[y + i, x + dr].Blue;
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
                    if (count_of_zero < contiguousIgnoreZero)
                        for (int j = i - 1; j >= (i - count_of_zero); j--)
                        {
                            changedPixels[j] = 1;
                        }
                    count_of_zero = 0;
                }
                else
                {
                    count_of_zero++;
                }
            }

            for (int i = 0; i < refLineLen; i++)
            {
                if (changedPixels[i] == 0)
                {
                    if (count_of_one > validVehicleThreshold)
                    {
                        dConnected += 1;
                        if (computeROI)
                        {
                            roi_prev = roi_new;
                            int corX, corY;
                            if (refLineRotate)
                            {
                                corX = x + i - count_of_one - 100;
                                corY = y + dr - 100;
                                roi_new = new Rectangle(corX, corY, count_of_one + 200, 300);
                            }
                            else
                            {
                                corX = x + dr - 100;
                                corY = y + i - count_of_one - 100;
                                roi_new = new Rectangle(corX, corY, 300, count_of_one + 200);
                            }
                        }
                    }
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

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            lane_change_flag = true;
            current_lane = comboBox1.Text;
        }
        private Image<Rgb, Byte> drawRefLine(Image<Rgb, Byte> img, int x, int y, int refLineLen, int dr, bool refLineRotate)
        {
            int numOfLines = 1;
            if (refLineRotate)
            {
                for (int k = 0; k < numOfLines; k++)
                    for (int i = 0; i < refLineLen; i++)
                    {
                        img[y + k, x + i] = new Rgb(255, 0, 0);
                        img[y + dr + k, x + i] = new Rgb(255, 0, 0);
                    }
            }
            else
            {
                for (int k = 0; k < numOfLines; k++)
                    for (int i = 0; i < refLineLen; i++)
                    {
                        img[y + i, x + k] = new Rgb(255, 0, 0);
                        img[y + i, x + dr + k] = new Rgb(255, 0, 0);
                    }
            }
            return img;
        }

        private void Monitor_FormClosed(object sender, FormClosedEventArgs e)
        {
            mre.Reset();
            t.Abort();
        }
    }
}
