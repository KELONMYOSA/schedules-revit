using System;
using System.Windows;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;

namespace Schedules
{
    public partial class UserInterfaceDuplicateKeySchedules : Window
    {
        public UserInterfaceDuplicateKeySchedules(IList<string> schedulesList)
        {
            InitializeComponent();
            SchedulesBox.ItemsSource = schedulesList;            
        }

        public IList<string> selectedSchedules
        {
            get
            {
                return SchedulesBox.SelectedItems.Cast<string>().ToList();
            }
        }

        public string selectedFolder
        {
            get
            {
                return FolderPath.Text;
            }
        }

        private void ButtonDuplicate(Object sender, EventArgs e)
        {
            if (SchedulesBox.SelectedItems.Count == 0)
            {
                System.Windows.Forms.MessageBox.Show("Спецификации не выбраны!", "Внимание!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (FolderPath.Text == "Выберите папку")
            {
                System.Windows.Forms.MessageBox.Show("Папка не выбрана!", "Внимание!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            DialogResult = true;
            Close();
        }

        private void ButtonSelectFolder(Object sender, EventArgs e)
        {
            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FolderPath.Text = folderBrowserDialog.SelectedPath;
            }
        }
    }
}
