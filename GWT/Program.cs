using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

using Urho;
using Urho.Desktop;

namespace GridWorld.Test
{
    static class Program
    {
        public static DirectoryInfo AppDir = null;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            string dataPath = string.Empty;

            AppDir = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            if (dataPath == string.Empty || !Directory.Exists(dataPath))
                dataPath = Path.Combine(AppDir.FullName, "assets");

            if (!Directory.Exists(dataPath))
            {
                MessageBox.Show(dataPath, "No assets", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (Directory.Exists(dataPath))
            {
                DesktopUrhoInitializer.AssetsDirectory = dataPath;

                ApplicationOptions options = new ApplicationOptions("Data");
                options.Orientation = ApplicationOptions.OrientationType.Landscape;
                options.ResizableWindow = true;
                options.WindowedMode = true;

                options.Multisampling = 16;

                options.UseDirectX11 = false;

                options.Width = 1280;
                options.Height = 720;

                int exitCode = 0;
                GameApp app = null;
                try
                {
                    app = new GameApp(options);
                    exitCode = app.Run();
                }
                catch (Exception ex)
                {
                    if (app == null || !app.IsExiting)
                        MessageBox.Show(ex.ToString());
                }
            }
        }
    }
}
