using System;
using System.ComponentModel.Design;
using System.Globalization;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio;
using System.IO;
using System.Collections.Generic;

namespace UnityBuildMenuProject {
    internal sealed class UnityBuildProject {
        public const int CommandId = 0x0101;
        public static readonly Guid CommandSet = new Guid("8f3ff30b-17c9-4377-8378-020d142a941c");

        private readonly Package package;
        Dictionary<string, int> projectDic = new Dictionary<string, int>();
        List<KeyValuePair<string, int>> listDic;

        private UnityBuildProject(Package package) {

            if (package == null) {
                throw new ArgumentNullException("package");
            }
            this.package = package;

            OleMenuCommandService commandService = this.ServiceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null) {

                for(int i = 0; i < 2; ++i) {
                    CommandID menuCommandID = new CommandID(CommandSet, CommandId + i);
                    OleMenuCommand menuItem = new OleMenuCommand(this.Execute, menuCommandID);
                    menuItem.BeforeQueryStatus += OnBeforeQueryStatus;
                    commandService.AddCommand(menuItem);
                }
            }
        }

        public UnityBuildProject() {
        }

        public static UnityBuildProject Instance {
            get;
            private set;
        }

        private IServiceProvider ServiceProvider {
            get {
                return this.package;
            }
        }

        public static void Initialize(Package package) {

            Instance = new UnityBuildProject(package);
        }

        private void OnBeforeQueryStatus(object sender, EventArgs e) {
            OleMenuCommand mc = sender as OleMenuCommand;

            EnvDTE.DTE dte;
            EnvDTE.Project project;
            object[] activeSolutionProjects;
            string activeProject = "";

            dte = (EnvDTE.DTE)Microsoft.VisualStudio.Shell.ServiceProvider.GlobalProvider.GetService(typeof(EnvDTE.DTE));
            activeSolutionProjects = dte.ActiveSolutionProjects as object[];
            
            if (activeSolutionProjects != null) {
                foreach (object activeSolutionProject in activeSolutionProjects) {
                    project = activeSolutionProject as EnvDTE.Project;
                    activeProject = project.FullName;
                }
            }

            UnityBuildProjectPackage unityBuildProjectPackage = package as UnityBuildProjectPackage;
            projectDic  = unityBuildProjectPackage.GetUnityBuildDirectoryInfo();
            
            listDic = new List<KeyValuePair<string, int>>(projectDic);

            if(mc != null) {
                for (int i = 0; i < projectDic.Count; ++i) {
                    if (listDic[i].Key == activeProject) {
                        if(listDic[i].Value == 2) {
                            mc.Enabled = true;
                            return;
                        }else if (mc.CommandID.ID == 257 && listDic[i].Value == 0) {
                            mc.Enabled = false;
                        } else if (mc.CommandID.ID == 257 && listDic[i].Value == 1) {
                            mc.Enabled = true;
                        } else if (mc.CommandID.ID == 258 && listDic[i].Value == 0) {
                            mc.Enabled = true;
                        } else if (mc.CommandID.ID == 258 && listDic[i].Value == 1) {
                            mc.Enabled = false;
                        }
                    }
                }
            }
        }

        private void Execute(object sender, EventArgs e) {
            OleMenuCommand mc = sender as OleMenuCommand;
                
            ThreadHelper.ThrowIfNotOnUIThread();
            string message = string.Format(CultureInfo.CurrentCulture, "UnityBuildProject", sender.GetType().FullName);
            EnvDTE.DTE dte;
            EnvDTE.Project project;
            object[] activeSolutionProjects;
            string title = "UnityBuildProject";

            bool UnityBuild = false;
            ProjectParser projParser = new ProjectParser();

            EnvDTE80.DTE2 dte2 = Package.GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE80.DTE2;
            string slnFilePath = dte2.Solution.FileName;
            slnFilePath = Path.GetDirectoryName(slnFilePath);

            string slnFileName = "";
            DirectoryInfo directory = new DirectoryInfo(slnFilePath);

            foreach (FileInfo file in directory.GetFiles()) {
                if (file.Extension.ToLower().CompareTo(".sln") == 0) {
                    slnFileName = file.Name.Substring(0, file.Name.Length);
                }
            }

            dte = (EnvDTE.DTE)Microsoft.VisualStudio.Shell.ServiceProvider.GlobalProvider.GetService(typeof(EnvDTE.DTE));
            activeSolutionProjects = dte.ActiveSolutionProjects as object[];
            
            if(activeSolutionProjects != null) {
                foreach(object activeSolutionProject in activeSolutionProjects) {
                    project = activeSolutionProject as EnvDTE.Project;
            
                    if(project != null) {
                        message = $"Called on {project.FullName}";

                        string projFileName = project.FileName;
                        string uniqueName = project.UniqueName;

                        int result = VsShellUtilities.ShowMessageBox(
                            package,
                            message,
                            title,
                            OLEMSGICON.OLEMSGICON_INFO,
                            OLEMSGBUTTON.OLEMSGBUTTON_OK,
                            OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                        
                        if (result == (int)VSConstants.MessageBoxResult.IDOK) {
                            if (mc.CommandID.ID == 257) {
                                UnityBuild = true;
                                projParser.ModifyUnityBuildXML(projFileName, slnFileName, UnityBuild, uniqueName, false);
                            }else if(mc.CommandID.ID == 258) {
                                UnityBuild = false;
                                projParser.ModifyUnityBuildXML(projFileName, slnFileName, UnityBuild, uniqueName, false);
                            }

                            for(int i = 0; i < listDic.Count; i++) {
                                if (listDic[i].Key.Contains(projFileName)) {
                                   if(projectDic[projFileName] == 2) {
                                        projectDic[projFileName] = 0;
                                    } else if(projectDic[projFileName] == 1) {
                                        projectDic[projFileName] = 0;
                                    } else if(projectDic[projFileName] == 0) {
                                        projectDic[projFileName] = 1;
                                    }
                                }
                            }
                        }
                    }
                } 
            }
        }
    }
}
