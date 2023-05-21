using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

                Dictionary<string, Dictionary<int, string>> mainScheduleColumns = new Dictionary<string, Dictionary<int, string>>();
                Dictionary<string, IList<string>> mainScheduleRowName = new Dictionary<string, IList<string>>();

                foreach (ViewSchedule schedule in selectedSchedules)
                {
                    Dictionary<int, string> columnIdAndName = new Dictionary<int, string>();
                    
                    for (int i = 0; i < schedule.Definition.GetFieldCount(); i++)
                    {
                        ScheduleField field = schedule.Definition.GetField(i);
                        if (field.GetName() != "Ключевое имя")
                        {
                            ElementId paramID = field.ParameterId;
                            try
                            {
                                columnIdAndName.Add(paramID.IntegerValue, doc.GetElement(paramID).Name);
                            }
                            catch 
                            {
                                columnIdAndName.Add(paramID.IntegerValue, null);
                            }
                        }
                    }

                    IList<string> rowNames = new List<string>();

                    FilteredElementCollector rows = new FilteredElementCollector(doc, schedule.Id);
                    foreach (Element row in rows)
                    {
                        rowNames.Add(row.LookupParameter("Ключевое имя").AsString());
                    }

                    mainScheduleColumns.Add(schedule.Name, columnIdAndName);
                    mainScheduleRowName.Add(schedule.Name, rowNames);
                }

                foreach (string path in revitFilesPaths)
                {
                    Document openedDoc = OpenDocumentWithoutWorksets(app, ModelPathUtils.ConvertUserVisiblePathToModelPath(path));

                    IList<ViewSchedule> schedulesInDoc = new FilteredElementCollector(openedDoc)
                        .OfCategory(BuiltInCategory.OST_Schedules)
                        .Cast<ViewSchedule>()
                        .Where(v => v.Definition.IsKeySchedule == true)
                        .Where(v => selectedSchedulesNames.Contains(v.Name))
                        .ToList();

                    foreach (ViewSchedule schedule in schedulesInDoc)
                    {
                        using (Transaction transaction = new Transaction(openedDoc))
                        {
                            int index = 0;
                            while (schedule.Definition.GetFieldCount() != 1)
                            {
                                ScheduleField field = schedule.Definition.GetField(index);
                                if (field.GetName() == "Ключевое имя")
                                {
                                    index++;
                                }
                                else
                                {
                                    transaction.Start("Удаление колонки");
                                    schedule.Definition.RemoveField(index);
                                    transaction.Commit();
                                }
                            }

                            IList<SchedulableField> schedulableFields = schedule.Definition.GetSchedulableFields();
                            IList<int> schedulableFieldsParamId = new List<int>();
                            IList<string> schedulableFieldsParamName = new List<string>();
                            foreach (SchedulableField schedulableField in schedulableFields)
                            {
                                ElementId paramID = schedulableField.ParameterId;
                                schedulableFieldsParamId.Add(paramID.IntegerValue);
                                try
                                {
                                    schedulableFieldsParamName.Add(doc.GetElement(paramID).Name);
                                }
                                catch { }
                            }

                            foreach (int columnId in mainScheduleColumns[schedule.Name].Keys)
                            {
                                string columnName = mainScheduleColumns[schedule.Name][columnId];
                                
                                if (schedulableFieldsParamName.Contains(columnName) || schedulableFieldsParamId.Contains(columnId))
                                {
                                    ElementId elemId = new ElementId(columnId);

                                    transaction.Start("Добавление колонки");
                                    schedule.Definition.AddField(ScheduleFieldType.Instance, elemId);
                                    transaction.Commit();
                                }
                            }
                            
                            FilteredElementCollector rows = new FilteredElementCollector(openedDoc, schedule.Id);
                            IList<string> rowNames = new List<string>();
                            foreach (Element row in rows)
                            {
                                rowNames.Add(row.LookupParameter("Ключевое имя").AsString());
                            }

                            foreach (string rowName in mainScheduleRowName[schedule.Name])
                            {
                                if (!rowNames.Contains(rowName))
                                {
                                    TableData colTableData = schedule.GetTableData();

                                    TableSectionData tsd = colTableData.GetSectionData(SectionType.Body);
                                    int rowIndex = tsd.LastRowNumber + 1;
                                    transaction.Start("Добавление строки");
                                    tsd.InsertRow(rowIndex);
                                    transaction.Commit();
                                    tsd.RefreshData();
                                    string newElemName = (tsd.NumberOfRows - 2).ToString();

                                    Element newElement = new FilteredElementCollector(openedDoc, schedule.Id).Where(e => e.Name.Equals(newElemName)).First();
                                    transaction.Start("Переименование строки");
                                    newElement.LookupParameter("Ключевое имя").Set(rowName);
                                    transaction.Commit();
                                }
                            }
                        }
                    }

                    SyncWithoutRelinquishing(openedDoc);
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
                TaskDialog.Show("Ошибка открытия файла", e.Message);
            }

            return doc;
        }

        private void SyncWithoutRelinquishing(Document doc)
        {
            TransactWithCentralOptions transOpts = new TransactWithCentralOptions();
            SynchLockCallback transCallBack = new SynchLockCallback();
            transOpts.SetLockCallback(transCallBack);

            SynchronizeWithCentralOptions syncOpts = new SynchronizeWithCentralOptions();
            RelinquishOptions relinquishOpts = new RelinquishOptions(false);
            syncOpts.SetRelinquishOptions(relinquishOpts);
            syncOpts.SaveLocalAfter = true;
            syncOpts.Comment = "Обновление ключевых спецификаций";

            try
            {
                doc.SynchronizeWithCentral(transOpts, syncOpts);
            }
            catch
            {
                TaskDialog.Show(Path.GetFileName(doc.PathName), "Ошибка синхронизации, сохранено локально.");
                doc.Save();
            }
        }

        class SynchLockCallback : ICentralLockedCallback
        {
            public bool ShouldWaitForLockAvailability()
            {
                return false;
            }
        }
    }
}
