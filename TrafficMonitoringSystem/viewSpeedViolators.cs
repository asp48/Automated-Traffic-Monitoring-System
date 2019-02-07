using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TrafficMonitoringSystem
{
    public partial class viewSpeedViolators : Form
    {
        string[] imagePaths;
        public viewSpeedViolators()
        {
            InitializeComponent();
            
        }
        public void populateData()
        {
            imagePaths = Directory.GetFiles("./SpeedViolators", "*.jpg");
            if(imagePaths.Length == 0)
            {
                MessageBox.Show("No Data Found!");
                this.Close();
                return;
            }
            flp1.SuspendLayout();
            foreach (string imagePath in imagePaths)
            {
                string fileName = Path.GetFileName(imagePath);
                string[] infoParams = fileName.Split('.')[0].Split('_');
                if (infoParams.Length == 5)
                {
                    UserControl2 uc2 = new UserControl2(imagePath, infoParams[1], infoParams[2], infoParams[3], infoParams[4].Replace('-', ':'));
                    flp1.Controls.Add(uc2);
                }
            }
            flp1.ResumeLayout();
        }

    }
}
