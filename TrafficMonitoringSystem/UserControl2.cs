using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TrafficMonitoringSystem
{
    public partial class UserControl2 : UserControl
    {
        public UserControl2()
        {
            InitializeComponent();
        }
        public UserControl2(string imagePath, string lane, string speed, string date, string time)
        {
            InitializeComponent();
            pictureBox1.Image = Image.FromFile(imagePath);
            label2.Text = lane;
            label4.Text = speed + " kmph";
            label6.Text = date;
            label8.Text = time;
        }
    }
}
