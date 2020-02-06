using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using EnvDTE;
using System.Linq;
using VSLangProj;
using System.Collections.Generic;

namespace UnityBuildMenuProject {
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
    [Guid(UnityBuildProjectPackage.PackageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideAutoLoad(UIContextGuids80.SolutionExists)]

    public sealed class UnityBuildProjectPackage : Package, IVsSolutionEvents{
        public const string PackageGuidString = "fabcf519-7820-4a6b-be4b-a0d2cd359773";

        private DTE _dte = null;
        private IVsSolution solution = null;
        private uint _hSolutionEvents = uint.MaxValue;

        private Dictionary<string, int> dictionaryInfo;

        public UnityBuildProjectPackage() {
        
        }

        protected override void Initialize() {
            base.Initialize();

            _dte = (DTE)this.GetService(typeof(DTE));
            AdviseSolutionEvents();

            UnityBuildProject.Initialize(this);
            SolutionUnityBuildControl.Initialize(this);
        }

        public void SetUnityBuildDirectoryInfo(Dictionary<string, int> dictionary) {
            this.dictionaryInfo = dictionary;
        }

        public Dictionary<string, int> GetUnityBuildDirectoryInfo() {
            return dictionaryInfo;
        }

        protected override void Dispose(bool disposing) {
            UnadviseSolutionEvents();
            base.Dispose(disposing);
        }

        private void AdviseSolutionEvents() {
            UnadviseSolutionEvents();

            solution = this.GetService(typeof(SVsSolution)) as IVsSolution;
            if (solution != null) {
                solution.AdviseSolutionEvents(this, out _hSolutionEvents);
            }
        }

        private void UnadviseSolutionEvents() {
            if (solution != null) {
                if (_hSolutionEvents != uint.MaxValue) {
                    solution.UnadviseSolutionEvents(_hSolutionEvents);
                    _hSolutionEvents = uint.MaxValue;
                }
                solution = null;
            }
        }

        public static IEnumerable<EnvDTE.Project> GetProjects(IVsSolution solution) {
            foreach (IVsHierarchy hier in GetProjectsInSolution(solution)) {
                EnvDTE.Project project = GetDTEProject(hier);
                if (project != null)
                    yield return project;
            }
        }

        public static IEnumerable<IVsHierarchy> GetProjectsInSolution(IVsSolution solution) {
            return GetProjectsInSolution(solution, __VSENUMPROJFLAGS.EPF_LOADEDINSOLUTION);
        }

        public static IEnumerable<IVsHierarchy> GetProjectsInSolution(IVsSolution solution, __VSENUMPROJFLAGS flags) {
            if (solution == null)
                yield break;

            IEnumHierarchies enumHierarchies;
            Guid guid = Guid.Empty;
            solution.GetProjectEnum((uint)flags, ref guid, out enumHierarchies);
            if (enumHierarchies == null)
                yield break;

            IVsHierarchy[] hierarchy = new IVsHierarchy[1];
            uint fetched;
            while (enumHierarchies.Next(1, hierarchy, out fetched) == VSConstants.S_OK && fetched == 1) {
                if (hierarchy.Length > 0 && hierarchy[0] != null)
                    yield return hierarchy[0];
            }
        }

        public static EnvDTE.Project GetDTEProject(IVsHierarchy hierarchy) {
            if (hierarchy == null)
                throw new ArgumentNullException("hierarchy");

            object obj;
            hierarchy.GetProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_ExtObject, out obj);
            return obj as EnvDTE.Project;
        }

        private T GetPropertyValue<T>(IVsSolution solutionInterface, __VSPROPID solutionProperty) {
            object value = null;
            T result = default(T);

            if (solutionInterface.GetProperty((int)solutionProperty, out value) == Microsoft.VisualStudio.VSConstants.S_OK) {
                result = (T)value;
            }
            return result;
        }

        int IVsSolutionEvents.OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded) {
            uint pc;
            pHierarchy.AdviseHierarchyEvents(new myHyEvent(pHierarchy, this), out pc);
            return VSConstants.S_OK;
        }

        int IVsSolutionEvents.OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel) {
            return VSConstants.S_OK;
        }

        int IVsSolutionEvents.OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved) {
            return VSConstants.S_OK;
        }

        int IVsSolutionEvents.OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy) {
            return VSConstants.S_OK;
        }

        int IVsSolutionEvents.OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel) {
            return VSConstants.S_OK;
        }

        int IVsSolutionEvents.OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy) {
            return VSConstants.S_OK;
        }

        int IVsSolutionEvents.OnAfterOpenSolution(object pUnkReserved, int fNewSolution) {
            string solutionDirectory;
            string solutionFile;
            string userOptsFile;
            solution.GetSolutionInfo(out solutionDirectory, out solutionFile, out userOptsFile);
            solutionFile = solutionFile.Substring(solutionFile.LastIndexOf(("\\")) + 1);

            Dictionary<string, int> dictionary = new Dictionary<string, int>();
            List<string> projectInfo = new List<string>();
            List<string> projectUniqueName = new List<string>();
            ProjectParser parser = new ProjectParser();
            int enableInfo = 0;

            foreach(Project project in GetProjects(solution)) {
                if(project.FileName == "") {
                    continue;
                }
                projectInfo.Add(project.FileName);
                projectUniqueName.Add(project.UniqueName);
            }

            for (int i = 0; i < projectInfo.Count; ++i) {
                string projectPathInfo = projectInfo[i];
                parser.ModifyUnityBuildXML(projectPathInfo, solutionFile, false, projectUniqueName[i], true);
                enableInfo = parser.GetUnityBuildMenuInfo();
                dictionary.Add(projectPathInfo, enableInfo);
            }
            SetUnityBuildDirectoryInfo(dictionary);

            return VSConstants.S_OK;
        }

        int IVsSolutionEvents.OnQueryCloseSolution(object pUnkReserved, ref int pfCancel) {
            return VSConstants.S_OK;
        }

        int IVsSolutionEvents.OnBeforeCloseSolution(object pUnkReserved) {
            return VSConstants.S_OK;
        }

        int IVsSolutionEvents.OnAfterCloseSolution(object pUnkReserved) {
            return VSConstants.S_OK;
        }
    }

    class myHyEvent : IVsHierarchyEvents {
        EnvDTE.Project project;
        DTE _dte = (EnvDTE.DTE) Microsoft.VisualStudio.Shell.ServiceProvider.GlobalProvider.GetService(typeof(EnvDTE.DTE));
        private IVsHierarchy hierarchy;
        IVsOutputWindowPane output;
        private readonly Package package;
        string resFileName = "";
        int moamoaStatus;

        internal myHyEvent(IVsHierarchy hierarchy, Package package) {
            this.hierarchy = hierarchy;
            this.package = package;
        }
    
        private Project[] GetProjects() {
            return _dte.Solution.Projects
                .Cast<Project>()
                .Select(x => ((VSProject)x.Object).Project)
                .ToArray();
        }

        int IVsHierarchyEvents.OnItemAdded(uint itemidParent, uint itemidSiblingPrev, uint itemidAdded) {
            string activeProject = "";
            
            object[] activeSolutionProjects;
            activeSolutionProjects = _dte.ActiveSolutionProjects as object[];
            if (activeSolutionProjects != null) {
                foreach (object activeSolutionProject in activeSolutionProjects) {
                    project = activeSolutionProject as EnvDTE.Project;
                    activeProject = project.FullName;
                }
            }
            
            UnityBuildProjectPackage unityBuildProjectPackage = package as UnityBuildProjectPackage;
            Dictionary<string, int> dicInfo = unityBuildProjectPackage.GetUnityBuildDirectoryInfo();
            List<KeyValuePair<string, int>> listDic;
            listDic = new List<KeyValuePair<string, int>>(dicInfo);
            
            for (int i = 0; i < listDic.Count; ++i) {
                if (listDic[i].Key == activeProject) {
                    int moamoaStatus = listDic[i].Value;
                }
            }

            object res;
            hierarchy.GetProperty(itemidAdded, (int)__VSHPROPID.VSHPROPID_ExtObject, out res);
            dynamic resFileName = res as EnvDTE.ProjectItem;

            Microsoft.VisualStudio.VCProjectEngine.VCFile file = resFileName.Object as Microsoft.VisualStudio.VCProjectEngine.VCFile;
            if(file.Extension == ".h") {
                return VSConstants.S_OK;
            }

            foreach (Microsoft.VisualStudio.VCProjectEngine.VCFileConfiguration info in file.FileConfigurations) {
                if(moamoaStatus == 0) {
                    info.ExcludedFromBuild = true;
                }else if(moamoaStatus == 1) {
                    info.ExcludedFromBuild = false;
                }
            }
            project.Save();
            return VSConstants.S_OK;
        }
    
        int IVsHierarchyEvents.OnItemsAppended(uint itemidParent) {
            return VSConstants.S_OK;
        }
        
        int IVsHierarchyEvents.OnItemDeleted(uint itemid) {
            return VSConstants.S_OK;
        }
    
        int IVsHierarchyEvents.OnPropertyChanged(uint itemid, int propid, uint flags) {
            return VSConstants.S_OK;
        }
    
        int IVsHierarchyEvents.OnInvalidateItems(uint itemidParent) {
            return VSConstants.S_OK;
        }
        
        int IVsHierarchyEvents.OnInvalidateIcon(IntPtr hicon) {
            return VSConstants.S_OK;
        }
    };
}
