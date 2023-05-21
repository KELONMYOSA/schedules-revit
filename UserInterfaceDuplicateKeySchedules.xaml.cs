using System;
using System.Windows;
using System.Windows.Forms;

namespace Schedules
{
    public partial class UserInterfaceDuplicateKeySchedules : Window
    {
        public UserInterfaceDuplicateKeySchedules()
        {
            InitializeComponent();
        }

        private void ButtonDuplicate(Object sender, EventArgs e)
        {
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
