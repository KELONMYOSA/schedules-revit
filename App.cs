using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;

namespace Schedules
{
    [Transaction(TransactionMode.Manual)]
    public class App : IExternalApplication
    {
        public static string assemblyPath = "";
        public Result OnStartup(UIControlledApplication application)
        {
            assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string tabName = "Спецификации";
            
            //Разделы вкладки
            //Ключевые спецификации
            try { application.CreateRibbonTab(tabName); } catch { }
            string panelName = "Ключевые спецификации";
            RibbonPanel panelKeySchedules = null;
            List<RibbonPanel> tryPanels = application.GetRibbonPanels(tabName).Where(i => i.Name == panelName).ToList();
            if (tryPanels.Count == 0)
            {
                panelKeySchedules = application.CreateRibbonPanel(tabName, panelName);
            }
            else
            {
                panelKeySchedules = tryPanels.First();
            }

            //Кнопки
            PushButton btn1 = panelKeySchedules.AddItem(new PushButtonData(
                            "DuplicateKeySchedules",
                            "Обновить",
                            assemblyPath,
                            "Schedules.DuplicateKeySchedules")
                            ) as PushButton;
            btn1.LargeImage = ConverPngToBitmap(Properties.Resources.DuplicateKeySchedules);
            btn1.ToolTip = "Размещение панелей по оси стены";

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        private BitmapImage ConverPngToBitmap(Image img)
        {
            using (var memory = new MemoryStream())
            {
                img.Save(memory, ImageFormat.Png);
                memory.Position = 0;

                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();

                return bitmapImage;
            }
        }
    }
}
