using System;
using System.Windows;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Autodesk.Revit.DB;

namespace Schedules
{
    public partial class UserInterfaceDuplicateKeySchedules : Window
    {
        private Document doc;
        private IList<string> selectedRevitFiles;


        public UserInterfaceDuplicateKeySchedules(IList<string> schedulesList, Document document)
        {
            InitializeComponent();
            SchedulesBox.ItemsSource = schedulesList;
            doc = document;
        }

        public IList<string> selectedSchedules
        {
            get
            {
                return SchedulesBox.SelectedItems.Cast<string>().ToList();
            }
        }

        public IList<string> selectedFiles
        {
            get
            {
                return selectedRevitFiles;
            }
        }

        public bool syncFiles
        {
            get
            {
                return (bool)SyncCheckbox.IsChecked;
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
            FolderPicker folderBrowserDialog = new FolderPicker();
            folderBrowserDialog.InputPath = Path.GetDirectoryName(doc.PathName);
            if (folderBrowserDialog.ShowDialog() == true)
            {
                string path = folderBrowserDialog.ResultPath;
                IList<string> revitFilesPaths = Directory.EnumerateFiles(path, "*.rvt", SearchOption.TopDirectoryOnly)
                    .Where(f => !f.Equals(doc.PathName)).ToList();
                Dictionary<string, string> filenameToPath = new Dictionary<string, string>();
                foreach (string revitFile in revitFilesPaths)
                {
                    filenameToPath.Add(Path.GetFileName(revitFile), revitFile);
                }

                var filesSelectionUi = new UserInterfaceFilesSelection(filenameToPath.Keys.ToList());
                bool tdRes = (bool)filesSelectionUi.ShowDialog();
                if (tdRes == false)
                {
                    return;
                }
                else
                {
                    IList<string> selectedFiles = filesSelectionUi.selectedFiles;
                    selectedRevitFiles = new List<string>();
                    foreach (string selectedFile in selectedFiles)
                    {
                        selectedRevitFiles.Add(filenameToPath[selectedFile]);
                    }

                    FolderPath.Text = "Выбрано файлов: " + selectedFiles.Count;
                }   
            }
        }
    }
}
