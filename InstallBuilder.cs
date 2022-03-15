using Microsoft.CSharp;
using Serilog;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace AE.Ingredior
{
    public class InstallBuilder
    {

        public static InstallBuilder Builder = new InstallBuilder();

        public List<string> filenames = new List<string>();

        public void BuildEXE()
        {
            var binPath = Path.Combine(FileSystemHelper.Output, "bin");

			Directory.CreateDirectory(binPath);

			var files = Directory.GetFiles(FileSystemHelper.BaseDirectory);

			Log.Debug("Loading all files in BaseDirectory");

            foreach (var file in files)
			{
				FileInfo fileInfo = new FileInfo(file);

				if (fileInfo.Extension != ".pdb")
                {
					AddFile(fileInfo.FullName);

					Log.Debug("File {name} was loaded into archive", fileInfo.Name);
				}
			}
			
			Log.Debug("Compiling EXE...");

			var exePath = Path.Combine(binPath, "AE-Orbis-Installer.exe");

			CompileArchive(exePath, "", SrcBuilder.GetInstallCode());

			Log.Debug("EXE Compiled\n\tPath: {path}", exePath);
			
			foreach (var file in Directory.GetFiles(FileSystemHelper.BaseDirectory))
			{
				FileInfo fileInfo = new FileInfo(file);

				if (fileInfo.Extension == ".gz")
				{
					fileInfo.Delete();
				}
			}

		}

		public void AddFile(string filename)
        {
            // Compress input file using System.IO.Compression
            using (Stream file = File.OpenRead(filename))
            {
                byte[] buffer = new byte[file.Length];

                if (file.Length != file.Read(buffer, 0, buffer.Length))
                    throw new IOException("Unable to read " + filename);

                using (Stream gzFile = File.Create(filename + ".gz"))
                {
                    using (Stream gzip = new GZipStream
                        (gzFile, CompressionMode.Compress))
                    {
                        gzip.Write(buffer, 0, buffer.Length);
                    }
                }
            }
            // Store filename so we can embed it on CompileArchive() call
            filenames.Add(filename + ".gz");
        }

        public void CompileArchive(string archiveFilename, string iconFilename, params string[] sourceFiles)
        {
            CodeDomProvider csc = new CSharpCodeProvider();
            CompilerParameters cp = new CompilerParameters();

            cp.GenerateExecutable = true;
            cp.OutputAssembly = archiveFilename;
            cp.CompilerOptions = "/target:winexe";

            // Add a custom option to run a file after extraction
            //if (run1stItem)
            //{
            //    cp.CompilerOptions += " /define:RUN_1ST_ITEM";
            //}
            if (!string.IsNullOrEmpty(iconFilename))
            {
                cp.CompilerOptions += " /win32icon:" + iconFilename;
            }
            cp.ReferencedAssemblies.Add("System.dll");
            cp.ReferencedAssemblies.Add("System.Windows.Forms.dll");

            // Add compressed files as embedded resources
            cp.EmbeddedResources.AddRange(filenames.ToArray());

            // Compile standalone executable with input files embedded as resource
            CompilerResults cr = csc.CompileAssemblyFromFile(cp, sourceFiles);

            // yell if compilation error
            if (cr.Errors.Count > 0)
            {
                string msg = $"Errors building {cr.PathToAssembly}";

                foreach (CompilerError ce in cr.Errors)
                {
                    msg += Environment.NewLine + ce.ToString();
                }
			
				Log.Fatal(msg);
            }
        }

    }

    public class SrcBuilder
    {
        public static string GetInstallCode()
        {
			string folderName = "AE Orbis";
			string exeName = "AE.Ingredior.exe";
          
			#region Code

            var code = @"
			using System;
			using System.IO;
			using System.IO.Compression;
			using System.Collections.Generic;
			using System.Windows.Forms;
			using System.Reflection;
			using System.Diagnostics;

			namespace SelfExtractor
			{
				static class Program
				{
					// The main entry point for the application.
				
					[STAThread]
					static void Main()
					{
						Application.EnableVisualStyles();
						Application.SetCompatibleTextRenderingDefault(false);
						Application.Run(new Form1());
					}
				}

				public class Form1 : Form
				{

					"
					+
					$"string foldername = \"{folderName}\";"
					+
					@"

					"
					+
					$"string exeName = \"{exeName}\";"
					+
					@"

					public Form1()
					{
						InitializeComponent();
					}

					private void Form1_Load(object sender, EventArgs e)
					{
						Visible = false;
						ShowInTaskbar = false;

						string folderName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), foldername);

						Directory.CreateDirectory(folderName);

						Assembly ass = Assembly.GetExecutingAssembly();
						string[] res = ass.GetManifestResourceNames();

						try
						{
							foreach (string name in res)
							{
								Stream rs = ass.GetManifestResourceStream(name);

								using (Stream gzip = new GZipStream(rs, CompressionMode.Decompress, true))
								{
									string path = Path.Combine(folderName, Path.GetFileNameWithoutExtension(name)); // remove "".gz""

									using (Stream file = File.Create(path))
									{
										for (int b = gzip.ReadByte(); b != -1; b = gzip.ReadByte())
										{
											file.WriteByte((byte)b);
										}
									}
								}
							}
			
							string exePath = Path.Combine(folderName, exeName);
								
							Process.Start(exePath);
						}
						catch (Exception ex)
						{
							MessageBox.Show(this, ex.Message, ass.GetName().Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
						}

						Environment.Exit(0);
					}

					/// <summary>
					/// Required designer variable.
					/// </summary>
					private System.ComponentModel.IContainer components = null;

					protected override void Dispose(bool disposing)
					{
						if (disposing && (components != null))
						{
							components.Dispose();
						}
						base.Dispose(disposing);
					}

					#region Windows Form Designer generated code

					/// <summary>
					/// Required method for Designer support - do not modify
					/// the contents of this method with the code editor.
					/// </summary>
					private void InitializeComponent()
					{
						// 
						// Form1
						// 
						this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
						this.Text = ""Form1"";
						this.Load += new System.EventHandler(this.Form1_Load);
					}

					#endregion
				}
			}
			";

			#endregion

			var srcPath = Path.Combine(FileSystemHelper.Temp, "src");
						
			Directory.CreateDirectory(srcPath);

			var aieCS = Path.Combine(srcPath, "aei.cs");

			Log.Debug("Writing code to: {codepath}", aieCS);

			File.WriteAllText(aieCS, code);

			return aieCS;
        }
    }
}
