using System;
using System.ComponentModel.Design;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio;
using System.IO;
using EnvDTE;
using EnvDTE80;
using System.Linq;

namespace UnityBuildMenuProject {
    internal sealed class SolutionUnityBuildControl {
        public const int CommandId = 0x0103;
        public static readonly Guid CommandSet = new Guid("96eb8471-a0b1-481e-9eec-54879db598ae");

        private readonly Package package;
        Dictionary<string, int> projectDic = new Dictionary<string, int>();
        List<KeyValuePair<string, int>> listDic;

        private SolutionUnityBuildControl(Package package) {
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

        public static SolutionUnityBuildControl Instance {
            get;
            private set;
        }

        private IServiceProvider ServiceProvider {
            get {
                return this.package;
            }
        }

        public static void Initialize(Package package) {
            Instance = new SolutionUnityBuildControl(package);
        }

        private void OnBeforeQueryStatus(object sender, EventArgs e) {
            OleMenuCommand mc = sender as OleMenuCommand;

            UnityBuildProjectPackage unityBuildProjectPackage = package as UnityBuildProjectPackage;
            projectDic = unityBuildProjectPackage.GetUnityBuildDirectoryInfo();
            listDic = new List<KeyValuePair<string, int>>(projectDic);

            List<int> trueKeyValue = new List<int>();
            List<int> falseKeyValue = new List<int>();
            List<int> enableKeyValue = new List<int>();
            
            if (mc != null) {
                for (int i = 0; i < listDic.Count; ++i) {
                    trueKeyValue.Add(1);
                    falseKeyValue.Add(0);
                    enableKeyValue.Add(listDic[i].Value);
                }

                bool enableTrue = enableKeyValue.SequenceEqual(trueKeyValue);
                bool enableFalse = enableKeyValue.SequenceEqual(falseKeyValue);

                if(enableTrue == false && enableFalse == false) {
                    mc.Enabled = true;
                    trueKeyValue.Clear();
                    falseKeyValue.Clear();
                    return;

                } else {
                    if(enableTrue == true & enableFalse == false) {
                        if (mc.CommandID.ID == 0x0103) {
                            mc.Enabled = true;
                        } else if (mc.CommandID.ID == 0x0104) {
                            mc.Enabled = false;
                        }
                    } else if(enableTrue == false & enableFalse == true) {
                        if (mc.CommandID.ID == 0x0103) {
                            mc.Enabled = false;
                        } else if (mc.CommandID.ID == 0x0104) {
                            mc.Enabled = true;
                        }
                    }
                }
                trueKeyValue.Clear();
                falseKeyValue.Clear();
            }
        }

        public static DTE2 GetActiveIDE() {
            DTE2 dte2 = Package.GetGlobalService(typeof(DTE)) as DTE2;
            return dte2;
        }

        public abstract class EnvDTEProjectKinds {
            public const string vsProjectKindSolutionFolder = "{66A26720-8FB5-11D2-AA7E-00C04F688DDE}";
        }

        private static IEnumerable<Project> GetSolutionFolderProjects(Project solutionFolder) {
            List<Project> list = new List<Project>();
            for (var i = 1; i <= solutionFolder.ProjectItems.Count; i++) {
                var subProject = solutionFolder.ProjectItems.Item(i).SubProject;
                if (subProject == null) {
                    continue;
                }
                if (subProject.Kind == EnvDTEProjectKinds.vsProjectKindSolutionFolder) {
                    list.AddRange(GetSolutionFolderProjects(subProject));
                } else {
                    list.Add(subProject);
                }
            }
            return list;
        }

        public static IList<Project> Projects() {
            Projects projects = GetActiveIDE().Solution.Projects;
            List<Project> list = new List<Project>();
            var item = projects.GetEnumerator();
            while (item.MoveNext()) {
                var project = item.Current as Project;
                if (project == null) {
                    continue;
                }
                if (project.Kind == EnvDTEProjectKinds.vsProjectKindSolutionFolder) {
                    list.AddRange(GetSolutionFolderProjects(project));
                } else {
                    list.Add(project);
                }
            }
            return list;
        }

        private void Execute(object sender, EventArgs e) {
            OleMenuCommand mc = sender as OleMenuCommand;

            ThreadHelper.ThrowIfNotOnUIThread();
            string message = string.Format(CultureInfo.CurrentCulture, "SolutionUnityBuildProject", this.GetType().FullName);
            string title = "SolutionUnityBuildControl";

            ProjectParser projParser = new ProjectParser();

            DTE2 dte2 = Package.GetGlobalService(typeof(DTE)) as DTE2;
            string slnFilePath = dte2.Solution.FileName;
            slnFilePath = Path.GetDirectoryName(slnFilePath);

            string slnFileName = "";
            DirectoryInfo directory = new DirectoryInfo(slnFilePath);

            foreach (FileInfo file in directory.GetFiles()) {
                if (file.Extension.ToLower().CompareTo(".sln") == 0) {
                    slnFileName = file.Name.Substring(0, file.Name.Length);
                }
            }

            List<string> projFiles = new List<string>();
            IList<Project> projFilePaths;
            projFilePaths = Projects();
   
            bool unitybuild = false;
            
            int result = VsShellUtilities.ShowMessageBox(
                this.package,
                message,
                title,
                OLEMSGICON.OLEMSGICON_INFO,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

            if (result == (int)VSConstants.MessageBoxResult.IDOK) {
                if(mc.CommandID.ID == 0x0103) {
                    unitybuild = true;
                }else if(mc.CommandID.ID == 0x0104) {
                    unitybuild = false;
                }
                
                for (int i = 0; i < listDic.Count; i++) {
                    projParser.ModifyUnityBuildXML(listDic[i].Key, slnFileName, unitybuild, projFilePaths[i].UniqueName, false);

                    if (listDic[i].Key.Contains(listDic[i].Key)) {
                        if (unitybuild == true) {
                            projectDic[listDic[i].Key] = 0;
                        } else {
                            projectDic[listDic[i].Key] = 1;
                        }
                    }
                }
            }
        }
    }
}
