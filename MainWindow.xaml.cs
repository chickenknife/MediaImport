using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.Configuration;
namespace MediaImport
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
        System.IO.DirectoryInfo destDir ;
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            destDir = new DirectoryInfo(ConfigurationManager.AppSettings["destractDirectory"]);
            this.textBox1.Text = destDir.FullName;
        }

        private void Grid_Drop(object sender, DragEventArgs e)
        {
            saveSettings();

            //get drop info
            string[] dirName =(string[])e.Data.GetData(DataFormats.FileDrop, false);

            DirectoryInfo dirInfo = new DirectoryInfo(dirName[0]);

            if (stackPanel1.Children.Cast<driveLabel>().Where(x => x.driveinfo.RootDirectory.Name == dirInfo.Root.Name).ToArray().Length == 0)
            {
                driveLabel drivelbl = new driveLabel(dirInfo, destDir);
                // drivelbl.Name = dirInfo.Root.Name.Replace(":\\","");

                //label design
                drivelbl.RenderSize = new Size(300, 60);
                drivelbl.Margin = new Thickness(2, 4, 2, 4);
                drivelbl.lblDriveLabel.Content = drivelbl.driveinfo.Name;

                //add to stackpanel
                this.stackPanel1.Children.Add(drivelbl);
            }
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog fb = new System.Windows.Forms.FolderBrowserDialog();

            fb.SelectedPath = this.destDir.FullName;

            if (fb.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                this.textBox1.Text = fb.SelectedPath;
            }
        }

        private void saveSettings()
        {
            //Create the object
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

            //make changes
            config.AppSettings.Settings["destractDirectory"].Value = this.textBox1.Text;

            //save to apply changes
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
            destDir = new System.IO.DirectoryInfo(this.textBox1.Text);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            saveSettings();
        }
    }
}
