using System;
using System.Windows;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;

namespace Schedules
{
    public partial class UserInterfaceFilesSelection : Window
    {
        public UserInterfaceFilesSelection(IList<string> filesList)
        {
            InitializeComponent();
            FilesBox.ItemsSource = filesList;            
        }

        public IList<string> selectedFiles
        {
            get
            {
                return FilesBox.SelectedItems.Cast<string>().ToList();
            }
        }

        private void ButtonOk(Object sender, EventArgs e)
        {
            if (FilesBox.SelectedItems.Count == 0)
            {
                System.Windows.Forms.MessageBox.Show("Файлы не выбраны!", "Внимание!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            DialogResult = true;
            Close();
        }
    }
}
