using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Application = Autodesk.Revit.ApplicationServices.Application;
using Document = Autodesk.Revit.DB.Document;

namespace Schedules
{
    [Transaction(TransactionMode.Manual)]
    class DuplicateKeySchedules : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Application app = commandData.Application.Application;
            Document doc = commandData.Application.ActiveUIDocument.Document;

            IEnumerable<ViewSchedule> keySchedules = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Schedules)
                .Cast<ViewSchedule>()
                .Where(v => v.Definition.IsKeySchedule == true);

            IList<string> keyScheduleNames = keySchedules.Select(v => v.Name).ToList();

            var ui = new UserInterfaceDuplicateKeySchedules(keyScheduleNames);
            bool tdRes = (bool)ui.ShowDialog();

            if (tdRes == false)
            {
                return Result.Cancelled;
            }
            else
            {
                string selectedFolder = ui.selectedFolder;
                IList<string> selectedSchedulesNames = ui.selectedSchedules;

                IList<ViewSchedule> selectedSchedules = keySchedules.Where(v => selectedSchedulesNames.Contains(v.Name)).ToList();
                IList<string> revitFilesPaths = Directory.EnumerateFiles(selectedFolder, "*.rvt", SearchOption.AllDirectories)
                    .Where(f => !f.Equals(doc.PathName)).ToList();

                foreach (string path in revitFilesPaths)
                {
                    Document openedDoc = OpenDocumentWithoutWorksets(app, ModelPathUtils.ConvertUserVisiblePathToModelPath(path));
                    
                }
                
                return Result.Succeeded;
            } 
        }

        private Document OpenDocumentWithoutWorksets(Application app, ModelPath projectPath)
        {
            Document doc = null;
            try
            {
                OpenOptions openOptions = new OpenOptions();
                WorksetConfiguration openConfig = new WorksetConfiguration(WorksetConfigurationOption.CloseAllWorksets);
                openOptions.SetOpenWorksetsConfiguration(openConfig);
                doc = app.OpenDocumentFile(projectPath, openOptions);
            }
            catch (Exception e)
            {
                TaskDialog.Show("Open File Failed", e.Message);
            }

            return doc;
        }
    }
}
