using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TrafficMonitoringSystem
{
    public partial class configure : Form
    {
        UserControl1 uc1, uc2, uc3, uc4;
        public configure()
        {
            InitializeComponent();
            this.KeyPreview = false;
            uc1 = new UserControl1();
            uc2 = new UserControl1();
            uc3 = new UserControl1();
            uc4 = new UserControl1();
            tabPage1.Controls.Add(uc1);
            tabPage2.Controls.Add(uc2);
            tabPage3.Controls.Add(uc3);
            tabPage4.Controls.Add(uc4);  
        }
        public void loadPrevConfigs()
        {
            uc1.loadPrevConfig("tabPage1");
            uc2.loadPrevConfig("tabPage2");
            uc3.loadPrevConfig("tabPage3");
            uc4.loadPrevConfig("tabPage4");
        }
    }
}
