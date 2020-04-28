#region copyright
/*
GridWorld a learning experiement in voxel technology
Copyright (c) 2020 Jeffery Myersn

Permission is hereby granted, free of charge, to any person
obtaining a copy of this software and associated documentation
files (the "Software"), to deal in the Software without
restriction, including without limitation the rights to use,
copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the
Software is furnished to do so, subject to the following
conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
OTHER DEALINGS IN THE SOFTWARE.
*/
#endregion
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
