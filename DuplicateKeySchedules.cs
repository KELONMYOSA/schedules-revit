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

            var ui = new UserInterfaceDuplicateKeySchedules(keyScheduleNames, doc);
            bool tdRes = (bool)ui.ShowDialog();

            if (tdRes == false)
            {
                return Result.Cancelled;
            }
            else
            {
                // Сбор изображений в шаблоне
                IList<ImageType> imagesInDocElem = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_RasterImages)
                    .WhereElementIsElementType()
                    .Cast<ImageType>()
                    .ToList();

                // Изменение спецификаций
                string resultMessage = "Измененные спецификации:\n";
                
                IList<string> revitFilesPaths = ui.selectedFiles;
                IList<string> selectedSchedulesNames = ui.selectedSchedules;
                bool syncFiles = ui.syncFiles;

                IList<ViewSchedule> selectedSchedules = keySchedules.Where(v => selectedSchedulesNames.Contains(v.Name)).ToList();

                Dictionary<string, Dictionary<int, string>> mainScheduleColumns = new Dictionary<string, Dictionary<int, string>>();
                Dictionary<string, IList<IList<Parameter>>> mainScheduleRowParams = new Dictionary<string, IList<IList<Parameter>>>();

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

                    IList<IList<Parameter>> rowParams = new List<IList<Parameter>>();

                    FilteredElementCollector rows = new FilteredElementCollector(doc, schedule.Id);
                    foreach (Element row in rows)
                    {
                        rowParams.Add((from Parameter p in row.Parameters select p).ToList());
                    }

                    mainScheduleColumns.Add(schedule.Name, columnIdAndName);
                    mainScheduleRowParams.Add(schedule.Name, rowParams);
                }

                foreach (string path in revitFilesPaths)
                {
                    Document openedDoc = OpenDocumentWithoutWorksets(app, ModelPathUtils.ConvertUserVisiblePathToModelPath(path));

                    // Замена изображений
                    IEnumerable<ImageType> imagesInCurDoc = new FilteredElementCollector(openedDoc)
                        .OfCategory(BuiltInCategory.OST_RasterImages)
                        .WhereElementIsElementType()
                        .Cast<ImageType>();
                    IList<string> imagesInCurDocName = imagesInCurDoc.Select(e => e.Name).ToList();
                    using (Transaction transaction = new Transaction(openedDoc))
                    {
                        foreach (ImageType image in imagesInDocElem)
                        {
                            System.Drawing.Bitmap imageBitMap = image.GetImage();
                            string imageTmpPath = Path.Combine(Path.GetTempPath(), image.Name + ".jpeg");
                            imageBitMap.Save(imageTmpPath);

                            transaction.Start("Изменение изображения");

                            if (imagesInCurDocName.Contains(image.Name))
                            {
                                // Удаляем изображение
                                ImageType changingImage = imagesInCurDoc.First(e => e.Name == image.Name);
                                openedDoc.Delete(changingImage.Id);
                            }

                            // Добавляем изображение
                            // Revit 2023
                            ImageTypeOptions imageOptions = new ImageTypeOptions(imageTmpPath, false, ImageTypeSource.Import);
                            ImageType newImage = ImageType.Create(openedDoc, imageOptions);
                            newImage.Name = image.Name;

                            // Revit 2020
                            // ImageType newImage = ImageType.Create(openedDoc, imageTmpPath);
                            // newImage.Name = image.Name;

                            transaction.Commit();

                            if (File.Exists(imageTmpPath))
                            {
                                File.Delete(imageTmpPath);
                            }
                        }
                    }

                    // Изменение спецификаций
                    IList<string> changedSchedules = new List<string>();

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
                            //Список столбцов в таблице
                            IList<string> fieldsInSchedule = new List<string>();
                            for (int i = 0; i < schedule.Definition.GetFieldCount(); i++)
                            {
                                ScheduleField field = schedule.Definition.GetField(i);
                                try
                                {
                                    fieldsInSchedule.Add(openedDoc.GetElement(field.ParameterId).Name);
                                }
                                catch { }
                            }
                            //Список столбцов возможных для добавления
                            IList<SchedulableField> schedulableFields = schedule.Definition.GetSchedulableFields();
                            IList<int> schedulableFieldsParamId = new List<int>();
                            IList<string> schedulableFieldsParamName = new List<string>();
                            foreach (SchedulableField schedulableField in schedulableFields)
                            {
                                ElementId paramID = schedulableField.ParameterId;
                                schedulableFieldsParamId.Add(paramID.IntegerValue);
                                try
                                {
                                    schedulableFieldsParamName.Add(openedDoc.GetElement(paramID).Name);
                                }
                                catch { }
                            }
                            //Добавление недостающих столбцов
                            foreach (int columnId in mainScheduleColumns[schedule.Name].Keys)
                            {
                                string columnName = mainScheduleColumns[schedule.Name][columnId];
                                if (schedulableFieldsParamName.Contains(columnName) && !fieldsInSchedule.Contains(columnName))
                                {
                                    ElementId elemId = new FilteredElementCollector(openedDoc)
                                        .OfClass(typeof(ParameterElement))
                                        .Where(p => p.Name.Equals(columnName))
                                        .Select(p => p.Id)
                                        .FirstOrDefault();

                                    if (elemId != null)
                                    {
                                        transaction.Start("Добавление колонки");
                                        schedule.Definition.AddField(ScheduleFieldType.Instance, elemId);
                                        transaction.Commit();
                                        if (!changedSchedules.Contains(schedule.Name))
                                        {
                                            changedSchedules.Add(schedule.Name);
                                        }
                                    }
                                }
                            }
                            //Сбор строк в таблице
                            FilteredElementCollector rows = new FilteredElementCollector(openedDoc, schedule.Id);
                            IList<string> rowNames = new List<string>();
                            foreach (Element row in rows)
                            {
                                rowNames.Add(row.LookupParameter("Ключевое имя").AsString());
                            }
                            int nRows = rowNames.Count;
                            //Добавление недостающих строк
                            foreach (IList<Parameter> rowParams in mainScheduleRowParams[schedule.Name])
                            {
                                string rowNameInMain = "";
                                foreach(Parameter rowParam in rowParams)
                                {
                                    if (rowParam.Definition.Name.Equals("Ключевое имя"))
                                    {
                                        rowNameInMain = rowParam.AsString();
                                    }
                                }
                                if (!rowNames.Contains(rowNameInMain))
                                {
                                    TableData colTableData = schedule.GetTableData();

                                    TableSectionData tsd = colTableData.GetSectionData(SectionType.Body);
                                    int rowIndex = tsd.LastRowNumber + 1;
                                    transaction.Start("Добавление строки");
                                    tsd.InsertRow(rowIndex);
                                    transaction.Commit();
                                    tsd.RefreshData();
                                    nRows++;
                                    string newElemName = (nRows).ToString();
                                    Element newElement = new FilteredElementCollector(openedDoc, schedule.Id).Where(e => e.Name.Equals(newElemName)).First();
                                    transaction.Start("Замена значений в строке");
                                    foreach (Parameter parameter in newElement.Parameters)
                                    {
                                        string paramName = parameter.Definition.Name;
                                        Parameter paramMain = rowParams.Where(p => p.Definition.Name == paramName).FirstOrDefault();
                                        if (paramMain != null)
                                        {
                                            if (!parameter.IsReadOnly && paramMain.HasValue)
                                            {
                                                if (parameter.StorageType == StorageType.ElementId)
                                                {
                                                    if (paramMain.AsElementId().IntegerValue != -1)
                                                    {
                                                        ImageType imageFromTemplate = doc.GetElement(paramMain.AsElementId()) as ImageType;
                                                        ElementId imageIdInCurDocForSelection = new FilteredElementCollector(openedDoc)
                                                                    .OfCategory(BuiltInCategory.OST_RasterImages)
                                                                    .Where(img => img.Name.Equals(imageFromTemplate.Name))
                                                                    .Select(img => img.Id)
                                                                    .FirstOrDefault();
                                                        if (imageIdInCurDocForSelection != null)
                                                        {
                                                            parameter.Set(imageIdInCurDocForSelection);
                                                        }
                                                    } 
                                                }
                                                else if (parameter.StorageType == StorageType.Integer)
                                                {
                                                    parameter.Set(paramMain.AsInteger());
                                                }
                                                else if (parameter.StorageType == StorageType.Double)
                                                {
                                                    parameter.Set(paramMain.AsDouble());
                                                }
                                                else
                                                {
                                                    parameter.Set(paramMain.AsString());
                                                }
                                            }
                                        }
                                    }
                                    transaction.Commit();
                                    if (!changedSchedules.Contains(schedule.Name))
                                    {
                                        changedSchedules.Add(schedule.Name);
                                    }
                                }
                                else
                                {
                                    Element newElement = new FilteredElementCollector(openedDoc, schedule.Id).Where(e => e.Name.Equals(rowNameInMain)).First();
                                    foreach (Parameter parameter in newElement.Parameters)
                                    {
                                        string paramName = parameter.Definition.Name;
                                        Parameter paramMain = rowParams.Where(p => p.Definition.Name == paramName).FirstOrDefault();
                                        if (paramMain != null)
                                        {
                                            if (!parameter.IsReadOnly && paramMain.HasValue)
                                            {
                                                bool wasChanged = false;
                                                transaction.Start("Замена значений в строке");
                                                if (parameter.StorageType == StorageType.ElementId)
                                                {
                                                    if (paramMain.AsElementId().IntegerValue != -1)
                                                    {
                                                        ImageType imageFromTemplate = doc.GetElement(paramMain.AsElementId()) as ImageType;
                                                        if (parameter.AsElementId().IntegerValue != -1)
                                                        {
                                                            ImageType curDocImage = openedDoc.GetElement(parameter.AsElementId()) as ImageType;
                                                            if (curDocImage.Name != imageFromTemplate.Name)
                                                            {
                                                                ElementId imageIdInCurDocForSelection = new FilteredElementCollector(openedDoc)
                                                                    .OfCategory(BuiltInCategory.OST_RasterImages)
                                                                    .Where(img => img.Name.Equals(imageFromTemplate.Name))
                                                                    .Select(img => img.Id)
                                                                    .FirstOrDefault();
                                                                if (imageIdInCurDocForSelection != null)
                                                                {
                                                                    parameter.Set(imageIdInCurDocForSelection);
                                                                }
                                                                else
                                                                {
                                                                    parameter.Set(new ElementId(-1));
                                                                }
                                                                wasChanged = true;
                                                            }
                                                        }
                                                        else
                                                        {
                                                            ElementId imageIdInCurDocForSelection = new FilteredElementCollector(openedDoc)
                                                                    .OfCategory(BuiltInCategory.OST_RasterImages)
                                                                    .Where(img => img.Name.Equals(imageFromTemplate.Name))
                                                                    .Select(img => img.Id)
                                                                    .FirstOrDefault();
                                                            if (imageIdInCurDocForSelection != null)
                                                            {
                                                                parameter.Set(imageIdInCurDocForSelection);
                                                            }
                                                            wasChanged = true;
                                                        }
                                                    } 
                                                    else
                                                    {
                                                        if (parameter.AsElementId().IntegerValue != -1)
                                                        {
                                                            wasChanged = true;
                                                            parameter.Set(new ElementId(-1));
                                                        }
                                                    }
                                                    
                                                }
                                                else if (parameter.StorageType == StorageType.Integer)
                                                {
                                                    if (parameter.AsInteger() != paramMain.AsInteger())
                                                    {
                                                        wasChanged = true;
                                                        parameter.Set(paramMain.AsInteger());
                                                    } 
                                                }
                                                else if (parameter.StorageType == StorageType.Double)
                                                {
                                                    if (parameter.AsDouble() != paramMain.AsDouble())
                                                    {
                                                        wasChanged = true;
                                                        parameter.Set(paramMain.AsDouble());
                                                    } 
                                                }
                                                else
                                                {
                                                    if (parameter.AsString() != paramMain.AsString())
                                                    {
                                                        wasChanged = true;
                                                        parameter.Set(paramMain.AsString());
                                                    }
                                                }
                                                transaction.Commit();
                                                if (!changedSchedules.Contains(schedule.Name) && wasChanged)
                                                {
                                                    changedSchedules.Add(schedule.Name);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    if (changedSchedules.Count > 0)
                    {
                        if (syncFiles)
                        {
                            SyncWithoutRelinquishing(openedDoc);
                        }
                        else
                        {
                            openedDoc.Save();
                        }

                        string outMessage = Path.GetFileName(openedDoc.PathName) + ":\n";
                        foreach (string schedule in changedSchedules)
                        {
                            outMessage += "- " + schedule + "\n";
                        }

                        resultMessage += "\n" + outMessage;
                    }
                    else
                    {
                        MessageBox.Show("В файле " + Path.GetFileName(openedDoc.PathName) + " спецификации для изменения не были найдены.", "Успешно!", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    openedDoc.Close(false);
                }
                
                if (!resultMessage.Equals("Измененные спецификации:\n"))
                {
                    MessageBox.Show(resultMessage, "Успешно!", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
