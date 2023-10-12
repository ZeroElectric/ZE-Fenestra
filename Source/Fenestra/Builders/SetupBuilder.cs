using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Microsoft.CSharp;
using Serilog;

namespace ZeroElectric.Fenestra
{
    public class SetupBuilder
    {

        public List<string> filenames = new List<string>();
        public static SetupBuilder Builder = new SetupBuilder();

        public void BuildEXE(FEPackManifest header, bool buildAsPortable = false)
        {
            string aepkgOutputPath = Path.Combine(FS.Output, $"{header.output.pkgName}-{header.pkgVer}.aepkg");

            Log.Information("Preparing files for setup.exe compilation...");

            foreach (string file in Directory.GetFiles(FS.BaseDirectory))
            {
                FileInfo fileInfo = new FileInfo(file);

                string ext = fileInfo.Extension;
                if (ext == ".exe" || ext == ".dll" || ext == ".aepkg" || ext == ".aepak")
                {
                    string tempFile;
                    if (ext == ".exe")
                    {
                        tempFile = Path.Combine(FS.TempSetup, $"{header.output.installerName}.exe");
                        Log.Debug("An EXE was found: {name}, and was copied into: {des}", fileInfo.Name, tempFile);
                    }
                    else
                    {
                        tempFile = Path.Combine(FS.TempSetup, fileInfo.Name);
                        Log.Debug("File, {name} was copied into: {des}", fileInfo.Name, tempFile);
                    }

                    fileInfo.CopyTo(tempFile);
                }
                else
                {
                    Log.Verbose("Found a invaild file while building setup exe", fileInfo.Name);
                }
            }

            Log.Information("Addeing prepared files into archive...");

            foreach (string file in Directory.GetFiles(FS.TempSetup))
            {
                AddFile(file);
            }

            if (buildAsPortable)
            {
                if (File.Exists(aepkgOutputPath))
                {
                    Log.Debug("Addeing aepkg to archive...");

                    AddFile(aepkgOutputPath);
                }
            }

            Log.Information("Compiling setup EXE...");

            string exePath = Path.Combine(FS.OutputBin, $"{header.output.installerName}.exe");

            CompileSetupEXE(exePath, "", SrcBuilder.GetInstallCode(header, buildAsPortable));

            Log.Information("Setup EXE complete!\n\t| Path: {path}", exePath);

            // Cleanup TODO(KEN) add more cleanup

            foreach (string file in Directory.GetFiles(FS.BaseDirectory))
            {
                FileInfo fileInfo = new FileInfo(file);
                if (fileInfo.Extension == ".gz")
                {
                    fileInfo.Delete();
                }
            }

            foreach (string file in Directory.GetFiles(FS.TempSetup))
            {
                Log.Debug("DEBUG fileInfo.Delete() {file}", file);
                //File.Delete(file);//TODO(Ken) DEGUB
            }

            void AddFile(string filename)
            {
                // Compress input file using System.IO.Compression
                using (Stream file = File.OpenRead(filename))
                {
                    byte[] buffer = new byte[file.Length];

                    if (file.Length != file.Read(buffer, 0, buffer.Length))
                    {
                        Log.Fatal("Unable to read file: {file}", filename);
                        throw new IOException("Unable to read " + filename);
                    }

                    using (Stream gzFile = File.Create(filename + ".gz"))
                    {
                        using (Stream gzip = new GZipStream(gzFile, CompressionMode.Compress))
                        {
                            gzip.Write(buffer, 0, buffer.Length);
                        }
                    }
                }

                // Store filename so we can embed it on CompileArchive() call
                filenames.Add(filename + ".gz");
            }
        }

        public void CompileSetupEXE(string archiveFilename, string iconFilename, params string[] sourceFiles)
        {
            CodeDomProvider csc = new CSharpCodeProvider();
            CompilerParameters cp = new CompilerParameters();

            cp.GenerateExecutable = true;
            cp.OutputAssembly = archiveFilename;
            cp.CompilerOptions = "/target:winexe";

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
                StringBuilder msg = new StringBuilder($"Errors building {cr.PathToAssembly}");

                foreach (CompilerError ce in cr.Errors)
                {
                    msg.AppendLine(Environment.NewLine + ce.ToString());
                }

                Log.Fatal(msg.ToString());
            }
        }
    }

    public class SrcBuilder
    {
        public static string GetInstallCode(FEPackManifest header, bool buildAsPortable = false)
        {
            var folderName = header.output.shortcutName;
            var exeName = $"{header.output.launcherName}.exe";

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

            var srcPath = Path.Combine(FS.Temp, "src");

            Directory.CreateDirectory(srcPath);

            var aieCS = Path.Combine(srcPath, "aei.cs");

            Log.Debug("Writing code to: {codepath}", aieCS);

            File.WriteAllText(aieCS, code);

            return aieCS;
        }
    }
}
